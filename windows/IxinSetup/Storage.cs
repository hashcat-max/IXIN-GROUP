using System.Collections;
using System.Collections.Generic;

namespace IxinSetup
{
    internal sealed class PartitionRef
    {
        public int Disk;
        public int Number;
        public long Offset;
        public long Size;
        public string DriveLetter = "";
    }

    /// <summary>Opérations sur le disque, faites par Windows lui-même (module Storage).</summary>
    internal static class Storage
    {
        public const string SetupLabel = "IXIN-SETUP";
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
            var o = Ps.One(@"
param([int]$Disk, [long]$Offset, [long]$Size, [string]$Label)
$p = New-Partition -DiskNumber $Disk -Offset $Offset -Size $Size
Format-Volume -Partition $p -FileSystem exFAT -NewFileSystemLabel $Label -Confirm:$false -Force | Out-Null
Set-Partition -DiskNumber $Disk -PartitionNumber $p.PartitionNumber -NoDefaultDriveLetter $true
Add-PartitionAccessPath -DiskNumber $Disk -PartitionNumber $p.PartitionNumber -AssignDriveLetter
$p = Get-Partition -DiskNumber $Disk -PartitionNumber $p.PartitionNumber
[pscustomobject]@{ Number = [int]$p.PartitionNumber; Offset = [long]$p.Offset; Size = [long]$p.Size; Letter = [string]$p.DriveLetter }",
                new Hashtable { ["Disk"] = disk, ["Offset"] = offset, ["Size"] = size, ["Label"] = SetupLabel });
            return new PartitionRef
            {
                Disk = disk,
                Number = (int)Ps.Long(o, "Number"),
                Offset = Ps.Long(o, "Offset"),
                Size = Ps.Long(o, "Size"),
                DriveLetter = Ps.Str(o, "Letter").Trim('\0', ' '),
            };
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
