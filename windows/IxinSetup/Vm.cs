using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace IxinSetup
{
    /// <summary>Ce que le PC offre pour faire tourner IXIN OS dans une machine virtuelle.</summary>
    internal sealed class VmFacts
    {
        public long Ram;                    // mémoire du PC (octets)
        public int Cores = Environment.ProcessorCount;
        public bool VirtualizationEnabled;  // Intel VT-x / AMD-V activé dans le firmware (ou hyperviseur déjà actif)
        public bool HypervisorPresent;      // hyperviseur de Windows déjà en marche
        public string PlatformState = "";   // « Plateforme de l'hyperviseur Windows » : Enabled, Disabled, (vide : inconnu)
        public long Free;                   // espace libre du lecteur de la machine virtuelle

        public bool PlatformEnabled => string.Equals(PlatformState, "Enabled", StringComparison.OrdinalIgnoreCase);
        public bool Accelerated => PlatformEnabled && (VirtualizationEnabled || HypervisorPresent);

        public string Describe() =>
            $"RAM {Facts.Gb(Ram)}, {Cores} processeurs logiques, virtualisation {(VirtualizationEnabled ? "activée" : "désactivée")}, " +
            $"hyperviseur {(HypervisorPresent ? "actif" : "absent")}, plateforme de l'hyperviseur « {PlatformState} », libre {Facts.Gb(Free)}";
    }

    /// <summary>
    /// Machine virtuelle IXIN : IXIN OS dans une fenêtre de Windows, sans toucher au disque du PC.
    /// Le moteur est QEMU (ixin-vm-engine-win64.zip, publié avec chaque version), accéléré par
    /// l'hyperviseur de Windows (WHPX). Tout est rangé dans un seul dossier de l'utilisateur ;
    /// « Supprimer la machine virtuelle » efface ce dossier et les raccourcis, rien d'autre.
    /// </summary>
    internal sealed class Vm
    {
        public const string EngineAsset = "ixin-vm-engine-win64.zip";
        public const long MinRam = 4L * Facts.GiB;
        public const long DiskSize = 120L * Facts.GiB;      // taille maximale ; le fichier ne grossit qu'avec les données
        public const long FreeNeeded = 45L * Facts.GiB;     // ISO + moteur + IXIN OS installé + marge
        private const string ShortcutName = "IXIN OS.lnk";

        public readonly string Folder;

        public Vm(string folder = null)
        {
            Folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IXIN OS (machine virtuelle)");
        }

        public string EngineDir => Path.Combine(Folder, "moteur");
        public string Qemu => Path.Combine(EngineDir, "qemu-system-x86_64w.exe");
        public string QemuConsole => Path.Combine(EngineDir, "qemu-system-x86_64.exe");
        public string QemuImg => Path.Combine(EngineDir, "qemu-img.exe");
        public string Disk => Path.Combine(Folder, "ixin-os.qcow2");
        public string Vars => Path.Combine(Folder, "uefi-vars.fd");
        public string Iso => Path.Combine(Folder, "ixin-os.iso");
        public string Icon => Path.Combine(Folder, "ixin.ico");
        public bool Exists => File.Exists(Disk) && File.Exists(Qemu);
        public static string PendingFlag => Path.Combine(Log.Directory, "vm-en-attente");

        // ── état du PC ───────────────────────────────────────────────────────

        public static VmFacts Collect(string folder)
        {
            var f = new VmFacts();
            try
            {
                var o = Ps.One(@"
$p = Get-CimInstance Win32_Processor | Select-Object -First 1
$c = Get-CimInstance Win32_ComputerSystem
$w = Get-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -ErrorAction SilentlyContinue
[pscustomobject]@{ Virt = [bool]$p.VirtualizationFirmwareEnabled; Hyper = [bool]$c.HypervisorPresent
                   Ram = [long]$c.TotalPhysicalMemory; Platform = [string]$w.State }");
                f.VirtualizationEnabled = Ps.Bool(o, "Virt");
                f.HypervisorPresent = Ps.Bool(o, "Hyper");
                f.Ram = Ps.Long(o, "Ram");
                f.PlatformState = Ps.Str(o, "Platform");
            }
            catch (SetupException ex)
            {
                Log.Warn("Lecture des capacités de virtualisation : " + ex.Message);
            }
            try
            {
                f.Free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Espace libre illisible : " + ex.Message);
            }
            Log.Info("Machine virtuelle : " + f.Describe());
            return f;
        }

        public static List<Check> Checks(VmFacts f)
        {
            var list = new List<Check>();
            if (f.Ram > 0 && f.Ram < MinRam)
                list.Add(new Check(CheckLevel.Blocking, $"Mémoire insuffisante ({Facts.Gb(f.Ram)})",
                    "Il faut au moins 4 Go de mémoire (8 Go conseillés) : Windows et IXIN OS tournent en même temps. " +
                    "Installez plutôt IXIN OS à côté de Windows."));
            else if (f.Ram > 0)
                list.Add(new Check(f.Ram < 8 * Facts.GiB ? CheckLevel.Warning : CheckLevel.Ok, $"Mémoire : {Facts.Gb(f.Ram)}",
                    $"IXIN OS en recevra {MemoryMb(f.Ram) / 1024} Go" + (f.Ram < 8 * Facts.GiB ? " : ça fonctionne, mais lentement avec beaucoup d'applications." : ".")));
            if (f.Free > 0 && f.Free < FreeNeeded)
                list.Add(new Check(CheckLevel.Blocking, "Espace libre insuffisant",
                    $"Il faut environ {Facts.Gb(FreeNeeded)} libres sur le disque ({Facts.Gb(f.Free)} actuellement)."));
            else if (f.Free > 0)
                list.Add(new Check(CheckLevel.Ok, "Espace disponible", $"{Facts.Gb(f.Free)} libres (environ 45 Go utilisés au fil du temps)"));
            if (!f.VirtualizationEnabled && !f.HypervisorPresent)
                list.Add(new Check(CheckLevel.Warning, "Virtualisation désactivée dans les paramètres du PC",
                    "Sans elle, IXIN OS fonctionne mais très lentement. Activez « Intel VT-x » / « Intel Virtualization Technology » " +
                    "ou « AMD-V » / « SVM Mode » dans les paramètres UEFI du PC (onglet Advanced ou CPU), puis relancez IXIN OS Setup."));
            else if (!f.PlatformEnabled)
                list.Add(new Check(CheckLevel.Info, "Accélération à activer",
                    "IXIN OS Setup va activer la « Plateforme de l'hyperviseur Windows » (composant de Windows) : un redémarrage " +
                    "du PC sera demandé, puis IXIN OS Setup se rouvrira tout seul."));
            else
                list.Add(new Check(CheckLevel.Ok, "Accélération matérielle disponible", "Plateforme de l'hyperviseur Windows activée"));
            return list;
        }

        /// <summary>Active la « Plateforme de l'hyperviseur Windows » (WHPX). Vrai si un redémarrage est nécessaire.</summary>
        public static bool EnablePlatform()
        {
            var o = Ps.One(@"
$r = Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart
[pscustomobject]@{ Restart = [bool]$r.RestartNeeded }");
            bool restart = Ps.Bool(o, "Restart");
            Log.Info("Plateforme de l'hyperviseur Windows activée" + (restart ? " (redémarrage nécessaire)" : ""));
            return restart;
        }

        // ── réglages du PC virtuel ───────────────────────────────────────────

        /// <summary>Mémoire donnée à IXIN OS : la moitié du PC, entre 2 et 8 Go.</summary>
        public static long MemoryMb(long ram) => Math.Max(2048, Math.Min(8192, ram / 2 / (1024 * 1024) / 512 * 512));

        public static int Processors(int cores) => Math.Max(2, Math.Min(8, cores / 2));

        private static string Q(string path) => "\"" + path + "\"";

        /// <summary>Ligne de commande de QEMU (aussi celle du raccourci « IXIN OS »).</summary>
        public string Arguments(long ram, int cores, bool headless = false, string serialLog = null)
        {
            var a = new StringBuilder();
            a.Append("-name \"IXIN OS\" -machine q35");
            // Hyperviseur de Windows si disponible, sinon émulation (lente)
            a.Append(" -accel whpx,kernel-irqchip=off -accel tcg");
            a.Append($" -m {MemoryMb(ram)} -smp {Processors(cores)}");
            a.Append($" -drive if=pflash,format=raw,readonly=on,file={Q(Path.Combine(EngineDir, "share", "edk2-x86_64-code.fd"))}");
            a.Append($" -drive if=pflash,format=raw,file={Q(Vars)}");
            // Disque d'IXIN OS d'abord ; l'ISO ne sert que tant qu'IXIN OS n'est pas installé
            a.Append($" -drive file={Q(Disk)},if=none,id=hd0,format=qcow2,cache=writeback,discard=unmap");
            a.Append(" -device virtio-blk-pci,drive=hd0,bootindex=1");
            if (File.Exists(Iso))
            {
                a.Append($" -drive file={Q(Iso)},if=none,id=cd0,media=cdrom,readonly=on");
                a.Append(" -device ide-cd,drive=cd0,bootindex=2");
            }
            a.Append(" -nic user,model=virtio-net-pci -device virtio-rng-pci");
            a.Append(" -device qemu-xhci -device usb-tablet");
            if (headless)
            {
                a.Append(" -display none -vga none");
                if (serialLog != null) a.Append($" -serial file:{Q(serialLog)}");
            }
            else
            {
                a.Append(" -device virtio-vga -display sdl,window-close=on");
                a.Append(" -audiodev dsound,id=son -device intel-hda -device hda-duplex,audiodev=son");
            }
            return a.ToString();
        }

        // ── installation ─────────────────────────────────────────────────────

        public void InstallEngine(string zip)
        {
            Directory.CreateDirectory(Folder);
            if (Directory.Exists(EngineDir)) Directory.Delete(EngineDir, true);
            string tmp = Path.Combine(Folder, "moteur-extraction");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            ZipFile.ExtractToDirectory(zip, tmp);
            // L'archive contient un dossier ixin-vm-engine/
            string inner = Path.Combine(tmp, "ixin-vm-engine");
            Directory.Move(Directory.Exists(inner) ? inner : tmp, EngineDir);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            if (!File.Exists(Qemu) || !File.Exists(QemuImg))
                throw new SetupException("Le moteur de la machine virtuelle est incomplet.");
            Log.Info("Moteur de la machine virtuelle installé : " + EngineDir);
        }

        public void CreateDisk()
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(Vars))
                File.Copy(Path.Combine(EngineDir, "share", "edk2-i386-vars.fd"), Vars);
            if (File.Exists(Disk)) return;
            Run(QemuImg, $"create -f qcow2 {Q(Disk)} {DiskSize / Facts.GiB}G");
            Log.Info($"Disque virtuel créé : {Disk} ({Facts.Gb(DiskSize)} au plus)");
        }

        public void PlaceIso(string iso)
        {
            Directory.CreateDirectory(Folder);
            if (File.Exists(Iso)) File.Delete(Iso);
            File.Copy(iso, Iso);
        }

        public void CreateShortcuts(long ram, int cores, string desktop = null, string startMenu = null)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ixin.ico"))
            using (var f = File.Create(Icon))
                s?.CopyTo(f);
            desktop = desktop ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            startMenu = startMenu ?? Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            foreach (var dir in new[] { desktop, startMenu })
            {
                Ps.Run(@"
param([string]$Path, [string]$Target, [string]$Arguments, [string]$Folder, [string]$Icon)
$s = (New-Object -ComObject WScript.Shell).CreateShortcut($Path)
$s.TargetPath = $Target; $s.Arguments = $Arguments; $s.WorkingDirectory = $Folder
$s.IconLocation = $Icon; $s.Description = 'IXIN OS dans une fenêtre (machine virtuelle IXIN)'
$s.Save()", new Hashtable
                {
                    ["Path"] = Path.Combine(dir, ShortcutName),
                    ["Target"] = Qemu,
                    ["Arguments"] = Arguments(ram, cores),
                    ["Folder"] = Folder,
                    ["Icon"] = Icon,
                });
            }
            Log.Info("Raccourcis « IXIN OS » créés (bureau, menu Démarrer)");
        }

        public string DesktopShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);

        /// <summary>Lance IXIN OS par son raccourci, avec les droits normaux de l'utilisateur (pas ceux d'administrateur).</summary>
        public void Launch()
        {
            Process.Start(new ProcessStartInfo("explorer.exe", Q(DesktopShortcut)) { UseShellExecute = false });
        }

        public void Remove()
        {
            foreach (var p in Process.GetProcessesByName("qemu-system-x86_64w").Concat(Process.GetProcessesByName("qemu-system-x86_64")))
            {
                try
                {
                    if (p.MainModule?.FileName?.StartsWith(EngineDir, StringComparison.OrdinalIgnoreCase) == true)
                        throw new SetupException("IXIN OS est en cours d'utilisation : éteignez-le d'abord (menu IXIN → Éteindre).");
                }
                catch (System.ComponentModel.Win32Exception) { }
            }
            foreach (var lnk in new[] { DesktopShortcut, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName) })
                if (File.Exists(lnk)) File.Delete(lnk);
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Log.Info("Machine virtuelle IXIN supprimée");
        }

        private static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd() + err.Result;
                p.WaitForExit();
                Log.Info($"{Path.GetFileName(exe)} {args} → {p.ExitCode}");
                if (p.ExitCode != 0) throw new SetupException($"{Path.GetFileName(exe)} : {output.Trim()}");
                return output;
            }
        }

        /// <summary>Démarre le PC virtuel sans fenêtre (tests) ; renvoie le processus.</summary>
        public Process StartHeadless(long ram, int cores, string serialLog)
        {
            var psi = new ProcessStartInfo(QemuConsole, Arguments(ram, cores, true, serialLog)) { UseShellExecute = false, CreateNoWindow = true };
            return Process.Start(psi);
        }
    }
}
