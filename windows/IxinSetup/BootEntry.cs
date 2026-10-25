using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IxinSetup
{
    /// <summary>
    /// Menu de démarrage de l'installation : chargeur GRUB dans la partition EFI de Windows
    /// (\EFI\IXIN-SETUP\grubx64.efi) et entrée « IXIN OS Setup » du firmware (via bcdedit).
    /// </summary>
    internal static class BootEntry
    {
        public const string Description = "IXIN OS Setup";
        public const string EspDirectory = @"EFI\IXIN-SETUP";
        public const string LoaderPath = @"\EFI\IXIN-SETUP\grubx64.efi";
        public const string ShimPath = @"\EFI\IXIN-SETUP\shimx64.efi";   // Secure Boot : shim devant le chargeur
        public const string OptionsFile = "options.cfg";   // options de l'installation, lues par le menu de démarrage
        public const string IxinLoaderPath = @"\EFI\IXIN\grubx64.efi";   // IXIN OS installé (son menu GRUB)
        public const string IxinShimPath = @"\EFI\IXIN\shimx64.efi";     // IXIN OS installé, compatible Secure Boot
        private const string FwBootMgr = "{a5a30fa2-3d06-4e9f-b5f4-a01df9d1fcba}";  // {fwbootmgr} affiché en entier (/v)
        private static readonly Regex Guid = new Regex(@"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}");

        // ── outils ────────────────────────────────────────────────────────────

        public static string Run(string exe, string args, bool check = true)
        {
            int code = Exec(exe, args, out string output);
            if (check && code != 0)
                throw new SetupException($"La commande « {exe} {args} » a échoué : {output.Trim()}");
            return output;
        }

        /// <summary>Lance un programme de System32 et renvoie son code de sortie.</summary>
        public static int Exec(string exe, string args, out string output)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, exe), args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                var error = p.StandardError.ReadToEndAsync();   // lu en parallèle : pas de blocage si la sortie d'erreur est remplie
                output = p.StandardOutput.ReadToEnd() + error.Result;
                p.WaitForExit();
                Log.Info($"{exe} {args} → {p.ExitCode}");
                return p.ExitCode;
            }
        }

        public static string FreeDriveLetter()
        {
            var used = new HashSet<char>(DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])));
            for (char c = 'Z'; c >= 'G'; c--)
            {
                if (!used.Contains(c)) return c.ToString();
            }
            throw new SetupException("Aucune lettre de lecteur libre pour accéder à la partition EFI.");
        }

        // ── partition EFI ─────────────────────────────────────────────────────

        /// <summary>Monte la partition EFI qui a servi à démarrer Windows (mountvol /S).</summary>
        public static string MountSystemEsp()
        {
            string letter = FreeDriveLetter();
            Run("mountvol.exe", letter + ": /S");
            return letter;
        }

        public static void Unmount(string letter)
        {
            Run("mountvol.exe", letter + ": /D", check: false);
        }

        public static byte[] Loader()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("grubx64.efi"))
            {
                if (s == null) throw new SetupException("Chargeur de démarrage absent du programme.");
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Contenu de options.cfg : options passées au système d'installation d'IXIN OS par le menu
        /// de démarrage (windows/boot/setup-grub.cfg). ASCII seulement : lu par GRUB.
        /// </summary>
        public static string OptionsContent(string espGuid, bool keepSetupPartition)
        {
            var options = new List<string>();
            string partuuid = (espGuid ?? "").Trim('{', '}', ' ').ToLowerInvariant();
            if (Regex.IsMatch(partuuid, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"))
                options.Add("ixin.esp=" + partuuid);
            if (keepSetupPartition) options.Add("ixin.keepsetup=1");
            return "# Options choisies dans IXIN OS Setup (lues par le menu de demarrage de l'installation)\n" +
                   "set ixin_options=\"" + string.Join(" ", options) + "\"\n";
        }

        private static byte[] Resource(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Version construite avec la clé Secure Boot d'IXIN : shim et certificat inclus.</summary>
        public static bool SecureBootCapable => Resource("shimx64.efi") != null && Resource("ixin-secureboot.cer") != null;

        /// <summary>Certificat Secure Boot d'IXIN (DER), ou null.</summary>
        public static byte[] SecureBootCertificate => Resource("ixin-secureboot.cer");

        /// <summary>Chemin démarré par l'entrée « IXIN OS Setup » : shim si Secure Boot est pris en charge.</summary>
        public static string EntryPath => SecureBootCapable ? ShimPath : LoaderPath;

        public static void InstallLoader(string espLetter, string options = null)
        {
            string dir = espLetter + @":\" + EspDirectory;
            byte[] loader = Loader();
            var drive = new DriveInfo(espLetter);
            if (drive.AvailableFreeSpace < loader.Length + (2 << 20))
                throw new SetupException($"La partition EFI est pleine ({Facts.Gb(drive.AvailableFreeSpace)} libres).");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "grubx64.efi"), loader);
            foreach (var name in new[] { "shimx64.efi", "mmx64.efi", "ixin-secureboot.cer" })
            {
                var data = Resource(name);
                if (data != null) File.WriteAllBytes(Path.Combine(dir, name), data);
            }
            File.WriteAllText(Path.Combine(dir, "LISEZMOI.txt"),
                "Menu de démarrage de l'installation d'IXIN OS, ajouté par IXIN OS Setup.\r\n" +
                "Il est supprimé automatiquement après l'installation, ou par « Annuler la préparation »\r\n" +
                "dans IXIN OS Setup.\r\n", Encoding.UTF8);
            if (options != null)
                File.WriteAllText(Path.Combine(dir, OptionsFile), options, new UTF8Encoding(false));   // sans BOM : lu par GRUB
            using (var sha = SHA256.Create())
            {
                if (!sha.ComputeHash(File.ReadAllBytes(Path.Combine(dir, "grubx64.efi"))).SequenceEqual(sha.ComputeHash(loader)))
                    throw new SetupException("La copie du chargeur de démarrage dans la partition EFI est incorrecte.");
            }
            Log.Info("Chargeur installé dans la partition EFI : " + dir);
        }

        public static void RemoveLoader(string espLetter)
        {
            string dir = espLetter + @":\" + EspDirectory;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Log.Info("Chargeur retiré : " + dir);
            }
        }

        // ── entrée du firmware ────────────────────────────────────────────────

        /// <summary>
        /// Crée l'entrée « IXIN OS Setup » dans le menu de démarrage du firmware. Elle démarre la
        /// partition EFI de Windows, ou celle montée sur <paramref name="deviceLetter"/> si indiquée
        /// (partition EFI dédiée, ou partition choisie dans les options avancées).
        /// </summary>
        public static string Create(string description = Description, string path = null, string deviceLetter = null)
        {
            path = path ?? EntryPath;
            foreach (var old in ParseEntries(Run("bcdedit.exe", "/enum firmware /v", check: false), path)) Delete(old);
            string output = Run("bcdedit.exe", $"/copy {{bootmgr}} /d \"{description}\"");
            var m = Guid.Match(output);
            if (!m.Success) throw new SetupException("Identifiant de la nouvelle entrée de démarrage introuvable : " + output.Trim());
            string id = m.Value;
            try
            {
                Run("bcdedit.exe", $"/set {id} path {path}");
                if (!string.IsNullOrEmpty(deviceLetter))
                    Run("bcdedit.exe", $"/set {id} device partition={deviceLetter}:");
                Run("bcdedit.exe", $"/set {{fwbootmgr}} displayorder {id} /addlast");
            }
            catch
            {
                Delete(id);
                throw;
            }
            Log.Info("Entrée de démarrage créée : " + id);
            return id;
        }

        /// <summary>Entrées existantes qui démarrent le chargeur d'IXIN OS Setup.</summary>
        public static List<string> Find()
        {
            string output = Run("bcdedit.exe", "/enum firmware /v", check: false);
            return ParseEntries(output, ShimPath).Concat(ParseEntries(output, LoaderPath)).Distinct().ToList();
        }

        /// <summary>Entrées du firmware qui démarrent IXIN OS installé.</summary>
        public static List<string> FindIxin()
        {
            string output = Run("bcdedit.exe", "/enum firmware /v", check: false);
            return ParseEntries(output, IxinShimPath).Concat(ParseEntries(output, IxinLoaderPath)).Distinct().ToList();
        }

        /// <summary>Première entrée de l'ordre de démarrage du firmware (null si inconnue).</summary>
        public static string FirstInBootOrder()
        {
            string output = Run("bcdedit.exe", "/enum {fwbootmgr} /v", check: false);
            return Guid.Matches(output).Cast<Match>().Select(m => m.Value)
                .FirstOrDefault(v => !v.Equals(FwBootMgr, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Remet l'entrée en tête de l'ordre de démarrage : Windows se replace parfois en premier
        /// (après certaines mises à jour), et le PC démarre alors sans afficher le menu IXIN.
        /// </summary>
        public static void PutFirst(string id)
        {
            Run("bcdedit.exe", $"/set {{fwbootmgr}} displayorder {id} /addfirst");
            Log.Info("Entrée de démarrage placée en premier : " + id);
        }

        /// <summary>
        /// Cherche dans la sortie de bcdedit les blocs dont le chemin est celui du chargeur indiqué
        /// (les noms des champs sont traduits selon la langue de Windows, pas les valeurs).
        /// </summary>
        public static List<string> ParseEntries(string bcdeditOutput, string loaderPath = LoaderPath)
        {
            var ids = new List<string>();
            foreach (var block in Regex.Split(bcdeditOutput.Replace("\r", ""), @"\n\s*\n"))
            {
                if (block.IndexOf(loaderPath, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var m = Guid.Match(block);
                if (m.Success && !ids.Contains(m.Value)) ids.Add(m.Value);
            }
            return ids;
        }

        public static void Delete(string id)
        {
            Run("bcdedit.exe", $"/delete {id}", check: false);
            Log.Info("Entrée de démarrage supprimée : " + id);
        }

        /// <summary>Au prochain démarrage seulement, lancer IXIN OS Setup (sans changer l'ordre habituel).</summary>
        public static void BootNextOnce(string id)
        {
            Run("bcdedit.exe", $"/set {{fwbootmgr}} bootsequence {id}");
        }

        public static void Restart(bool toFirmwareSettings)
        {
            string args = toFirmwareSettings
                ? "/r /fw /t 5 /c \"Redémarrage vers les paramètres UEFI pour désactiver Secure Boot\""
                : "/r /t 5 /c \"Redémarrage vers l'installation d'IXIN OS\"";
            Run("shutdown.exe", args);
        }
    }
}
