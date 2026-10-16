using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace IxinSetup
{
    internal sealed class PartitionRef
    {
        public int Disk;
        public int Number;
        public long Offset;
        public long Size;
        public string DriveLetter = "";
        public string Guid = "";
    }

    /// <summary>Opérations sur le disque, faites par Windows lui-même (module Storage).</summary>
    internal static class Storage
    {
        public const string SetupLabel = "IXIN-SETUP";
        public const string EspLabel = "IXIN-EFI";
        public const string EspGptType = "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}";
        public const long MiB = 1024L * 1024;

        public static long AlignUp(long value, long alignment) => (value + alignment - 1) / alignment * alignment;
        public static long AlignDown(long value, long alignment) => value / alignment * alignment;

        /// <summary>Réduit la partition de Windows à <paramref name="newSize"/> octets.</summary>
        public static long Shrink(string letter, long newSize)
        {
            var o = Ps.One(@"
param([string]$Letter, [long]$NewSize)
Resize-Partition -DriveLetter $Letter -Size $NewSize
[pscustomobject]@{ Size = [long](Get-Partition -DriveLetter $Letter).Size }",
                new Hashtable { ["Letter"] = letter, ["NewSize"] = newSize });
            return Ps.Long(o, "Size");
        }

        /// <summary>
        /// Lève les blocages les plus courants de la réduction de Windows : le fichier de mise en veille
        /// prolongée (hiberfil.sys, impossible à déplacer) est supprimé, puis l'espace libre est regroupé
        /// au début du volume (comme « Optimiser les lecteurs »). Renvoie false si une étape a échoué.
        /// </summary>
        public static bool UnblockShrink(string letter, bool hibernation = true)
        {
            bool ok = true;
            if (hibernation)
                ok &= BootEntry.Exec("powercfg.exe", "/hibernate off", out _) == 0;
            ok &= BootEntry.Exec("defrag.exe", letter + ": /X", out string report) == 0;
            Log.Info("Regroupement de l'espace libre :\n" + report.Trim());
            return ok;
        }

        /// <summary>Rend à Windows tout l'espace libre situé juste après sa partition.</summary>
        public static long ExtendToMax(string letter)
        {
            var o = Ps.One(@"
param([string]$Letter)
$p = Get-Partition -DriveLetter $Letter
$s = Get-PartitionSupportedSize -DiskNumber $p.DiskNumber -PartitionNumber $p.PartitionNumber
if ($s.SizeMax -gt $p.Size + 16MB) { Resize-Partition -DriveLetter $Letter -Size $s.SizeMax }
[pscustomobject]@{ Size = [long](Get-Partition -DriveLetter $Letter).Size }",
                new Hashtable { ["Letter"] = letter });
            return Ps.Long(o, "Size");
        }

        /// <summary>Espace libre (non partitionné) situé juste après la partition de Windows.</summary>
        public static long FreeAfter(string letter)
        {
            var o = Ps.One(@"
param([string]$Letter)
$p = Get-Partition -DriveLetter $Letter
$s = Get-PartitionSupportedSize -DiskNumber $p.DiskNumber -PartitionNumber $p.PartitionNumber
[pscustomobject]@{ Free = [long]([math]::Max(0, $s.SizeMax - $p.Size)) }",
                new Hashtable { ["Letter"] = letter });
            return Ps.Long(o, "Free");
        }

        /// <summary>
        /// Crée la partition exFAT « IXIN-SETUP » (qui recevra l'ISO) à l'emplacement indiqué
        /// et lui attribue une lettre temporaire pour la copie.
        /// </summary>
        public static PartitionRef CreateSetupPartition(int disk, long offset, long size)
        {
            // Juste après sa création, Windows met parfois quelques secondes à présenter le volume :
            // le formatage est retenté au lieu d'échouer.
            var o = Ps.One(@"
param([int]$Disk, [long]$Offset, [long]$Size, [string]$Label)
$n = (New-Partition -DiskNumber $Disk -Offset $Offset -Size $Size).PartitionNumber
for ($i = 1; ; $i++) {
    try {
        Format-Volume -Partition (Get-Partition -DiskNumber $Disk -PartitionNumber $n) -FileSystem exFAT -NewFileSystemLabel $Label -Confirm:$false -Force -ErrorAction Stop | Out-Null
        break
    } catch {
        if ($i -ge 8) { throw }
        Start-Sleep -Seconds 2
        Update-HostStorageCache -ErrorAction Ignore
    }
}
Set-Partition -DiskNumber $Disk -PartitionNumber $n -NoDefaultDriveLetter $true
$p = Get-Partition -DiskNumber $Disk -PartitionNumber $n
[pscustomobject]@{ Number = [int]$n; Offset = [long]$p.Offset; Size = [long]$p.Size; Guid = [string]$p.Guid }",
                new Hashtable { ["Disk"] = disk, ["Offset"] = offset, ["Size"] = size, ["Label"] = SetupLabel });
            var part = new PartitionRef
            {
                Disk = disk,
                Number = (int)Ps.Long(o, "Number"),
                Offset = Ps.Long(o, "Offset"),
                Size = Ps.Long(o, "Size"),
                Guid = Ps.Str(o, "Guid"),
            };
            part.DriveLetter = AssignLetter(part);
            return part;
        }

        /// <summary>
        /// Donne une lettre de lecteur à la partition (Windows ne le fait pas toujours tout seul,
        /// par exemple quand le montage automatique est désactivé) et vérifie qu'elle est accessible.
        /// </summary>
        public static string AssignLetter(PartitionRef p)
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                string letter = BootEntry.FreeDriveLetter();
                try
                {
                    Ps.Run(@"
param([int]$Disk, [int]$Number, [string]$Letter)
Add-PartitionAccessPath -DiskNumber $Disk -PartitionNumber $Number -AccessPath ""$($Letter):\""",
                        new Hashtable { ["Disk"] = p.Disk, ["Number"] = p.Number, ["Letter"] = letter });
                }
                catch (SetupException ex)
                {
                    Log.Warn($"Lettre {letter}: refusée pour la partition {p.Number} du disque {p.Disk} : {ex.Message}");
                }
                string got = CurrentLetter(p.Disk, p.Number);
                if (got.Length > 0 && Directory.Exists(got + @":")) return got;
                Thread.Sleep(2000);
            }
            throw new SetupException($"Windows n'a pas rendu accessible la partition {p.Number} du disque {p.Disk} (aucune lettre de lecteur).");
        }

        private static string CurrentLetter(int disk, int number)
        {
            var o = Ps.One(@"
param([int]$Disk, [int]$Number)
Update-HostStorageCache -ErrorAction Ignore
[pscustomobject]@{ Letter = [string](Get-Partition -DiskNumber $Disk -PartitionNumber $Number).DriveLetter }",
                new Hashtable { ["Disk"] = disk, ["Number"] = number });
            return Ps.Str(o, "Letter").Trim('\0', ' ');
        }

        // ── partition EFI dédiée à IXIN OS ───────────────────────────────────

        /// <summary>
        /// Crée une partition système EFI vide à l'emplacement indiqué (diskpart, comme les scripts
        /// de déploiement de Microsoft). Renvoyée aussitôt relue : son identifiant est enregistré
        /// avant le formatage, pour pouvoir la supprimer en cas d'échec.
        /// </summary>
        public static PartitionRef CreateEsp(int disk, long offset, long size)
        {
            Diskpart($"select disk {disk}\ncreate partition efi size={size / MiB} offset={offset / 1024}\n");
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                var o = Ps.One(@"
param([int]$Disk, [long]$Offset, [string]$Type)
Update-HostStorageCache -ErrorAction Ignore
$p = Get-Partition -DiskNumber $Disk | Where-Object { [math]::Abs($_.Offset - $Offset) -lt 1MB -and $_.GptType -eq $Type } | Select-Object -First 1
if ($p) { [pscustomobject]@{ Number = [int]$p.PartitionNumber; Offset = [long]$p.Offset; Size = [long]$p.Size; Guid = [string]$p.Guid } }",
                    new Hashtable { ["Disk"] = disk, ["Offset"] = offset, ["Type"] = EspGptType });
                if (o != null)
                {
                    var esp = new PartitionRef
                    {
                        Disk = disk,
                        Number = (int)Ps.Long(o, "Number"),
                        Offset = Ps.Long(o, "Offset"),
                        Size = Ps.Long(o, "Size"),
                        Guid = Ps.Str(o, "Guid"),
                    };
                    Log.Info($"Partition EFI créée : disque {disk}, partition {esp.Number}, {esp.Guid}");
                    return esp;
                }
                Thread.Sleep(2000);
            }
            throw new SetupException("La partition EFI créée est introuvable.");
        }

        /// <summary>Formate la partition EFI créée (FAT32, « IXIN-EFI ») et lui donne une lettre de lecteur.</summary>
        public static string FormatEsp(PartitionRef esp)
        {
            string letter = BootEntry.FreeDriveLetter();
            Diskpart($"select disk {esp.Disk}\nselect partition {esp.Number}\nformat quick fs=fat32 label=\"{EspLabel}\"\nassign letter={letter}\n");
            for (int attempt = 1; attempt <= 5 && !Directory.Exists(letter + @":\"); attempt++) Thread.Sleep(1000);
            if (!Directory.Exists(letter + @":\"))
                throw new SetupException("La partition EFI créée n'est pas accessible après formatage.");
            esp.DriveLetter = letter;
            return letter;
        }

        /// <summary>Donne une lettre de lecteur à une partition EFI existante (sans en modifier le contenu).</summary>
        public static string MountEsp(int disk, int number, string volumePath)
        {
            string letter = BootEntry.FreeDriveLetter();
            if (!string.IsNullOrEmpty(volumePath))
                BootEntry.Run("mountvol.exe", $"{letter}: {volumePath}", check: false);
            if (!Directory.Exists(letter + @":\"))
                Diskpart($"select disk {disk}\nselect partition {number}\nassign letter={letter}\n");
            if (!Directory.Exists(letter + @":\"))
                throw new SetupException($"Partition EFI {number} du disque {disk} inaccessible.");
            return letter;
        }

        /// <summary>
        /// Supprime la partition EFI créée par IXIN OS Setup, retrouvée par son identifiant :
        /// jamais une autre (ni, à plus forte raison, celle qui démarre Windows).
        /// </summary>
        public static void DeleteCreatedEsp(int disk, string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            var o = Ps.One(@"
param([int]$Disk, [string]$Guid, [string]$Type)
Update-HostStorageCache -ErrorAction Ignore
$p = Get-Partition -DiskNumber $Disk -ErrorAction Ignore | Where-Object { $_.Guid -eq $Guid } | Select-Object -First 1
if ($p) { [pscustomobject]@{ Number = [int]$p.PartitionNumber; Esp = [bool]($p.GptType -eq $Type); IsSystem = [bool]$p.IsSystem } }",
                new Hashtable { ["Disk"] = disk, ["Guid"] = guid, ["Type"] = EspGptType });
            if (o == null)
            {
                Log.Info("Partition EFI créée déjà supprimée : " + guid);
                return;
            }
            if (!Ps.Bool(o, "Esp") || Ps.Bool(o, "IsSystem"))
                throw new SetupException("La partition " + guid + " n'est pas celle créée par IXIN OS Setup : elle n'est pas supprimée.");
            int number = (int)Ps.Long(o, "Number");
            Diskpart($"select disk {disk}\nselect partition {number}\ndelete partition override\n");
            Log.Info($"Partition EFI créée supprimée (disque {disk}, partition {number})");
        }

        /// <summary>Exécute un script diskpart (les commandes de « Gestion des disques » en ligne de commande).</summary>
        public static string Diskpart(string script, bool check = true)
        {
            string file = Path.Combine(Path.GetTempPath(), "ixin-diskpart-" + System.Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(file, script, Encoding.ASCII);
            try
            {
                string output = BootEntry.Run("diskpart.exe", $"/s \"{file}\"", check);
                Log.Info("diskpart :\n" + script.Trim() + "\n→ " + output.Trim());
                return output;
            }
            finally
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }

        /// <summary>Retire la lettre de lecteur de la partition (Windows ne l'affiche plus).</summary>
        public static void RemoveDriveLetter(PartitionRef p)
        {
            if (string.IsNullOrEmpty(p.DriveLetter)) return;
            Ps.Run(@"
param([int]$Disk, [int]$Number, [string]$Letter)
Remove-PartitionAccessPath -DiskNumber $Disk -PartitionNumber $Number -AccessPath ""$($Letter):\""",
                new Hashtable { ["Disk"] = p.Disk, ["Number"] = p.Number, ["Letter"] = p.DriveLetter });
            p.DriveLetter = "";
        }

        /// <summary>Partitions exFAT « IXIN-SETUP » présentes sur le PC.</summary>
        public static List<PartitionRef> FindSetupPartitions()
        {
            var list = new List<PartitionRef>();
            foreach (var o in Ps.Run(@"
param([string]$Label)
foreach ($v in @(Get-Volume | Where-Object { $_.FileSystemLabel -eq $Label -and $_.FileSystem -eq 'exFAT' })) {
    $p = Get-Partition -Volume $v
    [pscustomobject]@{ Disk = [int]$p.DiskNumber; Number = [int]$p.PartitionNumber; Offset = [long]$p.Offset; Size = [long]$p.Size; Letter = [string]$p.DriveLetter }
}", new Hashtable { ["Label"] = SetupLabel }))
            {
                list.Add(new PartitionRef
                {
                    Disk = (int)Ps.Long(o, "Disk"),
                    Number = (int)Ps.Long(o, "Number"),
                    Offset = Ps.Long(o, "Offset"),
                    Size = Ps.Long(o, "Size"),
                    DriveLetter = Ps.Str(o, "Letter").Trim('\0', ' '),
                });
            }
            return list;
        }

        public static void DeletePartition(PartitionRef p)
        {
            Ps.Run(@"
param([int]$Disk, [int]$Number)
Remove-Partition -DiskNumber $Disk -PartitionNumber $Number -Confirm:$false",
                new Hashtable { ["Disk"] = p.Disk, ["Number"] = p.Number });
        }
    }
}
