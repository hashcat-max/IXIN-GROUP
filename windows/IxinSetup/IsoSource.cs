using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace IxinSetup
{
    internal sealed class Asset
    {
        public string Name;
        public string Url;
        public long Size;
    }

    /// <summary>Dernière version d'IXIN OS publiée (ISO découpée en morceaux de moins de 2 Go).</summary>
    internal sealed class ReleaseInfo
    {
        public string Tag;
        public string IsoName;
        public List<Asset> Parts = new List<Asset>();
        public Asset Checksum;
        public long IsoSize => Parts.Sum(p => p.Size);
    }

    internal struct Transfer
    {
        public string Stage;
        public long Done;
        public long Total;
        public double BytesPerSecond;
    }

    internal static class IsoSource
    {
        public const string Repository = "hashcat-max/IXIN-GROUP";
        private static readonly Regex IsoPart = new Regex(@"^(ixin-os-[\w.\-]+\.iso)(\.part\d+)?$", RegexOptions.IgnoreCase);
        private static readonly Regex IsoSum = new Regex(@"^(ixin-os-[\w.\-]+\.iso)\.sha256$", RegexOptions.IgnoreCase);
        private static HttpClient client;

        private static HttpClient Client
        {
            get
            {
                if (client == null)
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("IXIN-OS-Setup/" + Program.Version);
                }
                return client;
            }
        }

        public static string DownloadDirectory => Path.Combine(Log.Directory, "telechargement");

        /// <summary>
        /// Copie de la description de la dernière version (même format que l'API de GitHub), publiée sur
        /// GitHub Pages à chaque version : utilisée si l'API refuse la demande (limite de 60 demandes par
        /// heure et par adresse IP, vite atteinte derrière une box partagée ou un réseau d'entreprise).
        /// </summary>
        public const string MirrorUrl = "https://hashcat-max.github.io/IXIN-GROUP/site/release.json";

        public static ReleaseInfo Latest(CancellationToken ct)
        {
            try
            {
                return Parse(GetJson($"https://api.github.com/repos/{Repository}/releases/latest", ct, true));
            }
            catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is NoReleaseException))
            {
                Log.Warn("API de GitHub indisponible (" + ex.Message + ") : copie publiée sur GitHub Pages");
                try
                {
                    return Parse(GetJson(MirrorUrl, ct, false));
                }
                catch (Exception mirror) when (!(mirror is OperationCanceledException))
                {
                    Log.Warn("Copie GitHub Pages : " + mirror.Message);
                    throw ex is SetupException ? ex : new SetupException("Impossible de joindre le serveur de téléchargement : " + ex.Message, ex);
                }
            }
        }

        private sealed class NoReleaseException : SetupException
        {
            public NoReleaseException() : base("Aucune version d'IXIN OS n'est publiée pour le moment (ou le dépôt n'est pas public).") { }
        }

        private static string GetJson(string url, CancellationToken ct, bool api)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                if (api) req.Headers.Accept.ParseAdd("application/vnd.github+json");
                try
                {
                    using (var resp = Client.SendAsync(req, timeout.Token).GetAwaiter().GetResult())
                    {
                        if (api && resp.StatusCode == HttpStatusCode.NotFound) throw new NoReleaseException();
                        if (!resp.IsSuccessStatusCode)
                            throw new SetupException($"Le serveur de téléchargement a répondu {(int)resp.StatusCode} ({resp.ReasonPhrase}).");
                        return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new SetupException("Le serveur de téléchargement ne répond pas (délai dépassé).");
                }
            }
        }

        public static ReleaseInfo Parse(string json)
        {
            var root = (Dictionary<string, object>)new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json);
            var info = new ReleaseInfo { Tag = root.TryGetValue("tag_name", out var t) ? t as string : "" };
            var assets = new List<Asset>();
            foreach (var item in (root.TryGetValue("assets", out var a) ? a as object[] : null) ?? new object[0])
            {
                if (!(item is Dictionary<string, object> d)) continue;
                assets.Add(new Asset
                {
                    Name = d["name"] as string,
                    Url = d["browser_download_url"] as string,
                    Size = Convert.ToInt64(d["size"]),
                });
            }
            foreach (var asset in assets.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                var m = IsoPart.Match(asset.Name);
                if (m.Success)
                {
                    if (info.IsoName != null && info.IsoName != m.Groups[1].Value) continue;
                    info.IsoName = m.Groups[1].Value;
                    info.Parts.Add(asset);
                }
            }
            info.Checksum = assets.FirstOrDefault(x => IsoSum.IsMatch(x.Name) && IsoSum.Match(x.Name).Groups[1].Value == info.IsoName);
            if (info.IsoName == null || info.Parts.Count == 0)
                throw new SetupException("La dernière version publiée ne contient pas d'ISO d'IXIN OS.");
            if (info.Checksum == null)
                throw new SetupException("La dernière version publiée n'a pas d'empreinte SHA-256 : téléchargement refusé.");
            return info;
        }

        private sealed class DownloadProgress
        {
            public string Tag { get; set; }
            public long[] Done { get; set; }
        }

        /// <summary>
        /// Télécharge l'ISO (morceaux écrits directement à leur place dans un seul fichier, reprise
        /// possible après une coupure) puis vérifie son empreinte SHA-256.
        /// </summary>
        public static string Download(ReleaseInfo r, IProgress<Transfer> progress, CancellationToken ct)
        {
            Directory.CreateDirectory(DownloadDirectory);
            string iso = Path.Combine(DownloadDirectory, r.IsoName);
            string partial = iso + ".partiel";
            string stateFile = iso + ".progression";
            var ser = new JavaScriptSerializer();

            string expected = FetchChecksum(r, ct);
            if (File.Exists(iso))
            {
                if (Sha256(iso, progress, ct) == expected) return iso;
                File.Delete(iso);
            }

            DownloadProgress st = null;
            try { st = File.Exists(stateFile) ? ser.Deserialize<DownloadProgress>(File.ReadAllText(stateFile)) : null; }
            catch (Exception) { st = null; }
            if (st == null || st.Tag != r.Tag || st.Done == null || st.Done.Length != r.Parts.Count || !File.Exists(partial))
            {
                st = new DownloadProgress { Tag = r.Tag, Done = new long[r.Parts.Count] };
                File.Delete(partial);
            }

            long total = r.IsoSize;
            var started = DateTime.UtcNow;
            long startedAt = st.Done.Sum();
            using (var output = new FileStream(partial, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 1 << 20))
            {
                if (output.Length != total) output.SetLength(total);
                long offset = 0;
                for (int i = 0; i < r.Parts.Count; i++)
                {
                    var part = r.Parts[i];
                    int failures = 0;
                    while (st.Done[i] < part.Size)
                    {
                        ct.ThrowIfCancellationRequested();
                        long before = st.Done[i];
                        try
                        {
                            DownloadRange(part, i, offset, st, output, stateFile, ser, total, started, startedAt, progress, ct);
                        }
                        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                                   (ex is IOException || ex is HttpRequestException || ex is WebException ||
                                                    ex is OperationCanceledException || ex is ObjectDisposedException || ex is TransientException))
                        {
                            // Connexion coupée ou figée (Wi-Fi instable, mise en veille…) : on reprend là où on en était
                            failures = st.Done[i] > before ? 1 : failures + 1;
                            output.Flush(true);
                            File.WriteAllText(stateFile, ser.Serialize(st));
                            if (failures > 12)
                                throw new SetupException("Le téléchargement s'interrompt sans cesse : vérifiez la connexion à Internet, puis " +
                                                         "relancez IXIN OS Setup — il reprendra là où il s'est arrêté.", ex);
                            int wait = Math.Min(60, 5 * failures);
                            Log.Warn($"Téléchargement de {part.Name} interrompu ({ex.Message}) : nouvel essai dans {wait} s");
                            progress?.Report(new Transfer { Stage = $"Connexion interrompue, nouvel essai dans {wait} s", Done = st.Done.Sum(), Total = total });
                            if (ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(wait))) ct.ThrowIfCancellationRequested();
                        }
                    }
                    offset += part.Size;
                }
            }

            string actual = Sha256(partial, progress, ct);
            if (actual != expected)
            {
                File.Delete(partial);
                File.Delete(stateFile);
                throw new SetupException("Le fichier téléchargé est abîmé (empreinte SHA-256 différente). Relancez le téléchargement.");
            }
            File.Delete(stateFile);
            if (File.Exists(iso)) File.Delete(iso);
            File.Move(partial, iso);
            Log.Info($"ISO téléchargée et vérifiée : {r.IsoName} ({expected})");
            return iso;
        }

        private sealed class TransientException : Exception
        {
            public TransientException(string message) : base(message) { }
        }

        /// <summary>Une requête : le morceau <paramref name="i"/> depuis là où il s'est arrêté.</summary>
        private static void DownloadRange(Asset part, int i, long offset, DownloadProgress st, FileStream output, string stateFile,
                                          JavaScriptSerializer ser, long total, DateTime started, long startedAt,
                                          IProgress<Transfer> progress, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, part.Url))
            {
                if (st.Done[i] > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(st.Done[i], null);
                using (var headers = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    headers.CancelAfter(TimeSpan.FromSeconds(60));
                    using (var resp = Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, headers.Token).GetAwaiter().GetResult())
                    {
                        if (st.Done[i] > 0 && resp.StatusCode != HttpStatusCode.PartialContent && resp.IsSuccessStatusCode)
                            st.Done[i] = 0;   // reprise refusée par le serveur : on recommence ce morceau
                        else if ((int)resp.StatusCode >= 500 || (int)resp.StatusCode == 429 || resp.StatusCode == HttpStatusCode.RequestTimeout)
                            throw new TransientException($"erreur {(int)resp.StatusCode} du serveur");
                        else if (!resp.IsSuccessStatusCode)
                            throw new SetupException($"Téléchargement de {part.Name} : erreur {(int)resp.StatusCode} ({resp.ReasonPhrase}).");
                        output.Position = offset + st.Done[i];
                        using (var input = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        {
                            // Connexion figée sans erreur (fréquent en Wi-Fi) : sans donnée pendant 60 s, la
                            // réponse est fermée, la lecture échoue et le téléchargement reprend.
                            long lastDone = st.Done[i];
                            var watchdog = new Timer(_ =>
                            {
                                if (st.Done[i] == lastDone) { try { resp.Dispose(); } catch (Exception) { } }
                                lastDone = st.Done[i];
                            }, null, 60000, 60000);
                            using (watchdog)
                            using (ct.Register(() => { try { resp.Dispose(); } catch (Exception) { } }))
                            {
                                var buffer = new byte[1 << 20];
                                long sinceSave = 0;
                                int n;
                                while (st.Done[i] < part.Size && (n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, part.Size - st.Done[i]))) > 0)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    output.Write(buffer, 0, n);
                                    st.Done[i] += n;
                                    sinceSave += n;
                                    if (sinceSave >= 64 << 20)
                                    {
                                        output.Flush(true);
                                        File.WriteAllText(stateFile, ser.Serialize(st));
                                        sinceSave = 0;
                                    }
                                    double seconds = Math.Max(1, (DateTime.UtcNow - started).TotalSeconds);
                                    progress?.Report(new Transfer
                                    {
                                        Stage = "Téléchargement d'IXIN OS",
                                        Done = st.Done.Sum(),
                                        Total = total,
                                        BytesPerSecond = (st.Done.Sum() - startedAt) / seconds,
                                    });
                                }
                                ct.ThrowIfCancellationRequested();
                                if (st.Done[i] < part.Size) throw new TransientException("connexion fermée avant la fin du morceau");
                            }
                        }
                    }
                }
            }
            output.Flush(true);
            File.WriteAllText(stateFile, ser.Serialize(st));
        }

        public static string FetchChecksum(ReleaseInfo r, CancellationToken ct)
        {
            string text = Client.GetStringAsync(r.Checksum.Url).GetAwaiter().GetResult();
            return ParseChecksum(text, r.IsoName) ?? throw new SetupException("Empreinte SHA-256 de la version illisible.");
        }

        /// <summary>Lit une ligne « empreinte  nom » au format sha256sum.</summary>
        public static string ParseChecksum(string text, string isoName)
        {
            foreach (var line in (text ?? "").Split('\n'))
            {
                var m = Regex.Match(line.Trim(), @"^([0-9a-fA-F]{64})\s+\*?(.+)$");
                if (m.Success && (isoName == null || Path.GetFileName(m.Groups[2].Value.Trim()) == isoName))
                    return m.Groups[1].Value.ToLowerInvariant();
            }
            return null;
        }

        public static string Sha256(string path, IProgress<Transfer> progress, CancellationToken ct)
        {
            using (var sha = SHA256.Create())
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            {
                var buffer = new byte[4 << 20];
                long done = 0, total = input.Length;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, n, null, 0);
                    done += n;
                    progress?.Report(new Transfer { Stage = "Vérification de l'ISO", Done = done, Total = total });
                }
                sha.TransformFinalBlock(buffer, 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// ISO déjà présente sur le PC : contrôle avec le fichier .sha256 voisin s'il existe, sinon
        /// avec la dernière version publiée portant le même nom.
        /// </summary>
        public static string VerifyLocal(string isoPath, IProgress<Transfer> progress, CancellationToken ct)
        {
            string name = Path.GetFileName(isoPath);
            string expected = null;
            string sidecar = isoPath + ".sha256";
            if (File.Exists(sidecar)) expected = ParseChecksum(File.ReadAllText(sidecar), null);
            if (expected == null)
            {
                try
                {
                    var r = Latest(ct);
                    if (string.Equals(r.IsoName, name, StringComparison.OrdinalIgnoreCase)) expected = FetchChecksum(r, ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Log.Warn("Empreinte en ligne indisponible : " + ex.Message);
                }
            }
            string actual = Sha256(isoPath, progress, ct);
            if (expected == null)
                throw new SetupException("Impossible de vérifier cette ISO : placez le fichier .sha256 publié avec elle dans le même dossier, " +
                                         "ou choisissez le téléchargement automatique.");
            if (actual != expected)
                throw new SetupException("Cette ISO ne correspond pas à l'empreinte publiée : fichier abîmé ou modifié.");
            Log.Info($"ISO locale vérifiée : {name} ({actual})");
            return actual;
        }
    }
}
