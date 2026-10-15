using System.Collections;
using System.Collections.Generic;

namespace IxinSetup
{
    internal sealed class BitLockerInfo
    {
        public bool Available;          // interface BitLocker présente (Windows Pro, ou chiffrement de l'appareil)
        public int ProtectionStatus;    // 0 : désactivée, 1 : active, 2 : inconnue (volume verrouillé)
        public int ConversionStatus;    // 0 : non chiffré, 1 : chiffré, 2-5 : chiffrement/déchiffrement en cours ou en pause

        public bool Encrypted => Available && ConversionStatus != 0;
        public bool ProtectionOn => Available && ProtectionStatus == 1;
        public bool InProgress => Available && ConversionStatus >= 2;

        public string Describe()
        {
            if (!Available) return "non disponible";
            if (!Encrypted) return "non chiffré";
            return (InProgress ? "chiffrement en cours" : "chiffré") + (ProtectionOn ? ", protection active" : ", protection suspendue");
        }
    }

    internal sealed class RecoveryKey
    {
        public string Id;
        public string Password;
    }

    /// <summary>BitLocker / chiffrement de l'appareil, via son interface WMI (Win32_EncryptableVolume).</summary>
    internal static class BitLocker
    {
        // param(...) doit être la première instruction d'un script PowerShell : les fonctions
        // communes sont insérées juste après.
        private static string Script(string param, string body) => param + "\n" + Functions + body;

        private const string Functions = @"
function Get-IxinVolume([string]$Letter) {
    try {
        Get-CimInstance -Namespace 'root/CIMV2/Security/MicrosoftVolumeEncryption' -ClassName Win32_EncryptableVolume `
            -Filter ""DriveLetter='$($Letter):'"" -ErrorAction Stop
    } catch { $null }
}
function Assert-Ok($r, [string]$what) {
    if ($r.ReturnValue -ne 0) { throw ('{0} : code 0x{1:X8}' -f $what, [uint32]$r.ReturnValue) }
}
";

        public static BitLockerInfo Status(string letter)
        {
            try
            {
                var o = Ps.One(Script(@"param([string]$Letter)", @"
$v = Get-IxinVolume $Letter
if (-not $v) { return [pscustomobject]@{ Available = $false; Protection = 0; Conversion = 0 } }
$p = Invoke-CimMethod -InputObject $v -MethodName GetProtectionStatus
$c = Invoke-CimMethod -InputObject $v -MethodName GetConversionStatus
[pscustomobject]@{ Available = $true; Protection = [int]$p.ProtectionStatus; Conversion = [int]$c.ConversionStatus }
"), new Hashtable { ["Letter"] = letter });
                return new BitLockerInfo
                {
                    Available = Ps.Bool(o, "Available"),
                    ProtectionStatus = (int)Ps.Long(o, "Protection"),
                    ConversionStatus = (int)Ps.Long(o, "Conversion"),
                };
            }
            catch (SetupException ex)
            {
                Log.Warn("État BitLocker illisible : " + ex.Message);
                return new BitLockerInfo();
            }
        }

        /// <summary>Clés de récupération (48 chiffres) du volume.</summary>
        public static List<RecoveryKey> RecoveryKeys(string letter)
        {
            var keys = new List<RecoveryKey>();
            foreach (var o in Ps.Run(Script(@"param([string]$Letter)", @"
$v = Get-IxinVolume $Letter
if (-not $v) { return }
$r = Invoke-CimMethod -InputObject $v -MethodName GetKeyProtectors -Arguments @{ KeyProtectorType = [uint32]3 }
Assert-Ok $r 'Lecture des protecteurs BitLocker'
foreach ($id in @($r.VolumeKeyProtectorID)) {
    if (-not $id) { continue }
    $k = Invoke-CimMethod -InputObject $v -MethodName GetKeyProtectorNumericalPassword -Arguments @{ VolumeKeyProtectorID = $id }
    Assert-Ok $k 'Lecture de la clé de récupération'
    [pscustomobject]@{ Id = $id; Password = $k.NumericalPassword }
}
"), new Hashtable { ["Letter"] = letter }))
            {
                keys.Add(new RecoveryKey { Id = Ps.Str(o, "Id"), Password = Ps.Str(o, "Password") });
            }
            return keys;
        }

        /// <summary>Ajoute une clé de récupération (cas rare d'un volume chiffré sans clé de secours).</summary>
        public static void AddRecoveryKey(string letter)
        {
            Ps.Run(Script(@"param([string]$Letter)", @"
$v = Get-IxinVolume $Letter
$r = Invoke-CimMethod -InputObject $v -MethodName ProtectKeyWithNumericalPassword -Arguments @{ FriendlyName = 'IXIN OS Setup' }
Assert-Ok $r 'Création de la clé de récupération'
"), new Hashtable { ["Letter"] = letter });
            Log.Info("Clé de récupération BitLocker ajoutée sur " + letter + ":");
        }

        /// <summary>
        /// Suspend la protection pendant <paramref name="reboots"/> démarrages de Windows : le menu de
        /// démarrage et la désactivation éventuelle de Secure Boot ne déclenchent alors pas la demande
        /// de clé de récupération. Windows réactive ensuite la protection tout seul.
        /// </summary>
        public static void Suspend(string letter, int reboots)
        {
            Ps.Run(Script(@"param([string]$Letter, [int]$Count)", @"
$v = Get-IxinVolume $Letter
$r = Invoke-CimMethod -InputObject $v -MethodName DisableKeyProtectors -Arguments @{ DisableCount = [uint32]$Count }
Assert-Ok $r 'Suspension de BitLocker'
"), new Hashtable { ["Letter"] = letter, ["Count"] = reboots });
            Log.Info($"BitLocker suspendu sur {letter}: pour {reboots} redémarrages");
        }

        /// <summary>Réactive aussitôt la protection (préparation annulée).</summary>
        public static void Resume(string letter)
        {
            Ps.Run(Script(@"param([string]$Letter)", @"
$v = Get-IxinVolume $Letter
if ($v) {
    $r = Invoke-CimMethod -InputObject $v -MethodName EnableKeyProtectors
    Assert-Ok $r 'Réactivation de BitLocker'
}
"), new Hashtable { ["Letter"] = letter });
            Log.Info("Protection BitLocker réactivée sur " + letter + ":");
        }
    }
}
