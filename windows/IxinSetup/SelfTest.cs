using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace IxinSetup
{
    /// <summary>
    /// Tests sur un vrai Windows (intégration continue) sans toucher au disque du PC : des disques
    /// virtuels VHDX reproduisent la disposition d'un PC, puis la préparation complète et
    /// l'annulation sont exécutées et vérifiées dessus, pour chaque emplacement de la partition EFI.
    /// </summary>
    internal static class SelfTest
    {
        private static int failures;

        private static void Expect(bool ok, string what)
        {
            Console.WriteLine((ok ? "  OK    " : "  ÉCHEC ") + what);
            Log.Info((ok ? "test réussi : " : "TEST ÉCHOUÉ : ") + what);
            if (!ok) failures++;
        }

        private enum EspLayout
        {
            SameDisk,    // partition EFI sur le disque de Windows (cas courant)
            OtherDisk,   // partition EFI sur un autre disque (PC installé avec deux disques branchés)
            None,        // aucune partition EFI utilisable : IXIN OS Setup en crée une
        }

        public static int Run(string workDir)
        {
            failures = 0;
            workDir = workDir ?? @"C:\ixin-selftest";
            Directory.CreateDirectory(workDir);
            Console.WriteLine("IXIN OS Setup " + Program.Version + " — auto-test (disques virtuels)");

            UnitTests();

            string iso = Path.Combine(workDir, "ixin-os-test.iso");
            var rnd = new byte[64 << 20];
            new Random(42).NextBytes(rnd);
            File.WriteAllBytes(iso, rnd);
            string isoSha = IsoSource.Sha256(iso, null, CancellationToken.None);

            foreach (EspLayout layout in Enum.GetValues(typeof(EspLayout)))
                Scenario(workDir, iso, isoSha, layout);

            Console.WriteLine(failures == 0 ? "AUTO-TEST RÉUSSI" : $"AUTO-TEST : {failures} échec(s)");
            return failures == 0 ? 0 : 1;
        }

        private static void Scenario(string workDir, string iso, string isoSha, EspLayout layout)
        {
            Console.WriteLine();
            Console.WriteLine("── " + (layout == EspLayout.SameDisk ? "partition EFI sur le disque de Windows"
                : layout == EspLayout.OtherDisk ? "partition EFI sur un autre disque"
                : "aucune partition EFI : création automatique"));
            string pcVhd = Path.Combine(workDir, "pc-test.vhdx");
            string espVhd = Path.Combine(workDir, "esp-test.vhdx");
            string esp = layout == EspLayout.None ? null : BootEntry.FreeDriveLetter();
            string win = NextFreeLetter(esp ?? BootEntry.FreeDriveLetter());
            try
            {
                CreateTestDisk(pcVhd, layout == EspLayout.SameDisk ? esp : null, win);
                if (layout == EspLayout.OtherDisk) CreateEspDisk(espVhd, esp);
                File.WriteAllText(win + @":\pagefile-test.txt", "fichier de Windows simulé");
                if (esp != null)
                {
                    Directory.CreateDirectory(esp + @":\EFI\Microsoft\Boot");
                    File.WriteAllBytes(esp + @":\EFI\Microsoft\Boot\bootmgfw.efi", new byte[4096]);
                }
                if (layout == EspLayout.SameDisk)
                    Expect(Storage.UnblockShrink(win, hibernation: false), "regroupement de l'espace libre (defrag /X)");

                var facts = Facts.Collect(win);
                // Seuls les disques virtuels comptent (pas la partition EFI de la machine de test) ; la
                // partition EFI simulée joue le rôle de celle qui démarre Windows.
                int espDisk = layout == EspLayout.SameDisk ? facts.DiskNumber
                    : layout == EspLayout.OtherDisk ? DiskOfVhd(espVhd) : -1;
                Expect(facts.DiskNumber == DiskOfVhd(pcVhd), "disque de « Windows » simulé reconnu");
                facts.Esps.RemoveAll(e => e.Disk != facts.DiskNumber && e.Disk != espDisk);
                foreach (var e in facts.Esps) e.IsSystem = e.Disk == espDisk;
                facts.IsUefi = true;
                Console.WriteLine(facts.Describe());
                Expect(facts.Problem == null, "lecture du disque virtuel");
                Expect(string.Equals(facts.PartitionStyle, "GPT", StringComparison.OrdinalIgnoreCase), "disque GPT détecté");

                var choice = Plan.ChooseEsp(facts);
                if (layout == EspLayout.SameDisk)
                    Expect(choice.Mode == EspMode.Windows && choice.Esp.Disk == facts.DiskNumber, "partition EFI de Windows trouvée sur son disque");
                else if (layout == EspLayout.OtherDisk)
                    Expect(choice.Mode == EspMode.Windows && choice.Esp.Disk == espDisk && espDisk != facts.DiskNumber,
                           $"partition EFI de Windows trouvée sur un autre disque (disque {espDisk})");
                else
                    Expect(choice.Mode == EspMode.Create, "aucune partition EFI : création prévue");
                var blocking = Plan.Checks(facts, null).Where(c => c.Level == CheckLevel.Blocking).Select(c => c.Title).ToList();
                Expect(blocking.Count == 0, "aucune vérification bloquante" + (blocking.Count > 0 ? " : " + string.Join(" ; ", blocking) : ""));

                long originalSize = facts.Size;
                long ixinSize = Plan.MinIxin;
                Expect(facts.MaxShrinkByFs >= ixinSize + Plan.SetupSizeFor(iso.Length), "réduction possible de « Windows »");

                // Autre disque : la partition est désignée comme dans les options avancées et montée par
                // son volume (sans « mountvol /S », réservé à la partition qui démarre vraiment le PC).
                var options = new SetupOptions();
                string testEsp = esp;
                if (layout == EspLayout.OtherDisk)
                {
                    options.Esp = choice.Esp.Guid;
                    foreach (var e in facts.Esps) e.IsSystem = false;
                    BootEntry.Unmount(esp);
                    testEsp = null;
                }
                var steps = new Progress<string>(s => Console.WriteLine("  → " + s));
                var st = Installer.Prepare(facts, ixinSize, iso, isoSha, options, steps, null, CancellationToken.None, testMode: true, testEspLetter: testEsp);

                var after = Facts.Collect(win);
                Expect(st.Completed, "préparation terminée");
                Expect(Math.Abs(originalSize - st.ShrunkBy - after.Size) < Storage.MiB, $"« Windows » réduit ({Facts.Gb(after.Size)})");
                var parts = Storage.FindSetupPartitions().Where(p => p.Disk == facts.DiskNumber).ToList();
                Expect(parts.Count == 1, "une partition IXIN-SETUP");
                PartitionRef setup = parts.Count == 1 ? parts[0] : null;
                if (setup != null)
                {
                    Expect(setup.Offset == Storage.AlignUp(after.Offset + after.Size, Storage.MiB), "IXIN-SETUP placée juste après « Windows »");
                    Expect(setup.Size >= Plan.SetupSizeFor(iso.Length) - Storage.MiB, $"taille d'IXIN-SETUP ({Facts.Gb(setup.Size)})");
                    Expect(string.IsNullOrEmpty(setup.DriveLetter), "IXIN-SETUP sans lettre de lecteur");
                    string letter = Storage.AssignLetter(setup);
                    try
                    {
                        Expect(IsoSource.Sha256(letter + @":\ixin-os.iso", null, CancellationToken.None) == isoSha, "ISO copiée intacte");
                        Expect(File.Exists(letter + @":\LISEZMOI.txt"), "fichier LISEZMOI présent");
                    }
                    finally
                    {
                        Storage.RemoveDriveLetter(new PartitionRef { Disk = setup.Disk, Number = setup.Number, DriveLetter = letter });
                    }
                }

                // chargeur et options dans la bonne partition EFI
                var created = EspPartitions(facts.DiskNumber).Where(p => p.Number != (choice.Esp?.Number ?? -1) || p.Disk != choice.Esp?.Disk).ToList();
                string expectedGuid = layout == EspLayout.None ? created.FirstOrDefault()?.Guid : choice.Esp.Guid;
                if (layout == EspLayout.None)
                {
                    Expect(st.EspCreated && created.Count == 1, "partition EFI dédiée créée sur le disque de Windows");
                    if (created.Count == 1 && setup != null)
                        Expect(created[0].Offset == Storage.AlignUp(setup.Offset + setup.Size, Storage.MiB)
                               && Math.Abs(created[0].Size - Plan.NewEspSize) < Storage.MiB, "partition EFI dédiée placée après IXIN-SETUP (300 Mo)");
                }
                else
                {
                    Expect(!st.EspCreated && created.Count == 0, "aucune partition EFI créée sur le disque de Windows");
                }
                string espLetter = esp;
                bool mounted = false;
                if (layout != EspLayout.SameDisk)
                {
                    var target = layout == EspLayout.None ? created.FirstOrDefault() : new PartitionRef { Disk = choice.Esp.Disk, Number = choice.Esp.Number };
                    if (target != null)
                    {
                        espLetter = Storage.MountEsp(target.Disk, target.Number, layout == EspLayout.OtherDisk ? choice.Esp.VolumePath : null);
                        mounted = true;
                    }
                }
                try
                {
                    string loader = espLetter + @":\EFI\IXIN-SETUP\grubx64.efi";
                    Expect(espLetter != null && File.Exists(loader) && SameBytes(File.ReadAllBytes(loader), BootEntry.Loader()), "chargeur GRUB dans la partition EFI choisie");
                    string optionsFile = espLetter + @":\EFI\IXIN-SETUP\" + BootEntry.OptionsFile;
                    string optionsText = File.Exists(optionsFile) ? File.ReadAllText(optionsFile) : "";
                    string partuuid = (expectedGuid ?? "?").Trim('{', '}').ToLowerInvariant();
                    Expect(optionsText.Contains("ixin.esp=" + partuuid), "options.cfg désigne cette partition EFI (ixin.esp=" + partuuid + ")");
                    if (layout != EspLayout.None)
                        Expect(File.Exists(espLetter + @":\EFI\Microsoft\Boot\bootmgfw.efi"), "chargeur de Windows intact");
                }
                finally
                {
                    if (mounted) BootEntry.Unmount(espLetter);
                }

                long freeAfter = LargestFree(facts.DiskNumber);
                long expectedFree = ixinSize - (layout == EspLayout.None ? Plan.NewEspSize : 0) - Storage.MiB;
                Expect(freeAfter >= expectedFree, $"espace libre pour IXIN OS ({Facts.Gb(freeAfter)})");
                Expect(File.ReadAllText(win + @":\pagefile-test.txt") == "fichier de Windows simulé", "fichiers de « Windows » intacts");

                Installer.Undo(SetupState.Load(), steps, testMode: true, testEspLetter: testEsp);
                var restored = Facts.Collect(win);
                Expect(!Storage.FindSetupPartitions().Any(p => p.Disk == facts.DiskNumber), "IXIN-SETUP supprimée");
                if (layout == EspLayout.None)
                    Expect(EspPartitions(facts.DiskNumber).Count == 0, "partition EFI dédiée supprimée");
                Expect(restored.Size >= originalSize - 16 * Storage.MiB, $"taille d'origine rendue à « Windows » ({Facts.Gb(restored.Size)})");
                if (layout != EspLayout.None)
                {
                    string check = esp;
                    bool remount = layout == EspLayout.OtherDisk;
                    if (remount) check = Storage.MountEsp(choice.Esp.Disk, choice.Esp.Number, choice.Esp.VolumePath);
                    try
                    {
                        Expect(!Directory.Exists(check + @":\EFI\IXIN-SETUP"), "chargeur retiré de la partition EFI");
                        Expect(File.Exists(check + @":\EFI\Microsoft\Boot\bootmgfw.efi"), "chargeur de Windows toujours là");
                    }
                    finally
                    {
                        if (remount) BootEntry.Unmount(check);
                    }
                }
                Expect(SetupState.Load() == null, "fichier d'état supprimé");
                Expect(File.ReadAllText(win + @":\pagefile-test.txt") == "fichier de Windows simulé", "fichiers de « Windows » toujours intacts");
            }
            catch (Exception ex)
            {
                Expect(false, "exception : " + ex);
                // Remise en état seulement si une préparation a commencé sur le disque virtuel
                var started = SetupState.Load();
                if (started != null && started.DiskNumber >= 0 && started.DriveLetter == win)
                {
                    try { Installer.Undo(started, null, testMode: true, testEspLetter: null); } catch (Exception) { }
                }
                SetupState.Delete();
            }
            finally
            {
                DetachTestDisk(pcVhd);
                DetachTestDisk(espVhd);
            }
        }

        /// <summary>Crée puis supprime une entrée « IXIN OS Setup » du firmware (machine jetable).</summary>
        public static int RunBootEntry()
        {
            failures = 0;
            if (!Native.IsUefi())
            {
                Console.WriteLine("Démarrage BIOS : test de l'entrée de démarrage ignoré");
                return 0;
            }
            string id = null;
            try
            {
                id = BootEntry.Create();
                Expect(BootEntry.Find().Contains(id), "entrée de démarrage créée et retrouvée");
            }
            catch (Exception ex)
            {
                Expect(false, "création de l'entrée de démarrage : " + ex.Message);
            }
            finally
            {
                if (id != null) BootEntry.Delete(id);
            }
            Expect(BootEntry.Find().Count == 0, "entrée de démarrage supprimée");

            // IXIN OS installé : son entrée est retrouvée et peut être remise en tête de l'ordre de démarrage
            string ixin = null;
            try
            {
                ixin = BootEntry.Create("IXIN (test)", BootEntry.IxinLoaderPath);
                Expect(BootEntry.FindIxin().Contains(ixin), "entrée d'IXIN OS installé retrouvée");
                Expect(!string.Equals(BootEntry.FirstInBootOrder(), ixin, StringComparison.OrdinalIgnoreCase), "entrée d'IXIN OS ajoutée en fin de liste");
                BootEntry.PutFirst(ixin);
                Expect(string.Equals(BootEntry.FirstInBootOrder(), ixin, StringComparison.OrdinalIgnoreCase), "menu IXIN remis en premier au démarrage");
            }
            catch (Exception ex)
            {
                Expect(false, "entrée d'IXIN OS installé : " + ex.Message);
            }
            finally
            {
                if (ixin != null) BootEntry.Delete(ixin);
            }
            Expect(BootEntry.FindIxin().Count == 0, "entrée d'IXIN OS de test supprimée");

            // Entrée vers une autre partition EFI (partition dédiée, ou choisie dans les options avancées)
            string vhd = Path.Combine(Path.GetTempPath(), "ixin-boot-test.vhdx");
            string letter = BootEntry.FreeDriveLetter();
            string other = null;
            try
            {
                CreateEspDisk(vhd, letter);
                other = BootEntry.Create("IXIN OS Setup (test)", BootEntry.LoaderPath, letter);
                Expect(BootEntry.Find().Contains(other), "entrée vers une partition EFI dédiée créée");
                string shown = BootEntry.Run("bcdedit.exe", $"/enum {other} /v", check: false);
                Console.WriteLine(shown.Trim());
                // Sur un vrai disque : « partition=Z: » ; sur ce disque virtuel, Windows note « vhd=[C:]\…\ixin-boot-test.vhdx »
                Expect(shown.IndexOf("partition=" + letter + ":", StringComparison.OrdinalIgnoreCase) >= 0
                       || shown.IndexOf(Path.GetFileName(vhd), StringComparison.OrdinalIgnoreCase) >= 0,
                       "entrée dirigée vers la partition EFI indiquée (" + letter + ":)");
            }
            catch (Exception ex)
            {
                Expect(false, "entrée vers une partition EFI dédiée : " + ex.Message);
            }
            finally
            {
                if (other != null) BootEntry.Delete(other);
                DetachTestDisk(vhd);
            }
            Expect(BootEntry.Find().Count == 0, "entrée de test supprimée");
            Console.WriteLine(failures == 0 ? "TEST DE L'ENTRÉE DE DÉMARRAGE RÉUSSI" : $"{failures} échec(s)");
            return failures == 0 ? 0 : 1;
        }

        // ── tests sans disque ────────────────────────────────────────────────

        private static void UnitTests()
        {
            const string bcdFr = "Gestionnaire de démarrage du microprogramme\r\n---\r\nidentificateur          {fwbootmgr}\r\n" +
                "displayorder            {11111111-2222-3333-4444-555555555555}\r\n                        {aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}\r\n\r\n" +
                "Gestionnaire de démarrage Windows\r\n---\r\nidentificateur          {11111111-2222-3333-4444-555555555555}\r\n" +
                "path                    \\EFI\\Microsoft\\Boot\\bootmgfw.efi\r\ndescription             Windows Boot Manager\r\n\r\n" +
                "Application du microprogramme (101fffff)\r\n---\r\nidentificateur          {aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}\r\n" +
                "path                    \\EFI\\IXIN-SETUP\\grubx64.efi\r\ndescription             IXIN OS Setup\r\n";
            var ids = BootEntry.ParseEntries(bcdFr);
            Expect(ids.Count == 1 && ids[0] == "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}", "lecture de la sortie de bcdedit");
            Expect(BootEntry.ParseEntries(bcdFr, BootEntry.IxinLoaderPath).Count == 0, "IXIN-SETUP n'est pas pris pour IXIN OS installé");

            const string release = "{\"tag_name\":\"build-2026.10.15-2\",\"assets\":[" +
                "{\"name\":\"ixin-os-2026.10.15-x86_64.iso.part01\",\"size\":20,\"browser_download_url\":\"https://x/p1\"}," +
                "{\"name\":\"ixin-os-2026.10.15-x86_64.iso.sha256\",\"size\":96,\"browser_download_url\":\"https://x/sum\"}," +
                "{\"name\":\"ixin-os-2026.10.15-x86_64.iso.part00\",\"size\":30,\"browser_download_url\":\"https://x/p0\"}," +
                "{\"name\":\"IXIN-OS-Setup.exe\",\"size\":5,\"browser_download_url\":\"https://x/exe\"}]}";
            var r = IsoSource.Parse(release);
            Expect(r.IsoName == "ixin-os-2026.10.15-x86_64.iso" && r.Parts.Count == 2 && r.Parts[0].Url == "https://x/p0"
                   && r.IsoSize == 50 && r.Checksum?.Url == "https://x/sum", "lecture de la version publiée");
            Expect(IsoSource.ParseChecksum("7562c75ad8eb4af29cb1490edfa5f1c296e4eba1247754ee932ceaccadb70d8f  ixin-os-2026.10.15-x86_64.iso\n",
                       "ixin-os-2026.10.15-x86_64.iso") == "7562c75ad8eb4af29cb1490edfa5f1c296e4eba1247754ee932ceaccadb70d8f",
                   "lecture du fichier .sha256");
            Expect(Plan.SetupSizeFor(5079388160) == 6 * Plan.GiB, "taille de la partition d'installation");

            EspInfo Esp(int disk, bool system, long freeMb = 70, bool usb = false, string guid = "{0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0}") =>
                new EspInfo { Disk = disk, Number = 1, Size = 100 * Storage.MiB, Free = freeMb * Storage.MiB, IsSystem = system,
                              Removable = usb, EspType = true, FileSystem = "FAT32", Guid = guid };
            Facts Pc(params EspInfo[] esps) => new Facts
            {
                IsUefi = true, DiskNumber = 0, PartitionStyle = "GPT", DiskName = "SSD", DiskSize = 500 * Plan.GiB,
                Size = 400 * Plan.GiB, Free = 300 * Plan.GiB, SizeMin = 100 * Plan.GiB, Esps = esps.ToList(),
            };
            var c = Plan.ChooseEsp(Pc(Esp(0, true)));
            Expect(c.Mode == EspMode.Windows && c.Esp.Disk == 0, "EFI : partition de Windows sur son disque");
            c = Plan.ChooseEsp(Pc(Esp(1, true)));
            Expect(c.Mode == EspMode.Windows && c.Esp.Disk == 1, "EFI : partition de Windows sur un autre disque, utilisée");
            Expect(Plan.ChooseEsp(Pc(Esp(0, true, freeMb: 5))).Mode == EspMode.Create, "EFI : partition de Windows pleine → partition dédiée");
            Expect(Plan.ChooseEsp(Pc(Esp(1, true, usb: true))).Mode == EspMode.Create, "EFI : partition de Windows sur un disque USB → partition dédiée");
            Expect(Plan.ChooseEsp(Pc()).Mode == EspMode.Create, "EFI : aucune partition → partition dédiée");
            Expect(Plan.ChooseEsp(Pc(Esp(0, false))).Mode == EspMode.Existing, "EFI : partition de Windows non signalée, seule sur son disque");
            Expect(Plan.ChooseEsp(Pc(Esp(0, true)), SetupOptions.NewEsp).Mode == EspMode.Create, "EFI : partition dédiée demandée");
            var other = Esp(1, false, guid: "{11111111-2222-3333-4444-555555555555}");
            c = Plan.ChooseEsp(Pc(Esp(0, true), other), other.Guid);
            Expect(c.Mode == EspMode.Existing && c.Esp == other, "EFI : autre partition choisie dans les options avancées");
            var checks = Plan.Checks(Pc(Esp(1, true)), null);
            Expect(!checks.Any(x => x.Level == CheckLevel.Blocking) && checks.Any(x => x.Level == CheckLevel.Info && x.Title.Contains("autre disque")),
                   "vérifications : partition EFI sur un autre disque signalée, non bloquante");
            checks = Plan.Checks(Pc(), null);
            Expect(!checks.Any(x => x.Level == CheckLevel.Blocking) && checks.Any(x => x.Level == CheckLevel.Info && x.Title.Contains("sera créée")),
                   "vérifications : aucune partition EFI, création annoncée, non bloquante");
            string cfg = BootEntry.OptionsContent("{0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0}", true);
            Expect(cfg.Contains("set ixin_options=\"ixin.esp=0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0 ixin.keepsetup=1\"") && cfg.All(ch => ch < 128),
                   "options.cfg (ASCII, ixin.esp, ixin.keepsetup)");
            Expect(BootEntry.OptionsContent("pas un guid", false).Contains("set ixin_options=\"\""), "options.cfg sans identifiant invalide");
            Expect(BootEntry.Loader().Length > 100000, "chargeur GRUB intégré au programme");
        }

        // ── disque virtuel ───────────────────────────────────────────────────

        private static void CreateTestDisk(string vhd, string esp, string win)
        {
            DetachTestDisk(vhd);
            string efi = esp == null ? "" : $"create partition efi size=100\nformat quick fs=fat32 label=\"SYSTEM\"\nassign letter={esp}\n";
            Diskpart($@"create vdisk file=""{vhd}"" maximum=81920 type=expandable
select vdisk file=""{vhd}""
attach vdisk
convert gpt
{efi}create partition msr size=16
create partition primary
format quick fs=ntfs label=""Windows""
assign letter={win}
");
            Console.WriteLine($"Disque virtuel prêt : « Windows » {win}:" + (esp == null ? ", sans partition EFI" : $", EFI {esp}:"));
        }

        /// <summary>Second disque : sa partition EFI démarre Windows, le reste contient des données.</summary>
        private static void CreateEspDisk(string vhd, string esp)
        {
            DetachTestDisk(vhd);
            Diskpart($@"create vdisk file=""{vhd}"" maximum=1024 type=expandable
select vdisk file=""{vhd}""
attach vdisk
convert gpt
create partition efi size=100
format quick fs=fat32 label=""SYSTEM""
assign letter={esp}
create partition primary
format quick fs=ntfs label=""Donnees""
");
            Console.WriteLine($"Second disque virtuel prêt : EFI {esp}:");
        }

        private static void DetachTestDisk(string vhd)
        {
            if (!File.Exists(vhd)) return;
            Diskpart($"select vdisk file=\"{vhd}\"\ndetach vdisk\n", check: false);
            try { File.Delete(vhd); } catch (IOException) { }
        }

        private static void Diskpart(string script, bool check = true)
        {
            Console.WriteLine(Storage.Diskpart(script, check).Trim());
        }

        private static string NextFreeLetter(string taken)
        {
            for (char c = (char)(taken[0] - 1); c >= 'G'; c--)
            {
                if (!DriveInfo.GetDrives().Any(d => char.ToUpperInvariant(d.Name[0]) == c)) return c.ToString();
            }
            throw new SetupException("Pas de lettre de lecteur libre pour le test");
        }

        /// <summary>Numéro du disque d'un disque virtuel attaché (la lettre d'une partition EFI n'est pas visible par Get-Partition).</summary>
        private static int DiskOfVhd(string vhd)
        {
            var o = Ps.One(@"param([string]$Path) [pscustomobject]@{ Disk = [int](Get-DiskImage -ImagePath $Path | Get-Disk).Number }",
                new Hashtable { ["Path"] = vhd });
            return (int)Ps.Long(o, "Disk");
        }

        /// <summary>Partitions EFI (type GPT) d'un disque.</summary>
        private static List<PartitionRef> EspPartitions(int disk)
        {
            var list = new List<PartitionRef>();
            foreach (var o in Ps.Run(@"
param([int]$Disk, [string]$Type)
Update-HostStorageCache -ErrorAction Ignore
foreach ($p in @(Get-Partition -DiskNumber $Disk | Where-Object { $_.GptType -eq $Type })) {
    [pscustomobject]@{ Number = [int]$p.PartitionNumber; Offset = [long]$p.Offset; Size = [long]$p.Size; Guid = [string]$p.Guid }
}", new Hashtable { ["Disk"] = disk, ["Type"] = Storage.EspGptType }))
            {
                list.Add(new PartitionRef { Disk = disk, Number = (int)Ps.Long(o, "Number"), Offset = Ps.Long(o, "Offset"), Size = Ps.Long(o, "Size"), Guid = Ps.Str(o, "Guid") });
            }
            return list;
        }

        private static long LargestFree(int disk)
        {
            var o = Ps.One(@"param([int]$Disk) [pscustomobject]@{ Free = [long](Get-Disk -Number $Disk).LargestFreeExtent }",
                new Hashtable { ["Disk"] = disk });
            return Ps.Long(o, "Free");
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b));
            }
        }
    }
}
