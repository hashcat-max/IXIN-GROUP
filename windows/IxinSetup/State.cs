using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace IxinSetup
{
    /// <summary>
    /// Ce que IXIN OS Setup a modifié (C:\ProgramData\IXIN\setup-state.json) : permet de reprendre
    /// une préparation, de redémarrer sur l'installation plus tard, ou de tout annuler.
    /// </summary>
    internal sealed class SetupState
    {
        public string Version { get; set; } = Program.Version;
        public string Created { get; set; } = DateTime.Now.ToString("s");
        public string DriveLetter { get; set; } = "C";
        public int DiskNumber { get; set; } = -1;
        public long OriginalSize { get; set; }          // taille de Windows avant réduction
        public long ShrunkBy { get; set; }
        public long IxinSize { get; set; }              // espace libre laissé à IXIN OS
        public int SetupPartitionNumber { get; set; }
        public long SetupPartitionOffset { get; set; }
        public long SetupPartitionSize { get; set; }
        public string IsoName { get; set; }
        public string IsoSha256 { get; set; }
        public bool LoaderInstalled { get; set; }
        public string BootEntryId { get; set; }         // {guid} de l'entrée « IXIN OS Setup »
        public bool BitLockerSuspended { get; set; }
        public int? PreviousHiberboot { get; set; }     // démarrage rapide de Windows avant modification
        public int? PreviousRtcUtc { get; set; }        // horloge matérielle en UTC avant modification
        public bool? FastStartupChanged { get; set; }   // null : fichier d'une version précédente (réglage modifié)
        public bool? ClockChanged { get; set; }
        public string EspMode { get; set; }             // partition EFI du chargeur : Windows, Existing, Create
        public string EspGuid { get; set; }
        public int EspDisk { get; set; } = -1;
        public int EspNumber { get; set; }
        public string EspVolumePath { get; set; }
        public bool EspCreated { get; set; }            // partition EFI créée par IXIN OS Setup
        public bool KeepSetupPartition { get; set; }    // IXIN-SETUP gardée après l'installation (option avancée)
        public bool Completed { get; set; }
        public int RestartTries { get; set; }           // redémarrages vers l'installation demandés

        private static string FilePath => Path.Combine(Log.Directory, "setup-state.json");

        public static SetupState Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return new JavaScriptSerializer().Deserialize<SetupState>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Log.Warn("Fichier d'état illisible : " + ex.Message);
                return null;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Log.Directory);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
        }

        public static void Delete()
        {
            try { File.Delete(FilePath); } catch (IOException) { }
        }
    }
}
