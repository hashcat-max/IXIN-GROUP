using System;
using System.IO;
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

        public static event Action<string> Line;

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("ATTENTION", message);
        public static void Error(string message) => Write("ERREUR", message);

        private static void Write(string level, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message;
            lock (Gate)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(Directory);
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
