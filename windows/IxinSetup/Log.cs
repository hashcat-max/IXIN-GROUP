using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace IxinSetup
{
    /// <summary>Journal dans C:\ProgramData\IXIN\setup.log (utile au support) et dans la console.</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();

        public static string Directory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IXIN");

        public static string FilePath => Path.Combine(Directory, "setup.log");

        private static bool secured;

        /// <summary>
        /// C:\ProgramData\IXIN réservé aux administrateurs (lecture seule pour les autres comptes) : par
        /// défaut, tout utilisateur peut créer des fichiers dans un sous-dossier de ProgramData, et donc y
        /// déposer une DLL que chargerait le programme relancé avec les droits d'administrateur, ou
        /// modifier l'ISO téléchargée. Un dossier remplacé par un lien (jonction) est refusé.
        /// </summary>
        public static void SecureDirectory()
        {
            if (secured) return;
            secured = true;   // une seule tentative (le programme tourne en administrateur)
            try
            {
                var info = new DirectoryInfo(Directory);
                if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    info.Delete();   // jonction : seul le lien est supprimé, pas sa cible
                    info.Refresh();
                }
                if (!info.Exists) info.Create();
                var ds = new DirectorySecurity();
                ds.SetAccessRuleProtection(true, false);
                const InheritanceFlags all = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                ds.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    FileSystemRights.FullControl, all, PropagationFlags.None, AccessControlType.Allow));
                ds.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    FileSystemRights.FullControl, all, PropagationFlags.None, AccessControlType.Allow));
                ds.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.ReadAndExecute, all, PropagationFlags.None, AccessControlType.Allow));
                ds.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
                System.IO.Directory.SetAccessControl(Directory, ds);
                // Bibliothèques déposées à côté de la copie du programme : jamais chargées
                foreach (var dll in System.IO.Directory.GetFiles(Directory, "*.dll"))
                    File.Delete(dll);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Protection de " + Directory + " impossible : " + ex.Message);
            }
        }

        public static event Action<string> Line;

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("ATTENTION", message);
        public static void Error(string message) => Write("ERREUR", message);

        private static void Write(string level, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message;
            lock (Gate)
            {
                SecureDirectory();
                try
                {
                    File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            try { Console.WriteLine(line); } catch (IOException) { }
            Line?.Invoke(line);
        }
    }

    /// <summary>Erreur expliquée à l'utilisateur (message en français, sans détails techniques bruts).</summary>
    internal class SetupException : Exception
    {
        public SetupException(string message) : base(message) { }
        public SetupException(string message, Exception inner) : base(message, inner) { }
    }
}
