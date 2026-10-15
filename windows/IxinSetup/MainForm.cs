using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IxinSetup
{
    internal sealed class MainForm : Form
    {
        private const string HelpUrl = "https://github.com/hashcat-max/IXIN-GROUP/blob/HEAD/docs/INSTALLATION-WINDOWS.md";

        private readonly Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        private readonly FlowLayoutPanel footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 14, 20, 12),
            BackColor = Theme.Card,
        };

        private Facts facts;
        private SetupState state;
        private long ixinSize = Plan.DefaultIxin;
        private bool useLocalIso;
        private string localIsoPath;
        private bool keySaved;
        private CancellationTokenSource running;

        public MainForm()
        {
            Text = "IXIN OS Setup";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Card };
            var logo = new PictureBox { Size = new Size(56, 56), Location = new Point(24, 14), SizeMode = PictureBoxSizeMode.Zoom };
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ixin-logo-64.png"))
            {
                if (s != null) logo.Image = Image.FromStream(s);
            }
            header.Controls.Add(logo);
            header.Controls.Add(new Label
            {
                Text = "IXIN OS Setup",
                Font = Theme.Title,
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(92, 14),
            });
            header.Controls.Add(new Label
            {
                Text = "Installer IXIN OS à côté de Windows — version " + Program.Version,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(95, 50),
            });
            var line = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Accent };

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(line);
            Controls.Add(header);

            Shown += async (s, e) => await Analyse();
            FormClosing += (s, e) =>
            {
                if (running == null) return;
                if (MessageBox.Show("Une opération est en cours. L'annuler ? (Windows sera remis dans son état d'origine)",
                        "IXIN OS Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    running.Cancel();
                }
                e.Cancel = true;
            };
        }

        // ── navigation ────────────────────────────────────────────────────────

        private void ShowPage(Control page, params Button[] buttons)
        {
            content.Controls.Clear();
            page.Dock = DockStyle.Fill;
            content.Controls.Add(page);
            footer.Controls.Clear();
            foreach (var b in buttons) footer.Controls.Add(b);
        }

        private Button Action(string text, Action onClick, bool primary = false, bool enabled = true)
        {
            var b = Theme.Button(text, primary);
            b.Enabled = enabled;
            b.Click += (s, e) => onClick();
            return b;
        }

        private void ShowMessage(string title, string text, Color? color = null)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label(title, Theme.Title, color));
            col.Controls.Add(Theme.Label(text, null, Theme.Muted));
            ShowPage(col, Action("Quitter", Close));
        }

        private void ShowError(string message)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Un problème est survenu", Theme.Title, Theme.Danger));
            col.Controls.Add(Theme.Label(message));
            col.Controls.Add(Theme.Label("Journal détaillé : " + Log.FilePath, Theme.Small, Theme.Muted));
            ShowPage(col,
                Action("Quitter", Close),
                Action("Recommencer l'analyse", async () => await Analyse()),
                Action("Ouvrir le journal", () => Open(Log.FilePath)));
        }

        private static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch (Exception) { }
        }

        // ── 1. analyse du PC ──────────────────────────────────────────────────

        private async Task Analyse()
        {
            ShowPage(Busy("Analyse du PC…", "Lecture du disque de Windows, de BitLocker et de Secure Boot (jusqu'à une minute)."));
            try
            {
                string drive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").Substring(0, 1);
                state = SetupState.Load();
                facts = await Task.Run(() => Facts.Collect(drive));
                Log.Info("Analyse :\n" + facts.Describe());
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
                return;
            }
            if (state != null && state.Completed) await ShowManage();
            else if (state != null) ShowInterrupted();
            else ShowChecks();
        }

        private Control Busy(string title, string text)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label(title, Theme.Title));
            col.Controls.Add(Theme.Label(text, null, Theme.Muted));
            col.Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Width = 660, Height = 8, MarqueeAnimationSpeed = 30 });
            return col;
        }

        private void ShowChecks()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Installer IXIN OS à côté de Windows", Theme.Title));
            col.Controls.Add(Theme.Label("IXIN OS s'installe sur une partie libre de votre disque : Windows et vos fichiers restent en place. " +
                                         "Au démarrage du PC, un menu permettra de choisir IXIN OS ou Windows.", null, Theme.Muted));
            var checks = Plan.Checks(facts, state);
            foreach (var c in checks) col.Controls.Add(CheckRow(c));
            bool blocked = checks.Any(c => c.Level == CheckLevel.Blocking);
            var buttons = new List<Button>
            {
                Action("Continuer", ShowOptions, primary: true, enabled: !blocked),
                Action("Quitter", Close),
                Action("Aide", () => Open(HelpUrl)),
            };
            if (checks.Any(c => c.UnmovableFiles))
                buttons.Insert(1, Action("Débloquer la réduction", async () => await UnblockShrink()));
            ShowPage(col, buttons.ToArray());
        }

        private async Task UnblockShrink()
        {
            string drive = facts.DriveLetter;
            if (MessageBox.Show(
                    "IXIN OS Setup va :\n\n" +
                    "• désactiver la mise en veille prolongée de Windows (le fichier hiberfil.sys est supprimé ; " +
                    "elle est de toute façon déconseillée avec deux systèmes sur le PC) ;\n" +
                    $"• optimiser le lecteur {drive}: pour regrouper l'espace libre (quelques minutes sur un SSD, " +
                    "jusqu'à une heure sur un disque dur).\n\nVos fichiers ne sont pas modifiés. Continuer ?",
                    "IXIN OS Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            ShowPage(Busy("Déblocage de la réduction…", $"Optimisation du lecteur {drive}: — vous pouvez continuer à utiliser le PC."));
            try
            {
                bool ok = await Task.Run(() => Storage.UnblockShrink(drive));
                if (!ok) Log.Warn("Déblocage de la réduction : une étape a échoué (voir ci-dessus)");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
                return;
            }
            await Analyse();
        }

        private static Control CheckRow(Check c)
        {
            var row = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 4, 0, 6), BackColor = Theme.Background };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(new Label { Text = Theme.Symbol(c.Level), ForeColor = Theme.ColorOf(c.Level), Font = Theme.Subtitle, AutoSize = true }, 0, 0);
            var text = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            var title = Theme.Label(c.Title, Theme.Bold);
            title.MaximumSize = new Size(630, 0);
            title.Margin = new Padding(0, 2, 0, 0);
            text.Controls.Add(title);
            if (!string.IsNullOrEmpty(c.Detail))
            {
                var detail = Theme.Label(c.Detail, Theme.Small, Theme.Muted);
                detail.MaximumSize = new Size(630, 0);
                text.Controls.Add(detail);
            }
            row.Controls.Add(text, 1, 0);
            return row;
        }

        // ── 2. taille et source de l'ISO ──────────────────────────────────────

        private void ShowOptions()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Espace pour IXIN OS", Theme.Title));
            var sizeLabel = Theme.Label("", Theme.Subtitle, Theme.Accent);
            var info = Theme.Label("", Theme.Small, Theme.Muted);
            var bar = new TrackBar { Width = 660, TickStyle = TickStyle.None, BackColor = Theme.Background, SmallChange = 1, LargeChange = 10 };

            var download = new RadioButton { Text = "Télécharger la dernière version d'IXIN OS (recommandé, environ 5 Go)", AutoSize = true, Checked = !useLocalIso, ForeColor = Theme.Text, Margin = new Padding(0, 14, 0, 4) };
            var local = new RadioButton { Text = "Utiliser une ISO d'IXIN OS déjà téléchargée", AutoSize = true, Checked = useLocalIso, ForeColor = Theme.Text };
            var pathBox = new TextBox { Width = 540, ReadOnly = true, Text = localIsoPath ?? "", BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            var browse = Theme.Button("Parcourir…");
            var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 0, 0) };
            pathRow.Controls.Add(pathBox);
            pathRow.Controls.Add(browse);
            Button next = null;

            void Refresh()
            {
                long max = Plan.MaxIxin(facts, Plan.DefaultIsoSize, isoOnWindows: !useLocalIso);
                int minG = (int)(Plan.MinIxin / Plan.GiB), maxG = (int)Math.Max(minG, max / Plan.GiB);
                bar.Minimum = minG;
                bar.Maximum = maxG;
                bar.Value = (int)Math.Max(minG, Math.Min(maxG, ixinSize / Plan.GiB));
                ixinSize = bar.Value * Plan.GiB;
                long shrink = ixinSize + Plan.SetupSizeFor(Plan.DefaultIsoSize);
                sizeLabel.Text = $"IXIN OS : {bar.Value} Go";
                info.Text = $"Windows ({facts.DriveLetter}:) gardera {Facts.Gb(facts.Size - shrink)}, dont environ {Facts.Gb(facts.Free - shrink)} d'espace libre. " +
                            $"Maximum possible pour IXIN OS : {maxG} Go. Conseil : 80 Go ou plus pour les jeux.";
                pathRow.Enabled = useLocalIso;
                if (next != null) next.Enabled = max >= Plan.MinIxin && (!useLocalIso || File.Exists(localIsoPath ?? ""));
            }

            bar.Scroll += (s, e) => { ixinSize = bar.Value * Plan.GiB; Refresh(); };
            download.CheckedChanged += (s, e) => { useLocalIso = !download.Checked; Refresh(); };
            browse.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Filter = "Image ISO d'IXIN OS (*.iso)|*.iso", Title = "Choisir l'ISO d'IXIN OS" })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        localIsoPath = dlg.FileName;
                        pathBox.Text = localIsoPath;
                        local.Checked = true;
                        Refresh();
                    }
                }
            };

            col.Controls.Add(Theme.Label("Choisissez la place réservée à IXIN OS sur le disque. Le reste demeure à Windows.", null, Theme.Muted));
            col.Controls.Add(sizeLabel);
            col.Controls.Add(bar);
            col.Controls.Add(info);
            col.Controls.Add(Theme.Label("Programme d'installation", Theme.Subtitle));
            col.Controls.Add(download);
            col.Controls.Add(local);
            col.Controls.Add(pathRow);
            col.Controls.Add(Theme.Label("L'ISO est toujours vérifiée (empreinte SHA-256 publiée par IXIN) avant d'être utilisée.", Theme.Small, Theme.Muted));

            next = Action("Continuer", () =>
            {
                if (facts.BitLocker.Encrypted) ShowBitLocker();
                else ShowConfirm();
            }, primary: true);
            ShowPage(col, next, Action("Retour", ShowChecks));
            Refresh();
        }

        // ── 3. clé de récupération BitLocker ──────────────────────────────────

        private async void ShowBitLocker()
        {
            ShowPage(Busy("Clé de récupération BitLocker", "Lecture de vos clés de récupération…"));
            System.Collections.Generic.List<RecoveryKey> keys;
            try
            {
                keys = await Task.Run(() => BitLocker.RecoveryKeys(facts.DriveLetter));
            }
            catch (Exception ex)
            {
                ShowError("Lecture de la clé de récupération BitLocker impossible : " + ex.Message);
                return;
            }

            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Votre clé de récupération BitLocker", Theme.Title));
            col.Controls.Add(Theme.Label("Windows est chiffré par BitLocker. Après les changements du menu de démarrage, Windows peut " +
                                         "demander cette clé de 48 chiffres au démarrage. Notez-la (sur papier, sur votre téléphone ou une clé USB) " +
                                         "avant de continuer. Elle est souvent aussi enregistrée sur votre compte Microsoft (aka.ms/myrecoverykey).",
                                         null, Theme.Muted));
            Button next = null;
            if (keys.Count == 0)
            {
                col.Controls.Add(Theme.Label("Aucune clé de récupération n'existe pour ce disque : créez-en une avant de continuer.", Theme.Bold, Theme.Warning));
                col.Controls.Add(Action("Créer une clé de récupération", async () =>
                {
                    try
                    {
                        await Task.Run(() => BitLocker.AddRecoveryKey(facts.DriveLetter));
                        ShowBitLocker();
                    }
                    catch (Exception ex) { ShowError(ex.Message); }
                }));
            }
            foreach (var k in keys)
            {
                col.Controls.Add(Theme.Label("Identifiant de la clé : " + k.Id.Trim('{', '}').Split('-')[0], Theme.Small, Theme.Muted));
                col.Controls.Add(new TextBox
                {
                    Text = k.Password,
                    ReadOnly = true,
                    Width = 560,
                    Font = Theme.Mono,
                    BackColor = Theme.Card,
                    ForeColor = Theme.Accent,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 0, 10),
                });
            }
            if (keys.Count > 0)
            {
                col.Controls.Add(Action("Enregistrer dans un fichier…", () => SaveKeys(keys)));
            }
            var saved = new CheckBox
            {
                Text = "J'ai noté ma clé de récupération en lieu sûr (ailleurs que sur ce PC)",
                AutoSize = true,
                Checked = keySaved,
                Enabled = keys.Count > 0,
                ForeColor = Theme.Text,
                Margin = new Padding(0, 14, 0, 0),
            };
            saved.CheckedChanged += (s, e) => { keySaved = saved.Checked; next.Enabled = keySaved; };
            col.Controls.Add(saved);
            next = Action("Continuer", ShowConfirm, primary: true, enabled: keySaved && keys.Count > 0);
            ShowPage(col, next, Action("Retour", ShowOptions));
        }

        private void SaveKeys(System.Collections.Generic.List<RecoveryKey> keys)
        {
            using (var dlg = new SaveFileDialog { FileName = "Cle-de-recuperation-BitLocker.txt", Filter = "Texte (*.txt)|*.txt" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (dlg.FileName.StartsWith(facts.DriveLetter + ":", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Ce fichier est sur le disque chiffré lui-même : copiez-le aussi sur une clé USB ou un autre appareil.",
                                    "IXIN OS Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                File.WriteAllLines(dlg.FileName, new[] { "Clés de récupération BitLocker de " + Environment.MachineName + " (" + facts.DriveLetter + ":)", "" }
                    .Concat(keys.Select(k => "Identifiant : " + k.Id + Environment.NewLine + "Clé : " + k.Password + Environment.NewLine)));
            }
        }

        // ── 4. confirmation ──────────────────────────────────────────────────

        private void ShowConfirm()
        {
            long setup = Plan.SetupSizeFor(Plan.DefaultIsoSize);
            long shrink = ixinSize + setup;
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Vérifiez avant de commencer", Theme.Title));
            void Item(string text) => col.Controls.Add(Theme.Label("•  " + text));
            Item($"Windows ({facts.DriveLetter}:) sera réduit de {Facts.Gb(shrink)} : il gardera {Facts.Gb(facts.Size - shrink)}. Aucun fichier n'est supprimé.");
            Item($"{Facts.Gb(ixinSize)} resteront libres pour IXIN OS, et une partition « IXIN-SETUP » d'environ {Facts.Gb(setup)} recevra le programme d'installation (supprimée automatiquement ensuite).");
            Item(useLocalIso ? "L'ISO choisie sera vérifiée puis copiée." : "IXIN OS sera téléchargé (environ 5 Go) puis vérifié : comptez quelques minutes à une heure selon votre connexion.");
            Item("« IXIN OS Setup » sera ajouté au menu de démarrage du PC (Windows reste le choix par défaut).");
            Item("Le démarrage rapide de Windows sera désactivé et l'horloge réglée en UTC (recommandé pour le double démarrage).");
            if (facts.BitLocker.ProtectionOn) Item("BitLocker sera suspendu pendant 3 démarrages, puis se réactivera tout seul.");
            if (facts.SecureBoot == true) Item("Secure Boot devra ensuite être désactivé dans les paramètres UEFI (expliqué à l'étape suivante).");
            col.Controls.Add(Theme.Label("Tout peut être annulé : « Annuler la préparation » dans IXIN OS Setup rend l'espace à Windows.", Theme.Small, Theme.Muted));
            var backup = new CheckBox { Text = "J'ai sauvegardé mes fichiers importants (précaution recommandée)", AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 14, 0, 0) };
            col.Controls.Add(backup);
            var go = Action("Préparer l'installation", async () => await RunPrepare(), primary: true, enabled: false);
            backup.CheckedChanged += (s, e) => go.Enabled = backup.Checked;
            ShowPage(col, go, Action("Retour", () => { if (facts.BitLocker.Encrypted) ShowBitLocker(); else ShowOptions(); }));
        }

        // ── 5. préparation ───────────────────────────────────────────────────

        private async Task RunPrepare()
        {
            var col = Theme.Column();
            var stepLabel = Theme.Label("Démarrage…", Theme.Subtitle);
            var bar = new ProgressBar { Width = 660, Height = 12, Maximum = 1000, Style = ProgressBarStyle.Continuous };
            var detail = Theme.Label("", Theme.Small, Theme.Muted);
            var logBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Width = 660,
                Height = 250,
                BackColor = Theme.Card,
                ForeColor = Theme.Muted,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.Small,
            };
            col.Controls.Add(Theme.Label("Préparation de l'installation", Theme.Title));
            col.Controls.Add(Theme.Label("Si Windows propose de « formater » un nouveau disque pendant la préparation, cliquez sur Annuler " +
                                         "dans cette fenêtre : IXIN OS Setup s'en occupe.", Theme.Small, Theme.Warning));
            col.Controls.Add(stepLabel);
            col.Controls.Add(bar);
            col.Controls.Add(detail);
            col.Controls.Add(logBox);
            running = new CancellationTokenSource();
            var cancel = Action("Annuler", () =>
            {
                running?.Cancel();
                stepLabel.Text = "Annulation en cours…";
            });
            ShowPage(col, cancel);

            var step = new Progress<string>(s =>
            {
                stepLabel.Text = s;
                logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
            });
            var transfer = new Progress<Transfer>(t =>
            {
                bar.Value = t.Total > 0 ? (int)Math.Min(1000, t.Done * 1000 / t.Total) : 0;
                string speed = t.BytesPerSecond > 0 ? $" — {t.BytesPerSecond / 1048576:0.0} Mo/s" : "";
                string eta = t.BytesPerSecond > 0 && t.Total > t.Done ? $" — encore environ {Duration((t.Total - t.Done) / t.BytesPerSecond)}" : "";
                detail.Text = $"{t.Stage} : {Facts.Gb(t.Done)} sur {Facts.Gb(t.Total)}{speed}{eta}";
            });

            var ct = running.Token;
            try
            {
                state = await Task.Run(() =>
                {
                    string iso, sha;
                    IProgress<string> s = step;
                    if (useLocalIso)
                    {
                        s.Report("Vérification de l'ISO choisie…");
                        sha = IsoSource.VerifyLocal(localIsoPath, transfer, ct);
                        iso = localIsoPath;
                    }
                    else
                    {
                        s.Report("Recherche de la dernière version d'IXIN OS…");
                        var release = IsoSource.Latest(ct);
                        s.Report($"Téléchargement d'IXIN OS ({release.Tag}, {Facts.Gb(release.IsoSize)})…");
                        iso = IsoSource.Download(release, transfer, ct);
                        sha = IsoSource.FetchChecksum(release, ct);
                    }
                    s.Report("Nouvelle analyse du disque…");
                    var fresh = Facts.Collect(facts.DriveLetter);
                    long isoSize = new FileInfo(iso).Length;
                    long max = Plan.MaxIxin(fresh, isoSize, isoOnWindows: false);
                    if (max < Plan.MinIxin)
                        throw new SetupException("Il n'y a plus assez d'espace libre sur Windows pour IXIN OS.");
                    long size = Math.Min(ixinSize, max);
                    var result = Installer.Prepare(fresh, size, iso, sha, step, transfer, ct);
                    if (!useLocalIso)
                    {
                        try { File.Delete(iso); } catch (IOException) { }
                    }
                    return result;
                });
                running = null;
                ShowDone();
            }
            catch (OperationCanceledException)
            {
                running = null;
                ShowMessage("Préparation annulée", "Rien n'a été modifié : Windows est dans son état d'origine.");
            }
            catch (Exception ex)
            {
                running = null;
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        private static string Duration(double seconds)
        {
            if (seconds < 90) return $"{Math.Max(1, (int)seconds)} s";
            if (seconds < 5400) return $"{(int)(seconds / 60)} min";
            return $"{seconds / 3600:0.0} h";
        }

        // ── 6. terminé ───────────────────────────────────────────────────────

        private void ShowDone()
        {
            var col = Theme.Column();
            if (facts.SecureBoot == true)
            {
                col.Controls.Add(Theme.Label("Dernière étape : désactiver Secure Boot", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("Tout est prêt sur le disque. IXIN OS ne démarre pas encore avec Secure Boot :"));
                col.Controls.Add(Theme.Label("1.  Cliquez sur « Redémarrer vers les paramètres UEFI ».\n" +
                                             "2.  Dans le menu du PC (souvent l'onglet « Boot » ou « Security »), réglez « Secure Boot » sur « Disabled ».\n" +
                                             "3.  Enregistrez et quittez (souvent la touche F10).\n" +
                                             "4.  Le PC démarre alors sur l'installation d'IXIN OS. S'il redémarre sur Windows, relancez IXIN OS Setup " +
                                             "et cliquez sur « Redémarrer et installer IXIN OS ».", null, Theme.Text));
                col.Controls.Add(Theme.Label("Dans l'installation, à l'étape « Partitions », choisissez « Remplacer une partition » puis la zone « Espace libre ».", Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer vers les paramètres UEFI", () => Restart(true), primary: true),
                    Action("Plus tard", Close));
            }
            else
            {
                col.Controls.Add(Theme.Label("Tout est prêt !", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("Le PC va redémarrer sur l'installation d'IXIN OS (une seule fois : ensuite, Windows reste le choix par défaut)."));
                col.Controls.Add(Theme.Label("Dans le menu qui s'affiche, choisissez « Installer IXIN OS (à côté de Windows) ». À l'étape « Partitions » " +
                                             "de l'installation, choisissez « Remplacer une partition » puis cliquez sur la zone « Espace libre ».", null, Theme.Muted));
                col.Controls.Add(Theme.Label("Le même menu propose « Revenir à Windows » si vous changez d'avis.", Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer et installer IXIN OS", () => Restart(false), primary: true),
                    Action("Plus tard", Close));
            }
        }

        private void Restart(bool toFirmware)
        {
            try
            {
                Installer.Restart(state ?? SetupState.Load(), toFirmware);
                ShowMessage("Redémarrage…", "Le PC redémarre dans quelques secondes. Enregistrez votre travail dans les autres programmes.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        // ── préparation existante ─────────────────────────────────────────────

        private async Task ShowManage()
        {
            ShowPage(Busy("Installation préparée", "Vérification de la préparation existante…"));
            bool hasSetup;
            long reclaimable;
            try
            {
                hasSetup = await Task.Run(() => Storage.FindSetupPartitions().Any(p => p.Disk == state.DiskNumber));
                reclaimable = hasSetup ? 0 : await Task.Run(() => Storage.FreeAfter(state.DriveLetter));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return;
            }

            var col = Theme.Column();
            if (hasSetup)
            {
                col.Controls.Add(Theme.Label("L'installation d'IXIN OS est prête", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label($"Préparée le {state.Created.Replace('T', ' ')} — {Facts.Gb(state.IxinSize)} libres pour IXIN OS."));
                if (facts.SecureBoot == true)
                    col.Controls.Add(Theme.Label("Secure Boot est encore activé : désactivez-le dans les paramètres UEFI pour pouvoir démarrer l'installation.", null, Theme.Warning));
                col.Controls.Add(Theme.Label("Dans l'installation, à l'étape « Partitions », choisissez « Remplacer une partition » puis la zone « Espace libre ».", Theme.Small, Theme.Muted));
                col.Controls.Add(Theme.Label("« Annuler la préparation » supprime tout ce qu'IXIN OS Setup a ajouté et rend l'espace à Windows.", Theme.Small, Theme.Muted));
                ShowPage(col,
                    facts.SecureBoot == true
                        ? Action("Redémarrer vers les paramètres UEFI", () => Restart(true), primary: true)
                        : Action("Redémarrer et installer IXIN OS", () => Restart(false), primary: true),
                    Action("Annuler la préparation", async () => await RunUndo()),
                    Action("Quitter", Close));
            }
            else
            {
                col.Controls.Add(Theme.Label("IXIN OS est installé", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("La partition d'installation a été supprimée après l'installation d'IXIN OS."));
                if (reclaimable >= Plan.GiB)
                {
                    col.Controls.Add(Theme.Label($"{Facts.Gb(reclaimable)} libres se trouvent juste après Windows : vous pouvez les lui rendre.", null, Theme.Muted));
                    ShowPage(col,
                        Action($"Rendre {Facts.Gb(reclaimable)} à Windows", async () => await RunReclaim(), primary: true),
                        Action("Quitter", Close));
                }
                else
                {
                    ShowPage(col,
                        Action("Terminer", () => { SetupState.Delete(); Close(); }, primary: true),
                        Action("Quitter", Close));
                }
            }
        }

        private void ShowInterrupted()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Une préparation a été interrompue", Theme.Title, Theme.Warning));
            col.Controls.Add(Theme.Label("La dernière préparation ne s'est pas terminée (PC éteint pendant l'opération ?). " +
                                         "Annulez-la pour remettre Windows dans son état d'origine, puis recommencez."));
            ShowPage(col,
                Action("Annuler la préparation", async () => await RunUndo(), primary: true),
                Action("Quitter", Close));
        }

        private async Task RunUndo()
        {
            if (MessageBox.Show("Supprimer la préparation de l'installation d'IXIN OS et rendre l'espace à Windows ?",
                    "IXIN OS Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var busy = Busy("Annulation de la préparation…", "Suppression de ce qu'IXIN OS Setup a ajouté.");
            ShowPage(busy);
            var step = new Progress<string>(s => Log.Info(s));
            try
            {
                await Task.Run(() => Installer.Undo(state, step));
                state = null;
                ShowMessage("Préparation annulée", "Windows est dans son état d'origine : l'espace a été rendu et le menu de démarrage retiré.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        private async Task RunReclaim()
        {
            ShowPage(Busy("Windows récupère l'espace…", "Extension de la partition de Windows."));
            try
            {
                await Task.Run(() =>
                {
                    Storage.ExtendToMax(state.DriveLetter);
                    foreach (var id in BootEntry.Find()) BootEntry.Delete(id);
                    SetupState.Delete();
                });
                ShowMessage("C'est fait", "L'espace de l'installateur a été rendu à Windows.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }
    }
}
