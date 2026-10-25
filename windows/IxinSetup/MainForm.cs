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
        private const string UsbHelpUrl = "https://github.com/hashcat-max/IXIN-GROUP/blob/HEAD/docs/INSTALLATION-CLE-USB.md";

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
        private List<string> ixinEntries = new List<string>();   // entrées du firmware d'IXIN OS déjà installé
        private long ixinSize = Plan.DefaultIxin;
        private bool useLocalIso;
        private string localIsoPath;
        private readonly SetupOptions options = new SetupOptions();   // options avancées
        private readonly Vm vm = new Vm();
        private VmFacts vmFacts;
        private bool keySaved;
        private CancellationTokenSource running;

        private readonly bool afterRestart;
        internal string DemoManufacturer;      // captures d'écran : marque du PC d'exemple   // rouvert automatiquement après « Redémarrer et installer IXIN OS »

        public MainForm(bool analyse = true, bool afterRestart = false)
        {
            this.afterRestart = afterRestart;
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

            // Petits écrans (portable 1366 × 768 avec mise à l'échelle 125 %…) : la fenêtre ne dépasse jamais
            // de l'écran, les boutons du bas restent visibles et le contenu défile.
            Load += (s, e) => FitToScreen();
            if (analyse) Shown += async (s, e) => await Analyse();
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

        /// <summary>Secure Boot activé et version d'IXIN OS Setup signée : il reste activé (clé IXIN via MokManager).</summary>
        private bool MokFlow => facts?.SecureBoot == true && (DemoSecureBootCapable ?? BootEntry.SecureBootCapable);
        internal bool? DemoSecureBootCapable;   // captures d'écran

        private bool KeyEnrolled() => DemoSecureBootCapable.HasValue ? false : Mok.IsEnrolled(BootEntry.SecureBootCertificate);

        /// <summary>Demande l'enregistrement de la clé IXIN (si besoin) puis redémarre sur l'installation.</summary>
        private void RestartWithMok()
        {
            try
            {
                if (!KeyEnrolled()) Mok.RequestEnrollment(BootEntry.SecureBootCertificate);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
                return;
            }
            Restart(false);
        }

        private Control MokSteps()
        {
            return Theme.Label("1.  Cliquez sur « Redémarrer et autoriser IXIN OS ».\n" +
                               "2.  Un écran bleu « Shim UEFI key management » s'affiche : appuyez sur une touche, puis choisissez " +
                               "« Enroll MOK » → « Continue » → « Yes ».\n" +
                               $"3.  Tapez le mot de passe {Mok.Password} (il ne s'affiche pas), Entrée, puis « Reboot ».\n" +
                               "4.  Le PC revient sur Windows : IXIN OS Setup se rouvre tout seul, cliquez sur « Redémarrer et installer IXIN OS ».",
                               null, Theme.Text);
        }

        private void FitToScreen()
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height) Height = area.Height;
            if (Width > area.Width) Width = area.Width;
            Left = Math.Max(area.Left, area.Left + (area.Width - Width) / 2);
            Top = Math.Max(area.Top, area.Top + (area.Height - Height) / 2);
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
            var unsupported = Compat.Check();
            if (unsupported != null)
            {
                Log.Warn($"PC non pris en charge : {unsupported.Title} ({Compat.NativeArchitecture()}, build {Compat.WindowsBuild()})");
                ShowUnsupported(unsupported);
                return;
            }
            try
            {
                string drive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").Substring(0, 1);
                state = SetupState.Load();
                if (afterRestart) await Task.Run(() => Followup.Disarm());
                facts = await Task.Run(() =>
                {
                    Compat.EnsureServices();
                    return Facts.Collect(drive);
                });
                Log.Info("Analyse :\n" + facts.Describe());
                ixinEntries = facts.IsUefi ? await Task.Run(() => BootEntry.FindIxin()) : new List<string>();
                if (ixinEntries.Count > 0) Log.Info("IXIN OS est déjà installé (entrée du firmware " + ixinEntries[0] + ")");
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is TypeLoadException || ex is TypeInitializationException)
            {
                // System.Management.Automation absent : Windows PowerShell retiré ou désactivé
                Log.Error(ex.ToString());
                ShowError("Windows PowerShell est absent ou désactivé sur ce PC : IXIN OS Setup en a besoin pour lire les disques. " +
                          "Réactivez-le (Paramètres → Applications → Fonctionnalités facultatives), ou installez IXIN OS avec une clé USB.\n\n" + ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
                return;
            }
            if (File.Exists(Vm.PendingFlag))
            {
                // Accélération activée puis PC redémarré : on reprend la machine virtuelle
                ShowVmChecks();
                return;
            }
            if (afterRestart)
            {
                // Rouvert automatiquement à l'ouverture de session : rien à dire si IXIN OS est installé
                if (ixinEntries.Count > 0 || state == null || !state.Completed)
                {
                    Close();
                    return;
                }
                ShowReturned();
                return;
            }
            if (state != null && !state.Completed) ShowInterrupted();
            else if (state != null || ixinEntries.Count > 0) await ShowManage();
            else if (vm.Exists) ShowVmManage();
            else ShowModeChoice();
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
            var blocking = checks.Where(c => c.Level == CheckLevel.Blocking).ToList();
            var buttons = new List<Button>();
            // Seul blocage : des fichiers système en fin de disque. Le bouton principal le règle.
            if (blocking.Count > 0 && blocking.All(c => c.UnmovableFiles))
                buttons.Add(Action("Débloquer la réduction", async () => await UnblockShrink(), primary: true));
            else
                buttons.Add(Action("Continuer", ShowOptions, primary: true, enabled: blocking.Count == 0));
            buttons.Add(Action("Retour", ShowModeChoice));
            buttons.Add(Action("Aide", () => Open(HelpUrl)));
            if (blocking.Count > 0 && !blocking.All(c => c.UnmovableFiles) && checks.Any(c => c.UnmovableFiles))
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

        // ── 2. taille (et options avancées) ───────────────────────────────────

        private void ShowOptions()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Espace pour IXIN OS", Theme.Title));
            var sizeLabel = Theme.Label("", Theme.Subtitle, Theme.Accent);
            var info = Theme.Label("", Theme.Small, Theme.Muted);
            var bar = new TrackBar { Width = 660, TickStyle = TickStyle.None, BackColor = Theme.Background, SmallChange = 1, LargeChange = 10 };
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
                if (next != null) next.Enabled = max >= Plan.MinIxin && (!useLocalIso || File.Exists(localIsoPath ?? ""));
            }

            bar.Scroll += (s, e) => { ixinSize = bar.Value * Plan.GiB; Refresh(); };

            col.Controls.Add(Theme.Label("Choisissez la place réservée à IXIN OS sur le disque. Le reste demeure à Windows.", null, Theme.Muted));
            col.Controls.Add(sizeLabel);
            col.Controls.Add(bar);
            col.Controls.Add(info);
            col.Controls.Add(Theme.Label("Programme d'installation", Theme.Subtitle));
            col.Controls.Add(Theme.Label(useLocalIso
                ? "ISO choisie : " + localIsoPath + " (vérifiée avant d'être utilisée)."
                : "IXIN OS sera téléchargé (environ 5 Go), puis vérifié automatiquement (empreinte SHA-256 publiée par IXIN)."));
            var custom = AdvancedSummary();
            if (custom.Count > 0)
            {
                col.Controls.Add(Theme.Label("Options avancées choisies", Theme.Subtitle));
                foreach (var line in custom) col.Controls.Add(Theme.Label("•  " + line, Theme.Small, Theme.Text));
            }
            col.Controls.Add(Theme.Label("Utilisateurs avertis : « Options avancées » permet de choisir la taille exacte, la partition de démarrage (EFI), " +
                                         "une ISO déjà téléchargée et les réglages de Windows.", Theme.Small, Theme.Muted));

            next = Action("Continuer", () =>
            {
                if (facts.BitLocker.Encrypted) ShowBitLocker();
                else ShowConfirm();
            }, primary: true);
            ShowPage(col, next, Action("Retour", ShowChecks), Action("Options avancées…", ShowAdvanced));
            Refresh();
        }

        /// <summary>Options avancées qui diffèrent du parcours par défaut, en clair.</summary>
        private List<string> AdvancedSummary()
        {
            var lines = new List<string>();
            if (options.Esp == SetupOptions.NewEsp)
                lines.Add($"Partition de démarrage (EFI) dédiée à IXIN OS ({Facts.Mb(Plan.NewEspSize)}).");
            else if (options.Esp != null)
            {
                var e = facts.Esps.FirstOrDefault(x => string.Equals(x.Guid, options.Esp, StringComparison.OrdinalIgnoreCase));
                lines.Add("Partition de démarrage (EFI) : " + (e != null ? e.Describe() : options.Esp) + ".");
            }
            if (options.KeepSetupPartition) lines.Add("La partition IXIN-SETUP sera gardée après l'installation.");
            if (!options.DisableFastStartup) lines.Add("Le démarrage rapide de Windows reste activé.");
            if (!options.UtcClock) lines.Add("L'horloge matérielle n'est pas réglée en UTC.");
            return lines;
        }

        private static string EspSummary(EspChoice c)
        {
            if (c.Mode == EspMode.Create) return $"une partition EFI de {Facts.Mb(Plan.NewEspSize)} sera créée ({c.Why})";
            return (c.Mode == EspMode.Windows ? "partition EFI de Windows, " : "partition EFI ") + c.Esp.Describe();
        }

        // ── options avancées (utilisateurs avertis) ───────────────────────────

        private void ShowAdvanced()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Options avancées", Theme.Title));
            col.Controls.Add(Theme.Label("Pour les utilisateurs avertis : les réglages par défaut conviennent à presque tous les PC.", null, Theme.Muted));
            Label Section(string text)
            {
                var l = Theme.Label(text, Theme.Subtitle);
                l.Margin = new Padding(0, 12, 0, 4);
                return l;
            }
            FlowLayoutPanel Row(params Control[] controls)
            {
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4), BackColor = Theme.Background };
                row.Controls.AddRange(controls);
                return row;
            }

            // taille exacte
            bool local = useLocalIso;
            string isoPath = localIsoPath;
            var size = new NumericUpDown { Width = 90, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body };
            foreach (Control part in size.Controls)   // zone de saisie et flèches : sinon blanches sur la page sombre
            {
                part.BackColor = Theme.Card;
                part.ForeColor = Theme.Text;
            }
            var sizeHelp = Theme.Label("", Theme.Small, Theme.Muted);
            sizeHelp.Margin = new Padding(8, 6, 0, 0);
            void SizeRange()
            {
                long max = Plan.MaxIxin(facts, Plan.DefaultIsoSize, isoOnWindows: !local);
                int minG = (int)(Plan.MinIxin / Plan.GiB), maxG = (int)Math.Max(minG, max / Plan.GiB);
                size.Minimum = minG;
                size.Maximum = maxG;
                size.Value = Math.Max(minG, Math.Min(maxG, ixinSize / Plan.GiB));
                sizeHelp.Text = $"Go pour IXIN OS (de {minG} à {maxG} Go ; 80 Go ou plus conseillés pour les jeux)";
            }
            SizeRange();
            col.Controls.Add(Section("Taille"));
            col.Controls.Add(Row(size, sizeHelp));

            // partition EFI
            var auto = Plan.ChooseEsp(facts);
            var espValues = new List<string> { null };
            var espBox = Theme.ComboBox(660);
            espBox.Items.Add("Automatique (recommandé)");
            foreach (var e in facts.Esps.Where(e => e.IsFat))
            {
                espBox.Items.Add("Partition EFI existante : " + e.Describe() + (e.IsSystem ? " (celle de Windows)" : ""));
                espValues.Add(e.Guid);
            }
            espBox.Items.Add($"Créer une partition EFI dédiée à IXIN OS ({Facts.Mb(Plan.NewEspSize)})");
            espValues.Add(SetupOptions.NewEsp);
            espBox.SelectedIndex = Math.Max(0, espValues.IndexOf(options.Esp));
            var espHelp = Theme.Label("", Theme.Small, Theme.Muted);
            espBox.SelectedIndexChanged += (s, e) => EspHelp();
            void EspHelp()
            {
                string v = espValues[Math.Max(0, espBox.SelectedIndex)];
                espHelp.Text = v == null ? "Choix d'IXIN OS Setup pour ce PC : " + EspSummary(auto) + "."
                    : v == SetupOptions.NewEsp ? $"Une partition FAT32 de {Facts.Mb(Plan.NewEspSize)} est créée sur l'espace d'IXIN OS : la partition EFI de Windows n'est pas modifiée."
                    : "IXIN OS Setup, puis IXIN OS, y ajoutent leur chargeur de démarrage (quelques Mo) à côté de ceux déjà présents.";
            }
            EspHelp();
            col.Controls.Add(Section("Partition de démarrage (EFI)"));
            col.Controls.Add(espBox);
            col.Controls.Add(espHelp);

            // programme d'installation
            var download = new RadioButton { Text = "Télécharger la dernière version d'IXIN OS (recommandé)", AutoSize = true, Checked = !local, ForeColor = Theme.Text };
            var useIso = new RadioButton { Text = "Utiliser une ISO d'IXIN OS déjà téléchargée", AutoSize = true, Checked = local, ForeColor = Theme.Text };
            var pathBox = new TextBox { Width = 540, ReadOnly = true, Text = isoPath ?? "", BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            var browse = Theme.Button("Parcourir…");
            var pathRow = Row(pathBox, browse);
            pathRow.Margin = new Padding(20, 0, 0, 0);
            col.Controls.Add(Section("Programme d'installation"));
            col.Controls.Add(download);
            col.Controls.Add(useIso);
            col.Controls.Add(pathRow);
            col.Controls.Add(Theme.Label("Le fichier .sha256 publié avec l'ISO doit se trouver dans le même dossier : l'ISO est toujours vérifiée.", Theme.Small, Theme.Muted));

            // après l'installation
            var keep = new CheckBox { Text = "Garder la partition IXIN-SETUP après l'installation", AutoSize = true, Checked = options.KeepSetupPartition, ForeColor = Theme.Text };
            col.Controls.Add(Section("Après l'installation"));
            col.Controls.Add(keep);
            col.Controls.Add(Theme.Label("Pour réinstaller ou tester IXIN OS sans retélécharger : « IXIN OS Setup » reste dans le menu de démarrage du PC.", Theme.Small, Theme.Muted));

            // réglages de Windows
            var fast = new CheckBox { Text = "Désactiver le démarrage rapide de Windows (recommandé)", AutoSize = true, Checked = options.DisableFastStartup, ForeColor = Theme.Text };
            var utc = new CheckBox { Text = "Régler l'horloge matérielle en UTC, comme IXIN OS (recommandé)", AutoSize = true, Checked = options.UtcClock, ForeColor = Theme.Text };
            col.Controls.Add(Section("Réglages de Windows pour le double démarrage"));
            col.Controls.Add(fast);
            col.Controls.Add(Theme.Label("Sinon Windows ne s'éteint pas complètement et IXIN OS ne peut pas écrire sur ses disques.", Theme.Small, Theme.Muted));
            col.Controls.Add(utc);
            col.Controls.Add(Theme.Label("Sinon l'heure de Windows se décale après chaque passage sous IXIN OS.", Theme.Small, Theme.Muted));

            Button ok = null;
            void Validate()
            {
                pathRow.Enabled = local;
                if (ok != null) ok.Enabled = !local || File.Exists(isoPath ?? "");
            }
            download.CheckedChanged += (s, e) => { local = !download.Checked; SizeRange(); Validate(); };
            browse.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Filter = "Image ISO d'IXIN OS (*.iso)|*.iso", Title = "Choisir l'ISO d'IXIN OS" })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        isoPath = dlg.FileName;
                        pathBox.Text = isoPath;
                        useIso.Checked = true;
                        Validate();
                    }
                }
            };

            ok = Action("Valider", () =>
            {
                useLocalIso = local;
                localIsoPath = isoPath;
                ixinSize = (long)size.Value * Plan.GiB;
                options.Esp = espValues[Math.Max(0, espBox.SelectedIndex)];
                options.KeepSetupPartition = keep.Checked;
                options.DisableFastStartup = fast.Checked;
                options.UtcClock = utc.Checked;
                Log.Info("Options avancées : " + (AdvancedSummary().Count == 0 ? "par défaut" : string.Join(" ", AdvancedSummary())));
                ShowOptions();
            }, primary: true);
            ShowPage(col, ok, Action("Annuler", ShowOptions), Action("Valeurs par défaut", () =>
            {
                useLocalIso = false;
                options.Esp = null;
                options.KeepSetupPartition = false;
                options.DisableFastStartup = true;
                options.UtcClock = true;
                ixinSize = Plan.DefaultIxin;
                ShowAdvanced();
            }));
            Validate();
        }

        // ── 3. clé de récupération BitLocker ──────────────────────────────────

        private async void ShowBitLocker()
        {
            ShowPage(Busy("Clé de récupération BitLocker", "Lecture de vos clés de récupération…"));
            List<RecoveryKey> keys;
            try
            {
                keys = await Task.Run(() => BitLocker.RecoveryKeys(facts.DriveLetter));
            }
            catch (Exception ex)
            {
                ShowError("Lecture de la clé de récupération BitLocker impossible : " + ex.Message);
                return;
            }
            ShowBitLockerKeys(keys);
        }

        private void ShowBitLockerKeys(List<RecoveryKey> keys)
        {
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

        private void SaveKeys(List<RecoveryKey> keys)
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
            Item($"{Facts.Gb(ixinSize)} resteront libres pour IXIN OS, et une partition « IXIN-SETUP » d'environ {Facts.Gb(setup)} recevra le programme d'installation " +
                 (options.KeepSetupPartition ? "(gardée après l'installation : option avancée)." : "(supprimée automatiquement ensuite)."));
            var esp = Plan.ChooseEsp(facts, options.Esp);
            if (esp.Mode == EspMode.Create)
                Item($"Une partition de démarrage (EFI) de {Facts.Mb(Plan.NewEspSize)} sera créée pour IXIN OS, prise sur son espace ({esp.Why}).");
            else if (esp.Esp.Disk != facts.DiskNumber)
                Item($"Le chargeur de démarrage ira dans la partition EFI de Windows, sur le disque {esp.Esp.Disk} : ne retirez pas ce disque.");
            Item(useLocalIso ? "L'ISO choisie sera vérifiée puis copiée." : "IXIN OS sera téléchargé (environ 5 Go) puis vérifié : comptez quelques minutes à une heure selon votre connexion.");
            Item("« IXIN OS Setup » sera ajouté au menu de démarrage du PC (Windows reste le choix par défaut).");
            if (options.DisableFastStartup && options.UtcClock)
                Item("Le démarrage rapide de Windows sera désactivé et l'horloge réglée en UTC (recommandé pour le double démarrage).");
            else if (options.DisableFastStartup)
                Item("Le démarrage rapide de Windows sera désactivé (horloge inchangée : option avancée).");
            else if (options.UtcClock)
                Item("L'horloge sera réglée en UTC (démarrage rapide de Windows inchangé : option avancée).");
            else
                Item("Les réglages de Windows ne sont pas modifiés (options avancées).");
            if (facts.BitLocker.ProtectionOn) Item("BitLocker sera suspendu pendant 3 démarrages, puis se réactivera tout seul.");
            if (MokFlow) Item("Secure Boot reste activé : au redémarrage, un écran bleu demandera une fois d'autoriser IXIN OS (expliqué à l'étape suivante).");
            else if (facts.SecureBoot == true) Item("Secure Boot devra ensuite être désactivé dans les paramètres UEFI (expliqué à l'étape suivante).");
            col.Controls.Add(Theme.Label("Tout peut être annulé : « Annuler la préparation » dans IXIN OS Setup rend l'espace à Windows.", Theme.Small, Theme.Muted));
            var backup = new CheckBox { Text = "J'ai sauvegardé mes fichiers importants (précaution recommandée)", AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 14, 0, 0) };
            col.Controls.Add(backup);
            var go = Action("Préparer l'installation", async () => await RunPrepare(), primary: true, enabled: false);
            backup.CheckedChanged += (s, e) => go.Enabled = backup.Checked;
            ShowPage(col, go, Action("Retour", () => { if (facts.BitLocker.Encrypted) ShowBitLocker(); else ShowOptions(); }));
        }

        // ── 5. préparation ───────────────────────────────────────────────────

        private sealed class PrepareView
        {
            public Label Step, Detail;
            public ProgressBar Bar;
            public TextBox Log;
        }

        private PrepareView ShowPrepareView(Action onCancel)
        {
            var col = Theme.Column();
            var v = new PrepareView
            {
                Step = Theme.Label("Démarrage…", Theme.Subtitle),
                Bar = new ProgressBar { Width = 660, Height = 12, Maximum = 1000, Style = ProgressBarStyle.Continuous },
                Detail = Theme.Label("", Theme.Small, Theme.Muted),
            };
            v.Log = new TextBox
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
            col.Controls.Add(v.Step);
            col.Controls.Add(v.Bar);
            col.Controls.Add(v.Detail);
            col.Controls.Add(v.Log);
            ShowPage(col, Action("Annuler", onCancel));
            return v;
        }

        private async Task RunPrepare()
        {
            running = new CancellationTokenSource();
            PrepareView view = null;
            view = ShowPrepareView(() =>
            {
                running?.Cancel();
                view.Step.Text = "Annulation en cours…";
            });
            Label stepLabel = view.Step, detail = view.Detail;
            ProgressBar bar = view.Bar;
            TextBox logBox = view.Log;

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
                    var result = Installer.Prepare(fresh, size, iso, sha, options, step, transfer, ct);
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
            if (MokFlow)
            {
                col.Controls.Add(Theme.Label("Dernière étape : autoriser IXIN OS", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("Tout est prêt sur le disque. Secure Boot reste activé (Windows, ses jeux et BitLocker n'y voient " +
                                             "aucune différence) : il suffit d'autoriser une fois la clé d'IXIN OS."));
                col.Controls.Add(MokSteps());
                col.Controls.Add(Theme.Label("Dans l'installation, à l'étape « Partitions », la zone « Espace libre » est déjà choisie.", Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer et autoriser IXIN OS", RestartWithMok, primary: true),
                    Action("Plus tard", Close));
            }
            else if (facts.SecureBoot == true)
            {
                col.Controls.Add(Theme.Label("Dernière étape : désactiver Secure Boot", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("Tout est prêt sur le disque. IXIN OS ne démarre pas encore avec Secure Boot :"));
                col.Controls.Add(Theme.Label("1.  Cliquez sur « Redémarrer vers les paramètres UEFI ».\n" +
                                             "2.  Dans le menu du PC (souvent l'onglet « Boot » ou « Security »), réglez « Secure Boot » sur « Disabled ».\n" +
                                             "3.  Enregistrez et quittez (souvent la touche F10).\n" +
                                             "4.  Le PC démarre alors sur l'installation d'IXIN OS. S'il revient sur Windows, IXIN OS Setup se rouvre " +
                                             "tout seul et vous guide.", null, Theme.Text));
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
                col.Controls.Add(Theme.Label("Le même menu propose « Revenir à Windows » si vous changez d'avis. Si le PC revient sur Windows " +
                                             "sans afficher ce menu, IXIN OS Setup se rouvre tout seul et vous guide.", Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer et installer IXIN OS", () => Restart(false), primary: true),
                    Action("Plus tard", Close));
            }
        }

        private void Restart(bool toFirmware, bool putFirst = false)
        {
            try
            {
                Installer.Restart(state ?? SetupState.Load(), toFirmware, putFirst);
                ShowMessage("Redémarrage…", "Le PC redémarre dans quelques secondes. Enregistrez votre travail dans les autres programmes.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        // ── PC non pris en charge ─────────────────────────────────────────────

        private void ShowUnsupported(Unsupported u)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label(u.Title, Theme.Title, Theme.Warning));
            col.Controls.Add(Theme.Label(u.Text));
            col.Controls.Add(Theme.Label("Rien n'a été modifié sur ce PC.", Theme.Small, Theme.Muted));
            ShowPage(col, Action("Quitter", Close), Action("Aide", () => Open(UsbHelpUrl)));
        }

        // ── retour sur Windows au lieu de l'installation ──────────────────────

        private void ShowReturned()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Le PC a redémarré sur Windows", Theme.Title, Theme.Warning));
            string maker = (DemoManufacturer ?? Compat.Manufacturer()).Trim();
            string key = Compat.BootMenuKey(maker);
            string keyText = key != null
                ? $"Pour ce PC ({maker}), la touche est en général : {key}."
                : "Selon la marque : F12 (Acer, Dell, Lenovo…), F11 (MSI, ASRock), F9 (HP), F8 ou Échap (ASUS).";
            if (MokFlow)
            {
                if (KeyEnrolled())
                {
                    col.Controls.Add(Theme.Label("La clé d'IXIN OS est enregistrée ✔", Theme.Subtitle, Theme.Accent));
                    col.Controls.Add(Theme.Label("Secure Boot reconnaît maintenant IXIN OS. Redémarrez pour lancer l'installation."));
                    ShowPage(col,
                        Action("Redémarrer et installer IXIN OS", () => Restart(false), primary: true),
                        Action("Annuler la préparation", async () => await RunUndo()),
                        Action("Plus tard", Close));
                    return;
                }
                col.Controls.Add(Theme.Label("La clé d'IXIN OS n'a pas encore été autorisée : sans elle, Secure Boot refuse de lancer " +
                                             "l'installation et le PC revient sur Windows."));
                col.Controls.Add(MokSteps());
                col.Controls.Add(Theme.Label("L'écran bleu n'est pas apparu ? Le PC n'a peut-être pas démarré sur l'installation : appuyez sur la touche " +
                                             "du menu de démarrage dès l'allumage et choisissez « IXIN OS Setup ». " + keyText, Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer et autoriser IXIN OS", RestartWithMok, primary: true),
                    Action("Désactiver Secure Boot à la place", () => Restart(true)),
                    Action("Annuler la préparation", async () => await RunUndo()),
                    Action("Plus tard", Close));
                return;
            }
            if (facts.SecureBoot == true)
            {
                col.Controls.Add(Theme.Label("L'installation d'IXIN OS n'a pas pu démarrer : Secure Boot est toujours activé. " +
                                             "Le PC refuse alors de la lancer et revient sur Windows."));
                col.Controls.Add(Theme.Label("1.  Cliquez sur « Redémarrer vers les paramètres UEFI ».\n" +
                                             "2.  Cherchez « Secure Boot » (souvent dans l'onglet « Boot », « Security » ou « Authentication ») " +
                                             "et réglez-le sur « Disabled ».\n" +
                                             "3.  Enregistrez et quittez (souvent la touche F10) : l'installation d'IXIN OS démarre.", null, Theme.Text));
                col.Controls.Add(Theme.Label("Secure Boot grisé ? Certains PC demandent d'abord un mot de passe administrateur du BIOS " +
                                             "(« Set Supervisor Password ») : le guide l'explique.", Theme.Small, Theme.Muted));
                ShowPage(col,
                    Action("Redémarrer vers les paramètres UEFI", () => Restart(true), primary: true),
                    Action("Annuler la préparation", async () => await RunUndo()),
                    Action("Aide", () => Open(HelpUrl + "#3-secure-boot")),
                    Action("Plus tard", Close));
                return;
            }
            bool retried = state.RestartTries >= 2;
            col.Controls.Add(Theme.Label(retried
                ? "Le PC ne lance toujours pas l'installation d'IXIN OS par lui-même. Vous pouvez la choisir à la main au démarrage :"
                : "Ce PC n'a pas lancé l'installation d'IXIN OS au redémarrage : certains PC ignorent cette demande de Windows. " +
                  "Cliquez sur « Réessayer » : IXIN OS Setup place cette fois l'installation en tête du démarrage du PC."));
            col.Controls.Add(Theme.Label("Ou choisissez-la vous-même : redémarrez, appuyez plusieurs fois sur la touche du menu de démarrage " +
                                         "dès l'allumage, puis choisissez « IXIN OS Setup ». " + keyText, null, retried ? Theme.Text : Theme.Muted));
            col.Controls.Add(Theme.Label("Tant que l'installation est en tête, le PC affiche à chaque démarrage le menu d'IXIN OS Setup, " +
                                         "qui propose aussi « Revenir à Windows ». « Annuler la préparation » remet tout comme avant.",
                                         Theme.Small, Theme.Muted));
            ShowPage(col,
                Action("Réessayer", () => Restart(false, putFirst: true), primary: true),
                Action("Annuler la préparation", async () => await RunUndo()),
                Action("Aide", () => Open(HelpUrl + "#6-dépannage")),
                Action("Plus tard", Close));
        }

        // ── choix : à côté de Windows ou dans une fenêtre ────────────────────

        private void ShowModeChoice()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("Comment voulez-vous utiliser IXIN OS ?", Theme.Title));
            col.Controls.Add(ModeCard("À côté de Windows (recommandé)",
                "IXIN OS s'installe sur une partie libre du disque. Au démarrage du PC, un menu permet de choisir IXIN OS ou Windows. " +
                "Toute la puissance du PC : idéal pour les jeux et l'usage de tous les jours.", ShowChecks, true));
            col.Controls.Add(ModeCard("Dans une fenêtre de Windows (machine virtuelle)",
                "Le plus simple pour essayer : rien n'est modifié sur le disque ni au démarrage du PC, pas de Secure Boot à désactiver. " +
                "IXIN OS s'ouvre comme une application, depuis une icône du bureau. Plus lent, et sans les jeux exigeants.", ShowVmChecks, false));
            ShowPage(col, Action("Quitter", Close), Action("Aide", () => Open(HelpUrl)));
        }

        private Control ModeCard(string title, string text, Action onClick, bool primary)
        {
            var card = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 8, 0, 4), BackColor = Theme.Card, Cursor = Cursors.Hand,
            };
            var t = Theme.Label(title, Theme.Subtitle, primary ? Theme.Accent : Theme.Text);
            var d = Theme.Label(text, null, Theme.Muted);
            t.MaximumSize = d.MaximumSize = new Size(640, 0);
            card.Controls.Add(t);
            card.Controls.Add(d);
            var go = Action(primary ? "Installer à côté de Windows" : "Utiliser IXIN OS dans une fenêtre", onClick, primary);
            go.Margin = new Padding(0, 4, 0, 0);
            card.Controls.Add(go);
            foreach (Control c in new Control[] { card, t, d }) c.Click += (s, e) => onClick();
            return card;
        }

        // ── machine virtuelle IXIN ────────────────────────────────────────────

        private async void ShowVmChecks()
        {
            ShowPage(Busy("Machine virtuelle IXIN", "Vérification de la mémoire, de l'espace libre et de la virtualisation…"));
            try
            {
                vmFacts = await Task.Run(() => Vm.Collect(vm.Folder));
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
                return;
            }
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("IXIN OS dans une fenêtre", Theme.Title));
            col.Controls.Add(Theme.Label("IXIN OS Setup télécharge IXIN OS et le moteur de la machine virtuelle IXIN (environ 5 Go), crée un disque " +
                                         "virtuel et une icône « IXIN OS » sur le bureau. Rien d'autre n'est installé, et rien n'est modifié sur le disque " +
                                         "de Windows : tout tient dans un dossier, supprimable en un clic.", null, Theme.Muted));
            var checks = Vm.Checks(vmFacts);
            foreach (var c in checks) col.Controls.Add(CheckRow(c));
            bool blocked = checks.Any(c => c.Level == CheckLevel.Blocking);
            bool needPlatform = !vmFacts.PlatformEnabled && (vmFacts.VirtualizationEnabled || vmFacts.HypervisorPresent);
            ShowPage(col,
                needPlatform
                    ? Action("Activer l'accélération et redémarrer", async () => await EnableVmAcceleration(), primary: true, enabled: !blocked)
                    : Action("Préparer IXIN OS", async () => await RunVmPrepare(), primary: true, enabled: !blocked),
                Action("Retour", ShowModeChoice),
                Action("Aide", () => Open(HelpUrl + "#11-dans-une-fenêtre-machine-virtuelle")));
        }

        private async Task EnableVmAcceleration()
        {
            ShowPage(Busy("Activation de l'accélération…", "Ajout de la « Plateforme de l'hyperviseur Windows » (une à deux minutes)."));
            try
            {
                bool restart = await Task.Run(() => Vm.EnablePlatform());
                if (!restart)
                {
                    ShowVmChecks();
                    return;
                }
                File.WriteAllText(Vm.PendingFlag, DateTime.Now.ToString("s"));
                Followup.Arm();
                var col = Theme.Column();
                col.Controls.Add(Theme.Label("Redémarrage nécessaire", Theme.Title, Theme.Accent));
                col.Controls.Add(Theme.Label("Windows doit redémarrer pour activer l'accélération. IXIN OS Setup se rouvrira tout seul ensuite " +
                                             "et terminera la préparation d'IXIN OS."));
                ShowPage(col,
                    Action("Redémarrer maintenant", () => BootEntry.Run("shutdown.exe", "/r /t 5 /c \"Activation de l'accélération pour IXIN OS\""), primary: true),
                    Action("Plus tard", Close));
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        private async Task RunVmPrepare()
        {
            try { File.Delete(Vm.PendingFlag); } catch (IOException) { }
            running = new CancellationTokenSource();
            PrepareView view = null;
            view = ShowPrepareView(() =>
            {
                running?.Cancel();
                view.Step.Text = "Annulation en cours…";
            });
            Label stepLabel = view.Step, detail = view.Detail;
            ProgressBar bar = view.Bar;
            TextBox logBox = view.Log;
            var step = new Progress<string>(m =>
            {
                stepLabel.Text = m;
                logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + m + Environment.NewLine);
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
                await Task.Run(() =>
                {
                    IProgress<string> s = step;
                    s.Report("Recherche de la dernière version d'IXIN OS…");
                    var release = IsoSource.Latest(ct);
                    s.Report("Téléchargement du moteur de la machine virtuelle…");
                    string engine = IsoSource.DownloadAsset(release, Vm.EngineAsset, transfer, ct);
                    s.Report($"Téléchargement d'IXIN OS ({release.Tag}, {Facts.Gb(release.IsoSize)})…");
                    string iso = useLocalIso ? localIsoPath : IsoSource.Download(release, transfer, ct);
                    if (useLocalIso) IsoSource.VerifyLocal(localIsoPath, transfer, ct);
                    ct.ThrowIfCancellationRequested();
                    s.Report("Installation du moteur…");
                    vm.InstallEngine(engine);
                    s.Report("Copie d'IXIN OS dans la machine virtuelle…");
                    vm.PlaceIso(iso);
                    s.Report("Création du disque virtuel…");
                    vm.CreateDisk();
                    s.Report("Création de l'icône « IXIN OS » sur le bureau…");
                    vm.CreateShortcuts(vmFacts.Ram, vmFacts.Cores);
                    if (!useLocalIso) { try { File.Delete(iso); } catch (IOException) { } }
                    try { File.Delete(engine); } catch (IOException) { }
                });
                running = null;
                ShowVmReady(firstTime: true);
            }
            catch (OperationCanceledException)
            {
                running = null;
                ShowMessage("Préparation annulée", "Rien n'a été modifié sur le PC.");
            }
            catch (Exception ex)
            {
                running = null;
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        private void ShowVmReady(bool firstTime)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label(firstTime ? "Tout est prêt !" : "IXIN OS (machine virtuelle)", Theme.Title, Theme.Accent));
            if (firstTime)
            {
                col.Controls.Add(Theme.Label("IXIN OS s'ouvre dans une fenêtre. La première fois, installez-le dans la machine virtuelle (10 à 20 minutes) :"));
                col.Controls.Add(Theme.Label("1.  Cliquez sur « Installer IXIN OS sur ce PC ».\n" +
                                             "2.  À l'étape « Partitions », choisissez « Effacer le disque » : c'est le disque virtuel de 120 Go, " +
                                             "vos fichiers Windows ne sont pas touchés.\n" +
                                             "3.  Choisissez votre nom et votre mot de passe, puis « Installer ».\n" +
                                             "4.  À la fin, redémarrez : IXIN OS démarre désormais directement.", null, Theme.Text));
            }
            col.Controls.Add(Theme.Label("Ensuite, lancez IXIN OS avec l'icône « IXIN OS » du bureau ou du menu Démarrer. Ctrl + Alt + F : plein écran ; " +
                                         "Ctrl + Alt + G : libérer la souris.", Theme.Small, Theme.Muted));
            ShowPage(col,
                Action("Lancer IXIN OS", () => { vm.Launch(); Close(); }, primary: true),
                Action("Plus tard", Close));
        }

        private void ShowVmManage()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("IXIN OS (machine virtuelle)", Theme.Title, Theme.Accent));
            col.Controls.Add(Theme.Label("IXIN OS est prêt dans une machine virtuelle IXIN : lancez-le avec l'icône « IXIN OS » du bureau."));
            col.Controls.Add(Theme.Label("« Installer à côté de Windows » garde la machine virtuelle. « Supprimer » efface la machine virtuelle et " +
                                         "tout ce qu'elle contient (fichiers créés dans IXIN OS compris).", Theme.Small, Theme.Muted));
            ShowPage(col,
                Action("Lancer IXIN OS", () => { vm.Launch(); Close(); }, primary: true),
                Action("Supprimer la machine virtuelle", async () => await RunVmRemove()),
                Action("Installer à côté de Windows", ShowChecks),
                Action("Quitter", Close));
        }

        private async Task RunVmRemove()
        {
            if (MessageBox.Show("Supprimer la machine virtuelle IXIN et tout ce qu'elle contient ?\n\nLes fichiers créés dans IXIN OS seront perdus.",
                    "IXIN OS Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ShowPage(Busy("Suppression de la machine virtuelle…", vm.Folder));
            try
            {
                await Task.Run(() => vm.Remove());
                ShowMessage("C'est fait", "La machine virtuelle IXIN a été supprimée.");
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
            ShowPage(Busy("IXIN OS Setup", "Vérification de l'installation existante…"));
            int disk = state?.DiskNumber ?? facts.DiskNumber;
            string letter = state?.DriveLetter ?? facts.DriveLetter;
            bool hasSetup;
            long reclaimable;
            try
            {
                hasSetup = await Task.Run(() => Storage.FindSetupPartitions().Any(p => p.Disk == disk));
                reclaimable = hasSetup ? 0 : await Task.Run(() => Storage.FreeAfter(letter));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return;
            }
            // IXIN OS installé (ou partition d'installation déjà supprimée) : jamais « Annuler la préparation »,
            // qui rétablirait par exemple le démarrage rapide de Windows sous un double démarrage.
            if (ixinEntries.Count > 0 || !hasSetup)
            {
                ShowInstalled(hasSetup, reclaimable);
                return;
            }

            ShowPrepared();
        }

        private void ShowPrepared()
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("L'installation d'IXIN OS est prête", Theme.Title, Theme.Accent));
            col.Controls.Add(Theme.Label($"Préparée le {state.Created.Replace('T', ' ')} — {Facts.Gb(state.IxinSize)} libres pour IXIN OS."));
            if (MokFlow)
                col.Controls.Add(Theme.Label("Secure Boot est activé : au redémarrage, autorisez une fois IXIN OS sur l'écran bleu (mot de passe " +
                                             Mok.Password + ").", null, Theme.Muted));
            else if (facts.SecureBoot == true)
                col.Controls.Add(Theme.Label("Secure Boot est encore activé : désactivez-le dans les paramètres UEFI pour pouvoir démarrer l'installation.", null, Theme.Warning));
            col.Controls.Add(Theme.Label("Dans l'installation, à l'étape « Partitions », choisissez « Remplacer une partition » puis la zone « Espace libre ».", Theme.Small, Theme.Muted));
            col.Controls.Add(Theme.Label("« Annuler la préparation » supprime tout ce qu'IXIN OS Setup a ajouté et rend l'espace à Windows.", Theme.Small, Theme.Muted));
            ShowPage(col,
                MokFlow
                    ? Action("Redémarrer et installer IXIN OS", RestartWithMok, primary: true)
                    : facts.SecureBoot == true
                        ? Action("Redémarrer vers les paramètres UEFI", () => Restart(true), primary: true)
                        : Action("Redémarrer et installer IXIN OS", () => Restart(false), primary: true),
                Action("Annuler la préparation", async () => await RunUndo()),
                Action("Quitter", Close));
        }

        private void ShowInstalled(bool setupLeft, long reclaimable)
        {
            var col = Theme.Column();
            col.Controls.Add(Theme.Label("IXIN OS est installé", Theme.Title, Theme.Accent));
            col.Controls.Add(Theme.Label("Au démarrage du PC, le menu IXIN permet de choisir IXIN OS ou Windows."));
            var buttons = new List<Button>();
            if (setupLeft && state?.KeepSetupPartition == true)
            {
                col.Controls.Add(Theme.Label("La partition d'installation IXIN-SETUP est gardée (option avancée) : « IXIN OS Setup » reste " +
                                             "dans le menu de démarrage du PC pour réinstaller IXIN OS.", null, Theme.Muted));
                buttons.Add(Action("Supprimer IXIN-SETUP", async () => await RunRemoveSetup(), primary: true));
            }
            else if (setupLeft)
            {
                col.Controls.Add(Theme.Label("La partition d'installation (IXIN-SETUP) sera supprimée au premier démarrage d'IXIN OS ; " +
                                             "relancez ensuite IXIN OS Setup pour rendre cet espace à Windows.", null, Theme.Muted));
            }
            else if (reclaimable >= Plan.GiB)
            {
                col.Controls.Add(Theme.Label($"{Facts.Gb(reclaimable)} libres se trouvent juste après Windows : vous pouvez les lui rendre.", null, Theme.Muted));
                buttons.Add(Action($"Rendre {Facts.Gb(reclaimable)} à Windows", async () => await RunReclaim(), primary: true));
            }
            if (ixinEntries.Count > 0)
            {
                col.Controls.Add(Theme.Label("Le PC démarre directement sur Windows, sans afficher le menu IXIN ? Cela peut arriver après " +
                                             "une mise à jour de Windows : cliquez sur « Remettre le menu IXIN au démarrage ».", Theme.Small, Theme.Muted));
                buttons.Add(Action("Remettre le menu IXIN au démarrage", RestoreIxinMenu, primary: buttons.Count == 0));
            }
            if (state != null && !setupLeft && reclaimable < Plan.GiB)
                buttons.Add(Action("Terminer", () => { SetupState.Delete(); Close(); }, primary: buttons.Count == 0));
            buttons.Add(Action("Quitter", Close));
            ShowPage(col, buttons.ToArray());
        }

        private void RestoreIxinMenu()
        {
            try
            {
                BootEntry.PutFirst(ixinEntries[0]);
                MessageBox.Show("C'est fait : au prochain démarrage, le menu IXIN s'affichera de nouveau (IXIN OS ou Windows).",
                    "IXIN OS Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        // ── captures d'écran (données d'exemple, rien n'est lu ni modifié sur le PC) ──

        internal void Demo(string page, Facts sample, SetupState sampleState = null,
                           List<string> sampleIxinEntries = null, List<RecoveryKey> sampleKeys = null)
        {
            facts = sample;
            state = sampleState;
            ixinEntries = sampleIxinEntries ?? new List<string>();
            switch (page)
            {
                case "analyse":
                    ShowPage(Busy("Analyse du PC…", "Lecture du disque de Windows, de BitLocker et de Secure Boot (jusqu'à une minute)."));
                    break;
                case "verification":
                    ShowChecks();
                    break;
                case "espace":
                    ShowOptions();
                    break;
                case "avancees":
                    ShowAdvanced();
                    break;
                case "bitlocker":
                    ShowBitLockerKeys(sampleKeys ?? new List<RecoveryKey>());
                    break;
                case "confirmation":
                    ShowConfirm();
                    break;
                case "preparation":
                    var v = ShowPrepareView(() => { });
                    v.Step.Text = "Téléchargement d'IXIN OS (build-2026.10.15-3, 4,7 Go)…";
                    v.Bar.Value = 430;
                    v.Detail.Text = "Téléchargement : 2 Go sur 4,7 Go — 24,6 Mo/s — encore environ 2 min";
                    v.Log.Text = "21:30:02  Recherche de la dernière version d'IXIN OS…" + Environment.NewLine +
                                 "21:30:03  Téléchargement d'IXIN OS (build-2026.10.15-3, 4,7 Go)…" + Environment.NewLine;
                    break;
                case "terminee":
                    DemoSecureBootCapable = null;
                    ShowDone();
                    break;
                case "terminee-mok":
                    DemoSecureBootCapable = true;
                    ShowDone();
                    DemoSecureBootCapable = null;
                    break;
                case "prete":
                    ShowPrepared();
                    break;
                case "installee":
                    ShowInstalled(false, 6 * Plan.GiB);
                    break;
                case "choix":
                    ShowModeChoice();
                    break;
                case "vm-prete":
                    ShowVmReady(firstTime: true);
                    break;
                case "vm-verification":
                    vmFacts = new VmFacts { Ram = 16 * Plan.GiB, Cores = 8, VirtualizationEnabled = true, PlatformState = "Disabled", Free = 300 * Plan.GiB };
                    {
                        var col = Theme.Column();
                        col.Controls.Add(Theme.Label("IXIN OS dans une fenêtre", Theme.Title));
                        foreach (var c in Vm.Checks(vmFacts)) col.Controls.Add(CheckRow(c));
                        ShowPage(col, Action("Activer l'accélération et redémarrer", () => { }, primary: true), Action("Retour", () => { }));
                    }
                    break;
                case "retour-windows":
                    ShowReturned();
                    break;
                case "incompatible":
                    ShowUnsupported(Compat.Check("ARM64", true, 26100));
                    break;
                default:
                    throw new ArgumentException("page inconnue : " + page);
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
                await Task.Run(() =>
                {
                    Installer.Undo(state, step);
                    Followup.Disarm();
                });
                state = null;
                ShowMessage("Préparation annulée", "Windows est dans son état d'origine : l'espace a été rendu et le menu de démarrage retiré.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowError(ex.Message);
            }
        }

        private async Task RunRemoveSetup()
        {
            if (MessageBox.Show("Supprimer la partition IXIN-SETUP et l'entrée « IXIN OS Setup » du menu de démarrage, " +
                                "puis rendre cet espace à Windows ? IXIN OS n'est pas modifié.",
                    "IXIN OS Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ShowPage(Busy("Suppression de la partition d'installation…", "IXIN OS et Windows ne sont pas modifiés."));
            try
            {
                await Task.Run(() => Installer.RemoveSetupAfterInstall(state, new Progress<string>(s => Log.Info(s))));
                state = null;
                ShowMessage("C'est fait", "La partition IXIN-SETUP a été supprimée et son espace rendu à Windows.");
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
                    Storage.ExtendToMax(state?.DriveLetter ?? facts.DriveLetter);
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
