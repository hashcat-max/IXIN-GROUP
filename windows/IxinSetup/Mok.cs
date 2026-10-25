using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace IxinSetup
{
    /// <summary>
    /// Clé Secure Boot d'IXIN : demande d'enregistrement dans la liste de shim (MOK), depuis Windows.
    /// Comme « mokutil --import » sous Linux : IXIN OS Setup écrit les variables UEFI MokNew (le certificat)
    /// et MokAuth (empreinte du mot de passe) ; au redémarrage, shim ouvre MokManager, où l'utilisateur
    /// confirme l'enregistrement et tape le mot de passe. Secure Boot reste activé.
    /// </summary>
    internal static class Mok
    {
        public const string Password = "ixinos";
        public const string ShimGuid = "{605DAB50-E046-4300-ABB6-3DD810DD8B23}";
        private static readonly Guid CertX509 = new Guid("a5c059a1-94e4-4aa7-87b5-ab155c2bf072");
        private static readonly Guid ShimLock = new Guid("605dab50-e046-4300-abb6-3dd810dd8b23");
        private const uint Attributes = 0x7;   // NON_VOLATILE | BOOTSERVICE_ACCESS | RUNTIME_ACCESS

        /// <summary>Liste de signatures UEFI (EFI_SIGNATURE_LIST) contenant un certificat X.509, comme mokutil.</summary>
        public static byte[] SignatureList(byte[] certDer)
        {
            int sigSize = 16 + certDer.Length;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(CertX509.ToByteArray());     // SignatureType
                w.Write((uint)(28 + sigSize));       // SignatureListSize
                w.Write(0u);                         // SignatureHeaderSize
                w.Write((uint)sigSize);              // SignatureSize
                w.Write(ShimLock.ToByteArray());     // SignatureOwner
                w.Write(certDer);
                return ms.ToArray();
            }
        }

        /// <summary>MokAuth : SHA-256 de MokNew suivi du mot de passe en UCS-2 (format accepté par MokManager).</summary>
        public static byte[] Auth(byte[] mokNew, string password)
        {
            using (var sha = SHA256.Create())
            {
                var data = mokNew.Concat(Encoding.Unicode.GetBytes(password)).ToArray();
                return sha.ComputeHash(data);
            }
        }

        // ── variables UEFI depuis Windows ────────────────────────────────────

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetFirmwareEnvironmentVariableExW(string name, string guid, byte[] value, int size, uint attributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetFirmwareEnvironmentVariableW(string name, string guid, byte[] buffer, int size);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValueW(string system, string name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, int length, IntPtr previous, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct TokenPrivileges
        {
            public int Count;
            public long Luid;
            public int Attributes;
        }

        /// <summary>Droit de modifier les variables du firmware (les administrateurs l'ont, désactivé par défaut).</summary>
        private static void EnablePrivilege()
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out IntPtr token))   // ADJUST_PRIVILEGES | QUERY
                throw new Win32Exception();
            try
            {
                if (!LookupPrivilegeValueW(null, "SeSystemEnvironmentPrivilege", out long luid)) throw new Win32Exception();
                var tp = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 0x2 };      // SE_PRIVILEGE_ENABLED
                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() != 0)
                    throw new Win32Exception();
            }
            finally
            {
                CloseHandle(token);
            }
        }

        private static byte[] Read(string name)
        {
            var buffer = new byte[64 * 1024];
            int n = GetFirmwareEnvironmentVariableW(name, ShimGuid, buffer, buffer.Length);
            return n > 0 ? buffer.Take(n).ToArray() : null;
        }

        /// <summary>Vrai si le certificat est déjà dans la liste de shim (MokListRT, recopiée par shim à chaque démarrage).</summary>
        public static bool IsEnrolled(byte[] certDer)
        {
            try
            {
                EnablePrivilege();
                var list = Read("MokListRT");
                return list != null && IndexOf(list, certDer) >= 0;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is EntryPointNotFoundException)
            {
                Log.Warn("Liste des clés de shim illisible : " + ex.Message);
                return false;
            }
        }

        /// <summary>Demande l'enregistrement du certificat au prochain démarrage (MokManager).</summary>
        public static void RequestEnrollment(byte[] certDer)
        {
            EnablePrivilege();
            var mokNew = SignatureList(certDer);
            var auth = Auth(mokNew, Password);
            if (!SetFirmwareEnvironmentVariableExW("MokNew", ShimGuid, mokNew, mokNew.Length, Attributes) ||
                !SetFirmwareEnvironmentVariableExW("MokAuth", ShimGuid, auth, auth.Length, Attributes))
                throw new SetupException("Impossible de préparer l'enregistrement de la clé IXIN dans le firmware : " +
                                         new Win32Exception(Marshal.GetLastWin32Error()).Message);
            Log.Info("Enregistrement de la clé Secure Boot IXIN demandé (MokManager au prochain démarrage)");
        }

        /// <summary>Annule une demande (préparation annulée).</summary>
        public static void CancelRequest()
        {
            try
            {
                EnablePrivilege();
                SetFirmwareEnvironmentVariableExW("MokNew", ShimGuid, null, 0, Attributes);
                SetFirmwareEnvironmentVariableExW("MokAuth", ShimGuid, null, 0, Attributes);
            }
            catch (Exception ex) when (ex is Win32Exception || ex is EntryPointNotFoundException)
            {
                Log.Warn("Annulation de la demande de clé : " + ex.Message);
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
