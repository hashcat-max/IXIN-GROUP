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
    /// Tests sur un vrai Windows (intégration continue) sans toucher au disque du PC : un disque
    /// virtuel VHDX reproduit la disposition d'un PC (EFI, MSR, NTFS « Windows »), puis la
    /// préparation complète et l'annulation sont exécutées et vérifiées dessus.
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

        public static int Run(string workDir)
        {
            failures = 0;
            workDir = workDir ?? @"C:\ixin-selftest";
            Directory.CreateDirectory(workDir);
            Console.WriteLine("IXIN OS Setup " + Program.Version + " — auto-test (disque virtuel)");

            UnitTests();

            string vhd = Path.Combine(workDir, "pc-test.vhdx");
            string esp = BootEntry.FreeDriveLetter();
            string win = NextFreeLetter(esp);
            try
            {
                CreateTestDisk(vhd, esp, win);
                File.WriteAllText(win + @":\pagefile-test.txt", "fichier de Windows simulé");
                Directory.CreateDirectory(esp + @":\EFI\Microsoft\Boot");
                File.WriteAllBytes(esp + @":\EFI\Microsoft\Boot\bootmgfw.efi", new byte[4096]);

                string iso = Path.Combine(workDir, "ixin-os-test.iso");
                var rnd = new byte[64 << 20];
                new Random(42).NextBytes(rnd);
                File.WriteAllBytes(iso, rnd);
                string isoSha = IsoSource.Sha256(iso, null, CancellationToken.None);

                var facts = Facts.Collect(win);
                Console.WriteLine(facts.Describe());
                Expect(facts.Problem == null, "lecture du disque virtuel");
                Expect(string.Equals(facts.PartitionStyle, "GPT", StringComparison.OrdinalIgnoreCase), "disque GPT détecté");
                Expect(facts.EspPartitionNumber > 0, "partition EFI détectée");
                long originalSize = facts.Size;
                long ixinSize = Plan.MinIxin;
                Expect(facts.MaxShrinkByFs >= ixinSize + Plan.SetupSizeFor(iso.Length), "réduction possible de « Windows »");

                var steps = new Progress<string>(s => Console.WriteLine("  → " + s));
                var st = Installer.Prepare(facts, ixinSize, iso, isoSha, steps, null, CancellationToken.None, testMode: true, testEspLetter: esp);

                var after = Facts.Collect(win);
                Expect(st.Completed, "préparation terminée");
                Expect(Math.Abs(originalSize - st.ShrunkBy - after.Size) < Storage.MiB, $"« Windows » réduit ({Facts.Gb(after.Size)})");
                var parts = Storage.FindSetupPartitions().Where(p => p.Disk == facts.DiskNumber).ToList();
                Expect(parts.Count == 1, "une partition IXIN-SETUP");
                if (parts.Count == 1)
                {
                    var p = parts[0];
                    Expect(p.Offset == Storage.AlignUp(after.Offset + after.Size, Storage.MiB), "IXIN-SETUP placée juste après « Windows »");
                    Expect(p.Size >= Plan.SetupSizeFor(iso.Length) - Storage.MiB, $"taille d'IXIN-SETUP ({Facts.Gb(p.Size)})");
                    Expect(string.IsNullOrEmpty(p.DriveLetter), "IXIN-SETUP sans lettre de lecteur");
                    string letter = MountTemporarily(p);
                    try
                    {
                        Expect(IsoSource.Sha256(letter + @":\ixin-os.iso", null, CancellationToken.None) == isoSha, "ISO copiée intacte");
                        Expect(File.Exists(letter + @":\LISEZMOI.txt"), "fichier LISEZMOI présent");
                    }
                    finally
                    {
                        Storage.RemoveDriveLetter(new PartitionRef { Disk = p.Disk, Number = p.Number, DriveLetter = letter });
                    }
                }
                string loader = esp + @":\EFI\IXIN-SETUP\grubx64.efi";
                Expect(File.Exists(loader) && SameBytes(File.ReadAllBytes(loader), BootEntry.Loader()), "chargeur GRUB dans la partition EFI");
                Expect(File.Exists(esp + @":\EFI\Microsoft\Boot\bootmgfw.efi"), "chargeur de Windows intact");
                long freeAfter = LargestFree(facts.DiskNumber);
                Expect(freeAfter >= ixinSize - Storage.MiB, $"espace libre pour IXIN OS ({Facts.Gb(freeAfter)})");
                Expect(File.ReadAllText(win + @":\pagefile-test.txt") == "fichier de Windows simulé", "fichiers de « Windows » intacts");

                Installer.Undo(SetupState.Load(), steps, testMode: true, testEspLetter: esp);
                var restored = Facts.Collect(win);
                Expect(!Storage.FindSetupPartitions().Any(p => p.Disk == facts.DiskNumber), "IXIN-SETUP supprimée");
                Expect(restored.Size >= originalSize - 16 * Storage.MiB, $"taille d'origine rendue à « Windows » ({Facts.Gb(restored.Size)})");
                Expect(!Directory.Exists(esp + @":\EFI\IXIN-SETUP"), "chargeur retiré de la partition EFI");
                Expect(SetupState.Load() == null, "fichier d'état supprimé");
                Expect(File.ReadAllText(win + @":\pagefile-test.txt") == "fichier de Windows simulé", "fichiers de « Windows » toujours intacts");
            }
            catch (Exception ex)
            {
                Expect(false, "exception : " + ex);
            }
            finally
            {
                DetachTestDisk(vhd);
            }
            Console.WriteLine(failures == 0 ? "AUTO-TEST RÉUSSI" : $"AUTO-TEST : {failures} échec(s)");
            return failures == 0 ? 0 : 1;
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
            Expect(BootEntry.Loader().Length > 100000, "chargeur GRUB intégré au programme");
        }

        // ── disque virtuel ───────────────────────────────────────────────────

        private static void CreateTestDisk(string vhd, string esp, string win)
        {
            DetachTestDisk(vhd);
            Diskpart($@"create vdisk file=""{vhd}"" maximum=81920 type=expandable
select vdisk file=""{vhd}""
attach vdisk
convert gpt
create partition efi size=100
format quick fs=fat32 label=""SYSTEM""
assign letter={esp}
create partition msr size=16
create partition primary
format quick fs=ntfs label=""Windows""
assign letter={win}
");
            Console.WriteLine($"Disque virtuel prêt : EFI {esp}:, « Windows » {win}:");
        }

        private static void DetachTestDisk(string vhd)
        {
            if (!File.Exists(vhd)) return;
            Diskpart($"select vdisk file=\"{vhd}\"\ndetach vdisk\n", check: false);
            try { File.Delete(vhd); } catch (IOException) { }
        }

        private static void Diskpart(string script, bool check = true)
        {
            string file = Path.Combine(Path.GetTempPath(), "ixin-diskpart.txt");
            File.WriteAllText(file, script, Encoding.ASCII);
            try
            {
                string output = BootEntry.Run("diskpart.exe", $"/s \"{file}\"", check);
                Console.WriteLine(output.Trim());
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static string NextFreeLetter(string taken)
        {
            for (char c = (char)(taken[0] - 1); c >= 'G'; c--)
            {
                if (!DriveInfo.GetDrives().Any(d => char.ToUpperInvariant(d.Name[0]) == c)) return c.ToString();
            }
            throw new SetupException("Pas de lettre de lecteur libre pour le test");
        }

        private static string MountTemporarily(PartitionRef p)
        {
            var o = Ps.One(@"
param([int]$Disk, [int]$Number)
Add-PartitionAccessPath -DiskNumber $Disk -PartitionNumber $Number -AssignDriveLetter
[pscustomobject]@{ Letter = [string](Get-Partition -DiskNumber $Disk -PartitionNumber $Number).DriveLetter }",
                new Hashtable { ["Disk"] = p.Disk, ["Number"] = p.Number });
            return Ps.Str(o, "Letter").Trim('\0', ' ');
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
