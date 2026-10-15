using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace IxinSetup
{
    /// <summary>
    /// Exécute des commandes PowerShell de Windows dans ce processus (aucun script externe) :
    /// les commandes de stockage de Windows (Resize-Partition, New-Partition, Format-Volume…)
    /// sont celles de « Gestion des disques », avec des messages d'erreur en français.
    /// </summary>
    internal static class Ps
    {
        public static Collection<PSObject> Run(string script, IDictionary parameters = null)
        {
            using (var ps = PowerShell.Create())
            {
                ps.AddScript("$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'", false);
                ps.AddStatement();
                ps.AddScript(script, false);
                if (parameters != null)
                {
                    foreach (DictionaryEntry kv in parameters)
                    {
                        ps.AddParameter((string)kv.Key, kv.Value);
                    }
                }
                Collection<PSObject> result;
                try
                {
                    result = ps.Invoke();
                }
                catch (RuntimeException ex)
                {
                    throw new SetupException(Clean(ex.ErrorRecord?.Exception?.Message ?? ex.Message), ex);
                }
                if (ps.HadErrors && ps.Streams.Error.Count > 0)
                {
                    var err = ps.Streams.Error[0];
                    throw new SetupException(Clean(err.Exception?.Message ?? err.ToString()));
                }
                return result;
            }
        }

        /// <summary>Exécute un script qui renvoie un seul objet et lit ses propriétés.</summary>
        public static PSObject One(string script, IDictionary parameters = null)
        {
            return Run(script, parameters).FirstOrDefault(o => o != null);
        }

        public static long Long(PSObject o, string name)
        {
            var v = o?.Properties[name]?.Value;
            return v == null ? 0 : Convert.ToInt64(v);
        }

        public static string Str(PSObject o, string name)
        {
            return o?.Properties[name]?.Value?.ToString() ?? "";
        }

        public static bool Bool(PSObject o, string name)
        {
            var v = o?.Properties[name]?.Value;
            return v != null && Convert.ToBoolean(v);
        }

        private static string Clean(string message)
        {
            return (message ?? "").Trim().Replace("\r", "");
        }
    }
}
