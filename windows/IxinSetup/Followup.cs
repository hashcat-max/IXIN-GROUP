using System;
using System.IO;
using System.Windows.Forms;

namespace IxinSetup
{
    /// <summary>
    /// Réouverture automatique d'IXIN OS Setup à la prochaine ouverture de session Windows, après
    /// « Redémarrer et installer IXIN OS » : si le PC revient sur Windows au lieu de lancer
    /// l'installation (Secure Boot encore activé, firmware qui ignore l'entrée « IXIN OS Setup »),
    /// le programme se rouvre tout seul et explique quoi faire. Tâche planifiée à usage unique,
    /// lancée avec les droits d'administrateur (sans nouvelle demande de Windows).
    /// </summary>
    internal static class Followup
    {
        public const string Argument = "--after-restart";
        private const string TaskName = "IXIN OS Setup";

        private static string CopyPath => Path.Combine(Log.Directory, "IXIN-OS-Setup.exe");

        public static void Arm()
        {
            try
            {
                Directory.CreateDirectory(Log.Directory);
                string exe = Application.ExecutablePath;
                // Copie dans C:\ProgramData\IXIN : le fichier téléchargé peut être supprimé ou déplacé d'ici là
                if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(CopyPath), StringComparison.OrdinalIgnoreCase))
                    File.Copy(exe, CopyPath, true);
                BootEntry.Run("schtasks.exe",
                    $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /RL HIGHEST /IT /TR \"\\\"{CopyPath}\\\" {Argument}\"");
                Log.Info("Réouverture automatique d'IXIN OS Setup programmée à la prochaine ouverture de session");
            }
            catch (Exception ex)
            {
                // Confort seulement : le redémarrage n'en dépend pas
                Log.Warn("Réouverture automatique impossible : " + ex.Message);
            }
        }

        public static void Disarm()
        {
            try
            {
                if (BootEntry.Exec("schtasks.exe", $"/Query /TN \"{TaskName}\"", out _) != 0) return;
                BootEntry.Run("schtasks.exe", $"/Delete /F /TN \"{TaskName}\"", check: false);
                Log.Info("Réouverture automatique d'IXIN OS Setup retirée");
            }
            catch (Exception ex)
            {
                Log.Warn("Retrait de la réouverture automatique : " + ex.Message);
            }
        }
    }
}
