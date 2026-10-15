using System;
using System.Collections;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IxinSetup
{
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
        public int EspPartitionNumber;
        public long EspSize;
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
$esp = Get-Partition -DiskNumber $p.DiskNumber | Where-Object { $_.GptType -eq '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' } | Select-Object -First 1
[pscustomobject]@{
    DiskNumber = [int]$p.DiskNumber; PartitionNumber = [int]$p.PartitionNumber
    Offset = [long]$p.Offset; Size = [long]$p.Size; SizeMin = [long]$s.SizeMin; Free = [long]$v.SizeRemaining
    PartitionStyle = [string]$d.PartitionStyle; DiskName = [string]$d.FriendlyName; DiskSize = [long]$d.Size
    EspPartition = $(if ($esp) { [int]$esp.PartitionNumber } else { 0 }); EspSize = $(if ($esp) { [long]$esp.Size } else { 0 })
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
            f.EspPartitionNumber = (int)Ps.Long(o, "EspPartition");
            f.EspSize = Ps.Long(o, "EspSize");
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
            sb.AppendLine($"{DriveLetter}: partition {PartitionNumber}, {Gb(Size)}, libre {Gb(Free)}, réductible jusqu'à {Gb(SizeMin)}");
            sb.AppendLine($"Partition EFI : n°{EspPartitionNumber} ({Gb(EspSize)})");
            sb.AppendLine("BitLocker : " + BitLocker.Describe());
            sb.AppendLine("Batterie : " + (HasBattery ? (OnBattery ? "sur batterie " : "sur secteur ") + BatteryPercent + " %" : "aucune"));
            if (Problem != null) sb.AppendLine("Problème : " + Problem);
            return sb.ToString();
        }

        public static string Gb(long bytes)
        {
            return (bytes / (double)GiB).ToString("0.#") + " Go";
        }
    }
}
