using System;
using System.Windows.Forms;

namespace IxinSetup
{
    internal static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        private static int Main(string[] args)
        {
            bool afterRestart = args.Length > 0 && args[0] == Followup.Argument;
            // Modes sans interface (intégration continue / support)
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal) && !afterRestart)
            {
                Native.AttachParentConsole();
                switch (args[0])
                {
                    case "--version":
                        Console.WriteLine("IXIN OS Setup " + Version);
                        return 0;
                    case "--self-test":
                        return SelfTest.Run(args.Length > 1 ? args[1] : null);
                    case "--self-test-boot-entry":
                        return SelfTest.RunBootEntry();
                    case "--facts":
                        Console.WriteLine($"Processeur : {Compat.NativeArchitecture()} ; Windows build {Compat.WindowsBuild()} ; " +
                                          $"64 bits : {Environment.Is64BitOperatingSystem} ; processus 64 bits : {Environment.Is64BitProcess} ; " +
                                          $".NET {Environment.Version} ; marque : {Compat.Manufacturer()}");
                        Console.WriteLine(Facts.Collect("C").Describe());
                        return 0;
                    case "--screenshots":
                        return Screenshots.Run(args.Length > 1 ? args[1] : null);
                    default:
                        Console.WriteLine("Options : --version, --facts, --self-test [dossier], --self-test-boot-entry, --screenshots [dossier]");
                        return 2;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Fatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Fatal(e.ExceptionObject as Exception);
            Log.Info("IXIN OS Setup " + Version + " — démarrage" + (afterRestart ? " (réouverture après redémarrage)" : ""));
            Application.Run(new MainForm(afterRestart: afterRestart));
            return 0;
        }

        private static void Fatal(Exception ex)
        {
            Log.Error("Erreur inattendue : " + ex);
            MessageBox.Show("Une erreur inattendue s'est produite :\n\n" + (ex?.Message ?? "?") +
                            "\n\nDétails : " + Log.FilePath, "IXIN OS Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
