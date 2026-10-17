using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IxinSetup
{
    /// <summary>Raison pour laquelle ce Windows ne peut pas installer IXIN OS (avant toute analyse du disque).</summary>
    internal sealed class Unsupported
    {
        public string Title;
        public string Text;
    }

    /// <summary>
    /// Compatibilité du PC et de Windows, vérifiée avant toute autre chose : le programme démarre sur
    /// tous les Windows (32 / 64 bits, ARM, anciennes versions) pour expliquer clairement pourquoi
    /// il ne peut pas aller plus loin, au lieu d'une erreur incompréhensible.
    /// </summary>
    internal static class Compat
    {
        private const ushort MachineI386 = 0x014c;
        private const ushort MachineAmd64 = 0x8664;
        private const ushort MachineArm = 0x01c4;
        private const ushort MachineArm64 = 0xaa64;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        /// <summary>Processeur réel du PC (et non celui que Windows émule pour ce programme).</summary>
        public static string NativeArchitecture()
        {
            try
            {
                if (IsWow64Process2(GetCurrentProcess(), out _, out ushort native))
                {
                    switch (native)
                    {
                        case MachineAmd64: return "AMD64";
                        case MachineArm64: return "ARM64";
                        case MachineArm: return "ARM";
                        case MachineI386: return "X86";
                    }
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 10 antérieur à 1511 : variables d'environnement
            }
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ??
                          Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            return arch.ToUpperInvariant();
        }

        /// <summary>Numéro de build de Windows (10240 = première version de Windows 10).</summary>
        public static int WindowsBuild()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (int.TryParse(key?.GetValue("CurrentBuildNumber")?.ToString(), out int b)) return b;
                    if (int.TryParse(key?.GetValue("CurrentBuild")?.ToString(), out b)) return b;
                }
            }
            catch (Exception)
            {
            }
            return Environment.OSVersion.Version.Build;
        }

        public static Unsupported Check()
        {
            return Check(NativeArchitecture(), Environment.Is64BitOperatingSystem, WindowsBuild());
        }

        /// <summary>Règles de compatibilité (séparées pour les tests).</summary>
        public static Unsupported Check(string arch, bool windows64, int build)
        {
            const string usb = " Le bouton « Aide » explique comment installer IXIN OS avec une clé USB.";
            if (arch == "ARM64" || arch == "ARM")
                return new Unsupported
                {
                    Title = "Ce PC a un processeur ARM",
                    Text = "IXIN OS fonctionne sur les PC à processeur Intel ou AMD (64 bits). Ce PC a un processeur ARM " +
                           "(Qualcomm Snapdragon, Microsoft SQ…) : IXIN OS ne peut pas s'y installer, ni avec IXIN OS Setup, " +
                           "ni avec une clé USB.",
                };
            if (!windows64 || arch == "X86")
                return new Unsupported
                {
                    Title = "Windows 32 bits",
                    Text = "Ce Windows est une version 32 bits : IXIN OS Setup ne peut pas préparer l'installation depuis lui. " +
                           "Si le processeur du PC est un modèle 64 bits (presque tous les PC depuis 2008), IXIN OS peut quand même " +
                           "s'installer avec une clé USB." + usb,
                };
            if (build > 0 && build < 10240)
                return new Unsupported
                {
                    Title = "Version de Windows trop ancienne",
                    Text = "IXIN OS Setup fonctionne sous Windows 10 et Windows 11. Ce PC a une version plus ancienne de Windows " +
                           "(7, 8 ou 8.1) : installez IXIN OS avec une clé USB." + usb,
                };
            return null;
        }

        /// <summary>
        /// Services de Windows dont IXIN OS Setup a besoin (lecture des disques, diskpart), parfois
        /// désactivés par des logiciels « d'optimisation » : remis en démarrage manuel, comme à l'origine.
        /// </summary>
        public static void EnsureServices()
        {
            // winmgmt : WMI (disques, BitLocker) ; vds : Disque virtuel (diskpart) ;
            // smphost : fournisseur de stockage (Get-Partition…)
            foreach (var name in new[] { "winmgmt", "vds", "smphost" })
            {
                try
                {
                    int start = ServiceStart(name);
                    if (start != 4) continue;   // 4 : désactivé
                    Log.Warn($"Service « {name} » désactivé : remis en démarrage manuel");
                    BootEntry.Run("sc.exe", $"config {name} start= demand", check: false);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Service « {name} » : {ex.Message}");
                }
            }
        }

        private static int ServiceStart(string name)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
            {
                var v = key?.GetValue("Start");
                return v == null ? -1 : Convert.ToInt32(v);
            }
        }

        /// <summary>Marque du PC, pour indiquer la bonne touche du menu de démarrage.</summary>
        public static string Manufacturer()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    string m = key?.GetValue("SystemManufacturer")?.ToString() ?? "";
                    string board = key?.GetValue("BaseBoardManufacturer")?.ToString() ?? "";
                    return (m.Length == 0 || m.IndexOf("system manufacturer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.IndexOf("to be filled", StringComparison.OrdinalIgnoreCase) >= 0) ? board : m;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static readonly List<KeyValuePair<string, string>> BootKeys = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("ASUS", "F8 (PC de bureau) ou Échap (portable)"),
            new KeyValuePair<string, string>("Acer", "F12"),
            new KeyValuePair<string, string>("Dell", "F12"),
            new KeyValuePair<string, string>("HP", "Échap puis F9"),
            new KeyValuePair<string, string>("Hewlett", "Échap puis F9"),
            new KeyValuePair<string, string>("Lenovo", "F12 (ou Fn + F12)"),
            new KeyValuePair<string, string>("MSI", "F11"),
            new KeyValuePair<string, string>("Micro-Star", "F11"),
            new KeyValuePair<string, string>("Gigabyte", "F12"),
            new KeyValuePair<string, string>("ASRock", "F11"),
            new KeyValuePair<string, string>("Samsung", "Échap ou F2"),
            new KeyValuePair<string, string>("Toshiba", "F12"),
            new KeyValuePair<string, string>("Dynabook", "F12"),
            new KeyValuePair<string, string>("Huawei", "F12"),
            new KeyValuePair<string, string>("Honor", "F12"),
            new KeyValuePair<string, string>("Fujitsu", "F12"),
            new KeyValuePair<string, string>("Medion", "F10"),
            new KeyValuePair<string, string>("Microsoft", "maintenir « Volume − » en allumant la Surface"),
            new KeyValuePair<string, string>("Intel", "F10"),
            new KeyValuePair<string, string>("Sony", "F11 ou le bouton « Assist »"),
            new KeyValuePair<string, string>("Xiaomi", "F12"),
            new KeyValuePair<string, string>("Razer", "F12"),
        };

        /// <summary>Touche du menu de démarrage pour cette marque, ou null si elle n'est pas connue.</summary>
        public static string BootMenuKey(string manufacturer)
        {
            if (string.IsNullOrEmpty(manufacturer)) return null;
            foreach (var kv in BootKeys)
            {
                if (manufacturer.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            }
            return null;
        }
    }
}
