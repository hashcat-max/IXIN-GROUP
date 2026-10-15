using System;
using System.Runtime.InteropServices;

namespace IxinSetup
{
    internal static class Native
    {
        private enum FirmwareType : uint { Unknown = 0, Bios = 1, Uefi = 2, Max = 3 }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFirmwareType(out FirmwareType type);

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        /// <summary>Vrai si Windows a démarré en mode UEFI (seul mode pris en charge).</summary>
        public static bool IsUefi()
        {
            try
            {
                return GetFirmwareType(out var type) && type == FirmwareType.Uefi;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>Permet d'écrire dans la console qui a lancé le programme (modes en ligne de commande).</summary>
        public static void AttachParentConsole()
        {
            AttachConsole(-1);
        }
    }
}
