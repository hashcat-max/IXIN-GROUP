using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace IxinSetup
{
    /// <summary>
    /// Captures d'écran des pages d'IXIN OS Setup pour le site et la documentation, avec des
    /// données d'exemple (PC Windows 11 chiffré par BitLocker, Secure Boot activé) : rien n'est lu
    /// ni modifié sur le PC.   IXIN-OS-Setup.exe --screenshots [dossier]
    /// </summary>
    internal static class Screenshots
    {
        private const long GiB = Facts.GiB;

        public static int Run(string dir)
        {
            dir = Path.GetFullPath(string.IsNullOrEmpty(dir) ? "captures" : dir);
            Directory.CreateDirectory(dir);
            // Comme sur le PC d'un utilisateur français (virgule décimale)
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var keys = new List<RecoveryKey>
            {
                new RecoveryKey { Id = "{3F2A9C41-7B0E-4D8A-9E61-2C5B7A904D13}", Password = "123456-234567-345678-456789-567890-678901-789012-890123" },
            };
            var installed = new SetupState { Completed = true, IxinSize = 120 * GiB, Created = "2026-10-15T21:42:00" };
            var prepared = new SetupState { Completed = true, IxinSize = 120 * GiB, Created = "2026-10-15T21:42:00" };
            var returned = new SetupState { Completed = true, IxinSize = 120 * GiB, Created = "2026-10-15T21:42:00", RestartTries = 1 };
            var ixinEntry = new List<string> { "{8c2f41d0-12ab-11f1-b614-70a8a569ed22}" };

            var pages = new[]
            {
                new Shot("1-analyse", "analyse", Pc(), null, null),
                new Shot("2-verification", "verification", Pc(), null, null),
                new Shot("3-espace", "espace", Pc(), null, null),
                new Shot("3b-options-avancees", "avancees", Pc(), null, null),
                new Shot("4-cle-bitlocker", "bitlocker", Pc(), null, null),
                new Shot("5-confirmation", "confirmation", Pc(), null, null),
                new Shot("6-preparation", "preparation", Pc(), null, null),
                new Shot("7-secure-boot", "terminee", Pc(), null, null),
                new Shot("8-tout-est-pret", "terminee", Pc(secureBoot: false), null, null),
                new Shot("9-installation-prete", "prete", Pc(secureBoot: false), prepared, null),
                new Shot("10-ixin-installe", "installee", Pc(secureBoot: false), installed, ixinEntry),
                new Shot("reduction-bloquee", "verification", Pc(shrinkBlocked: true), null, null),
                new Shot("efi-autre-disque", "verification", Pc(secureBoot: false, espOnOtherDisk: true), null, null),
                new Shot("retour-windows", "retour-windows", Pc(secureBoot: false), returned, null),
                new Shot("retour-windows-secure-boot", "retour-windows", Pc(), returned, null),
                new Shot("pc-incompatible", "incompatible", Pc(), null, null),
            };

            using (var form = new MainForm(analyse: false) { DemoManufacturer = "LENOVO" })
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(40, 40);
                form.Show();
                foreach (var p in pages)
                {
                    form.Demo(p.Page, p.Pc, p.State, p.Ixin, keys);
                    for (int i = 0; i < 5; i++) Application.DoEvents();
                    form.Refresh();
                    string path = Path.Combine(dir, "ixin-os-setup-" + p.File + ".png");
                    Save(form, path);
                    Console.WriteLine("capture : " + path);
                }
                form.Hide();
            }
            return 0;
        }

        /// <summary>
        /// PC d'exemple : Windows 11, SSD de 1 To, C: avec 412 Go libres, BitLocker actif ; sa partition
        /// EFI est sur le SSD, ou sur un second disque (PC installé avec ses deux disques branchés).
        /// </summary>
        private static Facts Pc(bool secureBoot = true, bool shrinkBlocked = false, bool espOnOtherDisk = false)
        {
            return new Facts
            {
                DriveLetter = "C",
                IsUefi = true,
                SecureBoot = secureBoot,
                DiskNumber = 0,
                PartitionNumber = 3,
                PartitionStyle = "GPT",
                DiskName = "SSD NVMe 1 To",
                DiskSize = 1000204886016,
                Offset = 117L * 1024 * 1024,
                Size = 930 * GiB,
                Free = 412 * GiB,
                // Réduction limitée par des fichiers système en fin de disque (cas « Débloquer la réduction »)
                SizeMin = shrinkBlocked ? 925 * GiB : 520 * GiB,
                Esps = new List<EspInfo>
                {
                    new EspInfo
                    {
                        Disk = espOnOtherDisk ? 1 : 0, Number = 1, Offset = 1L << 20, Size = 100L << 20, Free = 71L << 20,
                        Guid = "{5c3e8a1f-2b7d-4e90-a6c1-3f8d2e7b9a04}", FileSystem = "FAT32", IsSystem = true, EspType = true,
                        DiskName = espOnOtherDisk ? "Disque dur 2 To" : "SSD NVMe 1 To",
                    },
                },
                WindowsName = "Windows 11 Professional 24H2 (build 26100)",
                HasBattery = true,
                OnBattery = false,
                BatteryPercent = 87,
                BitLocker = new BitLockerInfo { Available = true, ProtectionStatus = 1, ConversionStatus = 1 },
            };
        }

        /// <summary>Enregistre la zone cliente de la fenêtre (sans la barre de titre).</summary>
        private static void Save(Form form, string path)
        {
            using (var whole = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(whole, new Rectangle(Point.Empty, form.Size));
                Point client = form.PointToScreen(Point.Empty);
                var area = new Rectangle(client.X - form.Left, client.Y - form.Top, form.ClientSize.Width, form.ClientSize.Height);
                using (var page = whole.Clone(area, whole.PixelFormat))
                {
                    page.Save(path, ImageFormat.Png);
                }
            }
        }
    }

    internal sealed class Shot
    {
        public readonly string File, Page;
        public readonly Facts Pc;
        public readonly SetupState State;
        public readonly List<string> Ixin;

        public Shot(string file, string page, Facts pc, SetupState state, List<string> ixin)
        {
            File = file;
            Page = page;
            Pc = pc;
            State = state;
            Ixin = ixin;
        }
    }
}
