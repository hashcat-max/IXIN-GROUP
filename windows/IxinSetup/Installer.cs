using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace IxinSetup
{
    internal enum CheckLevel { Ok, Info, Warning, Blocking }

    internal sealed class Check
    {
        public CheckLevel Level;
        public string Title;
        public string Detail;
        public bool UnmovableFiles;   // réduction limitée par des fichiers système : « Débloquer la réduction » proposé

        public Check(CheckLevel level, string title, string detail = null)
        {
            Level = level;
            Title = title;
            Detail = detail;
        }
    }

    internal static class Plan
    {
        public const long GiB = Facts.GiB;
        public const long MinIxin = 45 * GiB;          // l'installeur d'IXIN OS exige 40 Go
        public const long DefaultIxin = 80 * GiB;
        public const long KeepWindowsFree = 20 * GiB;   // espace libre laissé à Windows
        public const long DefaultIsoSize = 5L * GiB;    // estimation avant de connaître la version

        public static long SetupSizeFor(long isoSize) =>
            Math.Max(6 * GiB, Storage.AlignUp(isoSize + 512 * Storage.MiB, GiB));

        /// <summary>Taille maximale pour IXIN OS (ISO téléchargée temporairement sur Windows comprise).</summary>
        public static long MaxIxin(Facts f, long isoSize, bool isoOnWindows)
        {
            long setup = SetupSizeFor(isoSize);
            long byFs = f.MaxShrinkByFs;
            long byFree = f.Free - KeepWindowsFree - (isoOnWindows ? isoSize : 0);
            return Storage.AlignDown(Math.Max(0, Math.Min(byFs, byFree) - setup), GiB);
        }

        public static List<Check> Checks(Facts f, SetupState existing)
        {
            var list = new List<Check>();
            if (!f.IsUefi)
            {
                list.Add(new Check(CheckLevel.Blocking, "Windows démarre en mode BIOS (Legacy)",
                    "IXIN OS Setup fonctionne uniquement avec un démarrage UEFI. Installez IXIN OS avec une clé USB (Rufus, mode GPT/UEFI)."));
                return list;
            }
            list.Add(new Check(CheckLevel.Ok, "PC compatible (UEFI 64 bits)", f.WindowsName));
            if (f.Problem != null)
            {
                list.Add(new Check(CheckLevel.Blocking, "Disque de Windows illisible", f.Problem));
                return list;
            }
            if (!string.Equals(f.PartitionStyle, "GPT", StringComparison.OrdinalIgnoreCase))
                list.Add(new Check(CheckLevel.Blocking, "Disque au format MBR",
                    "Le disque de Windows doit être au format GPT. Installez IXIN OS avec une clé USB."));
            else if (f.EspPartitionNumber == 0)
                list.Add(new Check(CheckLevel.Blocking, "Partition EFI introuvable sur le disque de Windows",
                    "Configuration non prise en charge : installez IXIN OS avec une clé USB."));
            else
                list.Add(new Check(CheckLevel.Ok, $"Disque GPT : {f.DiskName}", Facts.Gb(f.DiskSize)));

            if (f.BitLocker.InProgress)
                list.Add(new Check(CheckLevel.Blocking, "Chiffrement BitLocker en cours",
                    "Attendez la fin du chiffrement (ou du déchiffrement) de Windows, puis relancez IXIN OS Setup."));

            bool prepared = existing != null && existing.Completed;
            if (!prepared)
            {
                long max = MaxIxin(f, DefaultIsoSize, true);
                if (max < MinIxin)
                {
                    long shrinkNeeded = MinIxin + SetupSizeFor(DefaultIsoSize);
                    long needed = shrinkNeeded + KeepWindowsFree + DefaultIsoSize;
                    if (f.Free >= needed)
                        list.Add(new Check(CheckLevel.Blocking, f.DriveLetter + ": ne peut pas être réduit assez",
                            $"Windows a assez d'espace libre ({Facts.Gb(f.Free)}), mais des fichiers système impossibles à déplacer " +
                            "(mise en veille prolongée, fichier d'échange, points de restauration) se trouvent en fin de disque : " +
                            $"Windows ne peut être réduit que de {Facts.Gb(f.MaxShrinkByFs)} (il faut {Facts.Gb(shrinkNeeded)}). " +
                            "Cliquez sur « Débloquer la réduction » ; si cela ne suffit pas, l'aide explique les étapes suivantes.")
                        { UnmovableFiles = true });
                    else
                        list.Add(new Check(CheckLevel.Blocking, "Espace libre insuffisant sur " + f.DriveLetter + ":",
                            $"Il faut environ {Facts.Gb(needed)} libres sur Windows ({Facts.Gb(f.Free)} actuellement). " +
                            "Libérez de la place (Paramètres → Système → Stockage), puis relancez IXIN OS Setup."));
                }
                else
                {
                    list.Add(new Check(CheckLevel.Ok, "Espace disponible pour IXIN OS", "jusqu'à " + Facts.Gb(max)));
                }
            }

            if (f.SecureBoot == true)
                list.Add(new Check(CheckLevel.Warning, "Secure Boot est activé",
                    "IXIN OS ne démarre pas encore avec Secure Boot : il faudra le désactiver dans les paramètres UEFI " +
                    "(IXIN OS Setup y redémarre pour vous). Attention : certains jeux Windows avec anti-triche " +
                    "(Valorant, Battlefield 6…) exigent Secure Boot."));
            if (f.BitLocker.Encrypted)
                list.Add(new Check(CheckLevel.Warning, "Windows est chiffré (BitLocker)",
                    "Vous aurez besoin de votre clé de récupération : IXIN OS Setup vous la montre avant de commencer."));
            if (f.OnBattery)
                list.Add(new Check(f.BatteryPercent < 30 ? CheckLevel.Blocking : CheckLevel.Warning, $"Sur batterie ({f.BatteryPercent} %)",
                    "Branchez le chargeur : une coupure pendant la préparation est à éviter."));
            if (prepared)
                list.Add(new Check(CheckLevel.Info, "Installation déjà préparée",
                    $"Espace pour IXIN OS : {Facts.Gb(existing.IxinSize)} — préparé le {existing.Created.Replace('T', ' ')}"));
            return list;
        }
    }

    internal static class Installer
    {
        public static SetupState Prepare(Facts f, long ixinSize, string isoPath, string isoSha256,
                                         IProgress<string> step, IProgress<Transfer> copy, CancellationToken ct,
                                         bool testMode = false, string testEspLetter = null)
        {
            long isoSize = new FileInfo(isoPath).Length;
            long setupSize = Plan.SetupSizeFor(isoSize);
            long shrink = setupSize + ixinSize;
            if (shrink > f.MaxShrinkByFs)
                throw new SetupException($"Windows ne peut être réduit que de {Facts.Gb(f.MaxShrinkByFs)}.");

            var st = new SetupState
            {
                DriveLetter = f.DriveLetter,
                DiskNumber = f.DiskNumber,
                OriginalSize = f.Size,
                IxinSize = ixinSize,
                IsoName = Path.GetFileName(isoPath),
                IsoSha256 = isoSha256,
            };
            st.Save();
            Log.Info($"Préparation : IXIN OS {Facts.Gb(ixinSize)}, partition d'installation {Facts.Gb(setupSize)}, " +
                     $"Windows réduit de {Facts.Gb(shrink)}{(testMode ? " (mode test)" : "")}");
            try
            {
                if (!testMode && f.BitLocker.ProtectionOn)
                {
                    step.Report("Suspension temporaire de BitLocker…");
                    BitLocker.Suspend(f.DriveLetter, 3);
                    st.BitLockerSuspended = true;
                    st.Save();
                }

                ct.ThrowIfCancellationRequested();
                step.Report($"Réduction de Windows ({f.DriveLetter}:) de {Facts.Gb(shrink)}… (quelques minutes)");
                long newSize = Storage.AlignDown(f.Size - shrink, Storage.MiB);
                long size = Storage.Shrink(f.DriveLetter, newSize);
                st.ShrunkBy = f.Size - size;
                st.Save();

                step.Report("Création de la partition d'installation IXIN-SETUP…");
                var part = Storage.CreateSetupPartition(f.DiskNumber, Storage.AlignUp(f.Offset + size, Storage.MiB), setupSize);
                st.SetupPartitionNumber = part.Number;
                st.SetupPartitionOffset = part.Offset;
                st.SetupPartitionSize = part.Size;
                st.Save();
                if (string.IsNullOrEmpty(part.DriveLetter))
                    throw new SetupException("La partition IXIN-SETUP n'a pas reçu de lettre de lecteur.");

                step.Report("Copie d'IXIN OS dans la partition d'installation…");
                string dest = part.DriveLetter + @":\ixin-os.iso";
                Copy(isoPath, dest + ".tmp", copy, ct);
                step.Report("Vérification de la copie…");
                if (IsoSource.Sha256(dest + ".tmp", copy, ct) != isoSha256)
                    throw new SetupException("La copie de l'ISO est incorrecte (problème de disque ?).");
                File.Move(dest + ".tmp", dest);
                File.WriteAllText(part.DriveLetter + @":\LISEZMOI.txt",
                    "Partition d'installation d'IXIN OS, créée par IXIN OS Setup.\r\n" +
                    "Elle est supprimée automatiquement au premier démarrage d'IXIN OS une fois installé,\r\n" +
                    "ou par « Annuler la préparation » dans IXIN OS Setup.\r\n", Encoding.UTF8);
                Storage.RemoveDriveLetter(part);

                step.Report("Ajout du menu de démarrage dans la partition EFI…");
                string esp = testMode ? testEspLetter : BootEntry.MountSystemEsp();
                try
                {
                    BootEntry.InstallLoader(esp);
                    st.LoaderInstalled = true;
                    st.Save();
                }
                finally
                {
                    if (!testMode) BootEntry.Unmount(esp);
                }

                if (!testMode)
                {
                    step.Report("Ajout de « IXIN OS Setup » au menu de démarrage du PC…");
                    st.BootEntryId = BootEntry.Create();
                    st.Save();
                    WindowsSettings.Apply(st);
                }
                st.Completed = true;
                st.Save();
                step.Report("Préparation terminée.");
                Log.Info("Préparation terminée");
                return st;
            }
            catch (Exception ex)
            {
                bool cancelled = ex is OperationCanceledException;
                Log.Error((cancelled ? "Préparation annulée" : "Préparation interrompue") + " : " + ex);
                step.Report("Remise en état de Windows…");
                try
                {
                    Undo(st, step, testMode, testEspLetter);
                }
                catch (Exception undoEx)
                {
                    Log.Error("Remise en état incomplète : " + undoEx);
                    throw new SetupException((cancelled ? "Préparation annulée." : ex.Message) +
                        "\n\nLa remise en état automatique est incomplète : relancez IXIN OS Setup et choisissez « Annuler la préparation ».", ex);
                }
                if (cancelled) throw;
                throw new SetupException(ex.Message + "\n\nWindows a été remis dans son état d'origine.", ex);
            }
        }

        /// <summary>Supprime tout ce que IXIN OS Setup a ajouté et rend l'espace à Windows.</summary>
        public static void Undo(SetupState st, IProgress<string> step, bool testMode = false, string testEspLetter = null)
        {
            string letter = st?.DriveLetter ?? "C";
            if (!testMode)
            {
                step?.Report("Retrait de « IXIN OS Setup » du menu de démarrage…");
                foreach (var id in BootEntry.Find()) BootEntry.Delete(id);
            }

            step?.Report("Retrait du chargeur de la partition EFI…");
            string esp = testMode ? testEspLetter : BootEntry.MountSystemEsp();
            try
            {
                BootEntry.RemoveLoader(esp);
            }
            finally
            {
                if (!testMode) BootEntry.Unmount(esp);
            }

            foreach (var p in Storage.FindSetupPartitions())
            {
                if (st != null && st.DiskNumber >= 0 && p.Disk != st.DiskNumber) continue;
                step?.Report("Suppression de la partition IXIN-SETUP…");
                Storage.DeletePartition(p);
                Log.Info($"Partition IXIN-SETUP supprimée (disque {p.Disk}, partition {p.Number})");
            }

            step?.Report($"Windows ({letter}:) récupère l'espace libéré…");
            long size = Storage.ExtendToMax(letter);
            Log.Info($"Taille de {letter}: après remise en état : {Facts.Gb(size)}");

            if (!testMode && st != null)
            {
                if (st.PreviousHiberboot.HasValue || st.PreviousRtcUtc.HasValue || st.Completed)
                    WindowsSettings.Restore(st);
                if (st.BitLockerSuspended) BitLocker.Resume(letter);
            }
            SetupState.Delete();
            step?.Report("IXIN OS Setup a tout annulé : Windows est dans son état d'origine.");
        }

        /// <summary>Redémarre sur l'installation (une seule fois), ou d'abord vers les paramètres UEFI.</summary>
        public static void Restart(SetupState st, bool toFirmwareSettings)
        {
            var ids = BootEntry.Find();
            string id = ids.Count > 0 ? ids[0] : null;
            if (id == null)
            {
                id = BootEntry.Create();
                st.BootEntryId = id;
                st.Save();
            }
            BootEntry.BootNextOnce(id);
            BootEntry.Restart(toFirmwareSettings);
        }

        private static void Copy(string source, string destination, IProgress<Transfer> progress, CancellationToken ct)
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            {
                var buffer = new byte[4 << 20];
                long done = 0, total = input.Length;
                var started = DateTime.UtcNow;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, n);
                    done += n;
                    progress?.Report(new Transfer
                    {
                        Stage = "Copie d'IXIN OS",
                        Done = done,
                        Total = total,
                        BytesPerSecond = done / Math.Max(1, (DateTime.UtcNow - started).TotalSeconds),
                    });
                }
                output.Flush(true);
            }
        }
    }
}
