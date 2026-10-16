using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IxinSetup
{
    /// <summary>Partition EFI (FAT32) trouvée sur l'un des disques du PC.</summary>
    internal sealed class EspInfo
    {
        public int Disk;
        public int Number;
        public long Offset;
        public long Size;
        public long Free = -1;              // espace libre ; -1 si Windows ne l'indique pas
        public string Guid = "";            // identifiant unique de la partition (GPT), {…}
        public string VolumePath = "";      // \\?\Volume{…}\ : accès sans lettre de lecteur
        public string FileSystem = "";
        public string Label = "";
        public string DiskName = "";
        public bool IsSystem;               // partition qui a démarré Windows
        public bool Removable;              // disque USB ou carte mémoire
        public bool EspType;                // type GPT « partition système EFI »

        /// <summary>FAT : seul système de fichiers que le firmware UEFI sait lire.</summary>
        public bool IsFat => FileSystem.StartsWith("FAT", StringComparison.OrdinalIgnoreCase) || (FileSystem.Length == 0 && EspType);

        /// <summary>Identifiant au format de Linux (PARTUUID) : minuscules, sans accolades.</summary>
        public string PartUuid => Guid.Trim('{', '}', ' ').ToLowerInvariant();

        public string Describe()
        {
            string free = Free >= 0 ? $", {Facts.Mb(Free)} libres" : "";
            return $"disque {Disk}, partition {Number} ({Facts.Mb(Size)}{free})" +
                   (IsSystem ? ", démarre Windows" : "") + (Removable ? ", disque USB" : "");
        }
    }

    /// <summary>État du PC, relevé avant toute modification.</summary>
    internal sealed class Facts
    {
        public const long GiB = 1024L * 1024 * 1024;

        public string DriveLetter = "C";
        public bool IsUefi;
        public bool? SecureBoot;            // null : inconnu
        public int DiskNumber = -1;
        public int PartitionNumber;
        public string PartitionStyle = "";
        public string DiskName = "";
        public long DiskSize;
        public long Offset, Size, SizeMin, Free;
        public List<EspInfo> Esps = new List<EspInfo>();   // partitions EFI de tous les disques
        public BitLockerInfo BitLocker = new BitLockerInfo();
        public bool HasBattery, OnBattery;
        public int BatteryPercent = -1;
        public string WindowsName = "";
        public string Problem;              // erreur de lecture du disque, le cas échéant

        /// <summary>Taille maximale dont Windows peut être réduit (contrainte de NTFS).</summary>
        public long MaxShrinkByFs => Math.Max(0, Size - SizeMin);

        public static Facts Collect(string driveLetter)
        {
            var f = new Facts { DriveLetter = driveLetter.TrimEnd(':', '\\').ToUpperInvariant() };
            f.IsUefi = Native.IsUefi();
            f.SecureBoot = ReadSecureBoot();
            f.WindowsName = ReadWindowsName();
            ReadPower(f);
            try
            {
                ReadDisk(f);
            }
            catch (SetupException ex)
            {
                f.Problem = ex.Message;
                Log.Error("Lecture du disque impossible : " + ex.Message);
            }
            try
            {
                ReadEsps(f);
            }
            catch (SetupException ex)
            {
                // sans liste, IXIN OS Setup créera sa propre partition EFI
                Log.Warn("Recherche des partitions EFI impossible : " + ex.Message);
            }
            f.BitLocker = IxinSetup.BitLocker.Status(f.DriveLetter);
            return f;
        }

        private static void ReadDisk(Facts f)
        {
            const string script = @"
param([string]$Letter)
$p = Get-Partition -DriveLetter $Letter
$d = Get-Disk -Number $p.DiskNumber
$s = Get-PartitionSupportedSize -DiskNumber $p.DiskNumber -PartitionNumber $p.PartitionNumber
$v = Get-Volume -DriveLetter $Letter
[pscustomobject]@{
    DiskNumber = [int]$p.DiskNumber; PartitionNumber = [int]$p.PartitionNumber
    Offset = [long]$p.Offset; Size = [long]$p.Size; SizeMin = [long]$s.SizeMin; Free = [long]$v.SizeRemaining
    PartitionStyle = [string]$d.PartitionStyle; DiskName = [string]$d.FriendlyName; DiskSize = [long]$d.Size
}";
            var o = Ps.One(script, new Hashtable { ["Letter"] = f.DriveLetter[0] });
            f.DiskNumber = (int)Ps.Long(o, "DiskNumber");
            f.PartitionNumber = (int)Ps.Long(o, "PartitionNumber");
            f.Offset = Ps.Long(o, "Offset");
            f.Size = Ps.Long(o, "Size");
            f.SizeMin = Ps.Long(o, "SizeMin");
            f.Free = Ps.Long(o, "Free");
            f.PartitionStyle = Ps.Str(o, "PartitionStyle");
            f.DiskName = Ps.Str(o, "DiskName");
            f.DiskSize = Ps.Long(o, "DiskSize");
        }

        /// <summary>
        /// Partitions EFI de tous les disques : celle qui a démarré Windows (IsSystem) n'est pas
        /// forcément sur le disque de Windows (PC installé avec deux disques branchés, disque cloné…).
        /// </summary>
        private static void ReadEsps(Facts f)
        {
            const string script = @"
$type = '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'
foreach ($p in @(Get-Partition -ErrorAction Ignore | Where-Object { $_.GptType -eq $type -or $_.IsSystem })) {
    $d = Get-Disk -Number $p.DiskNumber -ErrorAction Ignore
    $v = $p | Get-Volume -ErrorAction Ignore | Select-Object -First 1
    [pscustomobject]@{
        Disk = [int]$p.DiskNumber; Number = [int]$p.PartitionNumber; Offset = [long]$p.Offset; Size = [long]$p.Size
        Guid = [string]$p.Guid; IsSystem = [bool]$p.IsSystem; EspType = [bool]($p.GptType -eq $type)
        Removable = [bool]([string]$d.BusType -in @('USB', 'SD', 'MMC')); DiskName = [string]$d.FriendlyName
        FileSystem = [string]$v.FileSystem; Label = [string]$v.FileSystemLabel; VolumePath = [string]$v.Path
        Free = $(if ($v) { [long]$v.SizeRemaining } else { -1 })
    }
}";
            foreach (var o in Ps.Run(script))
            {
                if (o == null) continue;
                f.Esps.Add(new EspInfo
                {
                    Disk = (int)Ps.Long(o, "Disk"),
                    Number = (int)Ps.Long(o, "Number"),
                    Offset = Ps.Long(o, "Offset"),
                    Size = Ps.Long(o, "Size"),
                    Free = Ps.Long(o, "Free"),
                    Guid = Ps.Str(o, "Guid"),
                    VolumePath = Ps.Str(o, "VolumePath"),
                    FileSystem = Ps.Str(o, "FileSystem"),
                    Label = Ps.Str(o, "Label"),
                    DiskName = Ps.Str(o, "DiskName"),
                    IsSystem = Ps.Bool(o, "IsSystem"),
                    Removable = Ps.Bool(o, "Removable"),
                    EspType = Ps.Bool(o, "EspType"),
                });
            }
        }

        private static bool? ReadSecureBoot()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                {
                    var v = key?.GetValue("UEFISecureBootEnabled");
                    return v == null ? (bool?)null : Convert.ToInt32(v) == 1;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ReadWindowsName()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int build = int.TryParse(key?.GetValue("CurrentBuild")?.ToString(), out var b) ? b : 0;
                    string name = build >= 22000 ? "Windows 11" : "Windows 10";
                    string edition = key?.GetValue("EditionID")?.ToString() ?? "";
                    string display = key?.GetValue("DisplayVersion")?.ToString() ?? "";
                    return (name + " " + edition + " " + display).Trim() + " (build " + build + ")";
                }
            }
            catch (Exception)
            {
                return "Windows";
            }
        }

        private static void ReadPower(Facts f)
        {
            var p = SystemInformation.PowerStatus;
            f.HasBattery = p.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
                           && p.BatteryChargeStatus != BatteryChargeStatus.Unknown;
            f.OnBattery = f.HasBattery && p.PowerLineStatus == PowerLineStatus.Offline;
            f.BatteryPercent = f.HasBattery ? (int)Math.Round(p.BatteryLifePercent * 100) : -1;
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows : " + WindowsName);
            sb.AppendLine("UEFI : " + IsUefi + " ; Secure Boot : " + (SecureBoot?.ToString() ?? "inconnu"));
            sb.AppendLine($"Disque {DiskNumber} « {DiskName} » ({PartitionStyle}, {Gb(DiskSize)})");
            sb.AppendLine($"{DriveLetter}: partition {PartitionNumber}, {Gb(Size)}, libre {Gb(Free)}, réduction possible {Gb(MaxShrinkByFs)}");
            if (Esps.Count == 0) sb.AppendLine("Partition EFI : aucune trouvée");
            foreach (var e in Esps) sb.AppendLine($"Partition EFI : {e.Describe()} — {e.FileSystem} « {e.Label} » {e.Guid}");
            sb.AppendLine("BitLocker : " + BitLocker.Describe());
            sb.AppendLine("Batterie : " + (HasBattery ? (OnBattery ? "sur batterie " : "sur secteur ") + BatteryPercent + " %" : "aucune"));
            if (Problem != null) sb.AppendLine("Problème : " + Problem);
            return sb.ToString();
        }

        public static string Gb(long bytes)
        {
            return (bytes / (double)GiB).ToString("0.#") + " Go";
        }

        public static string Mb(long bytes)
        {
            return bytes >= GiB ? Gb(bytes) : (bytes / 1048576.0).ToString("0") + " Mo";
        }
    }
}
