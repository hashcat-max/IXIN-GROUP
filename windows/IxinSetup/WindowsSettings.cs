using System;
using Microsoft.Win32;

namespace IxinSetup
{
    /// <summary>
    /// Réglages de Windows recommandés pour le double démarrage, réversibles (anciennes valeurs
    /// conservées dans le fichier d'état) :
    /// - démarrage rapide désactivé : sinon Windows « hiberne » au lieu de s'éteindre et ses
    ///   disques restent dans un état intermédiaire ;
    /// - horloge matérielle en UTC, comme IXIN OS : sinon l'heure de Windows est fausse
    ///   après chaque passage sous IXIN OS.
    /// </summary>
    internal static class WindowsSettings
    {
        private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
        private const string TimeKey = @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation";

        public static void Apply(SetupState st)
        {
            st.PreviousHiberboot = ReadDword(PowerKey, "HiberbootEnabled");
            st.PreviousRtcUtc = ReadDword(TimeKey, "RealTimeIsUniversal");
            WriteDword(PowerKey, "HiberbootEnabled", 0);
            WriteDword(TimeKey, "RealTimeIsUniversal", 1);
            Log.Info($"Démarrage rapide désactivé (avant : {st.PreviousHiberboot?.ToString() ?? "absent"}), " +
                     $"horloge en UTC (avant : {st.PreviousRtcUtc?.ToString() ?? "absent"})");
        }

        public static void Restore(SetupState st)
        {
            Restore(PowerKey, "HiberbootEnabled", st.PreviousHiberboot);
            Restore(TimeKey, "RealTimeIsUniversal", st.PreviousRtcUtc);
            Log.Info("Réglages de Windows rétablis");
        }

        private static int? ReadDword(string key, string name)
        {
            using (var k = Registry.LocalMachine.OpenSubKey(key))
            {
                var v = k?.GetValue(name);
                return v == null ? (int?)null : Convert.ToInt32(v);
            }
        }

        private static void WriteDword(string key, string name, int value)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(key))
            {
                k.SetValue(name, value, RegistryValueKind.DWord);
            }
        }

        private static void Restore(string key, string name, int? previous)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(key))
            {
                if (previous.HasValue) k.SetValue(name, previous.Value, RegistryValueKind.DWord);
                else k.DeleteValue(name, false);
            }
        }
    }
}
