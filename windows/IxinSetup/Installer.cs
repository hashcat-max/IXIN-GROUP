using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Où IXIN OS Setup (puis IXIN OS) range son chargeur de démarrage.</summary>
    internal enum EspMode
    {
        Windows,    // partition EFI qui démarre Windows, même sur un autre disque
        Existing,   // autre partition EFI choisie dans les options avancées
        Create,     // partition EFI dédiée, créée par IXIN OS Setup
    }

    internal sealed class EspChoice
    {
        public EspMode Mode;
        public EspInfo Esp;     // Windows / Existing
        public string Why;      // Create : pourquoi une partition est créée
    }

    /// <summary>Réglages du parcours, modifiables dans les options avancées.</summary>
    internal sealed class SetupOptions
    {
        public const string NewEsp = "new";

        public string Esp;                      // null : automatique ; NewEsp : partition dédiée ; sinon Guid d'une partition EFI
        public bool KeepSetupPartition;         // garder IXIN-SETUP après l'installation
        public bool DisableFastStartup = true;
        public bool UtcClock = true;
    }

    internal static class Plan
    {
        public const long GiB = Facts.GiB;
        public const long MinIxin = 45 * GiB;          // l'installeur d'IXIN OS exige 40 Go
        public const long DefaultIxin = 80 * GiB;
        public const long KeepWindowsFree = 20 * GiB;   // espace libre laissé à Windows
        public const long DefaultIsoSize = 5L * GiB;    // estimation avant de connaître la version
        public const long NewEspSize = 300 * Storage.MiB;     // FAT32 valable même sur disque à secteurs de 4 Ko (≥ 260 Mo)
        public const long EspFreeNeeded = 20 * Storage.MiB;   // chargeurs d'IXIN OS Setup et d'IXIN OS (≈ 5 Mo) + marge pour Windows

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

        /// <summary>Partition EFI utilisable telle quelle : FAT, pas sur un disque USB, assez de place.</summary>
        public static bool Usable(EspInfo e) =>
            e != null && e.IsFat && !e.Removable && (e.Free < 0 || e.Free >= EspFreeNeeded);

        /// <summary>
        /// Choix de la partition EFI : celle qui démarre Windows, où qu'elle soit (c'est le cas
        /// normal) ; sinon une partition EFI dédiée est créée, prise sur l'espace d'IXIN OS.
        /// </summary>
        public static EspChoice ChooseEsp(Facts f, string preference = null)
        {
            if (preference == SetupOptions.NewEsp)
                return new EspChoice { Mode = EspMode.Create, Why = "partition EFI dédiée choisie dans les options avancées" };
            if (!string.IsNullOrEmpty(preference))
            {
                var chosen = f.Esps.FirstOrDefault(e => string.Equals(e.Guid, preference, StringComparison.OrdinalIgnoreCase));
                if (chosen != null && chosen.IsFat)
                    return new EspChoice { Mode = chosen.IsSystem ? EspMode.Windows : EspMode.Existing, Esp = chosen };
                Log.Warn("Partition EFI choisie introuvable (" + preference + ") : choix automatique");
            }
            var system = f.Esps.FirstOrDefault(e => e.IsSystem && e.IsFat);
            if (Usable(system))
                return new EspChoice { Mode = EspMode.Windows, Esp = system };
            if (system == null)
            {
                // Windows n'indique pas sa partition EFI : une seule partition EFI sur son disque, c'est elle
                var onDisk = f.Esps.Where(e => e.Disk == f.DiskNumber && e.EspType && e.IsFat).ToList();
                if (onDisk.Count == 1 && Usable(onDisk[0]))
                    return new EspChoice { Mode = EspMode.Existing, Esp = onDisk[0] };
            }
            string why = system == null ? "aucune partition EFI utilisable n'a été trouvée"
                : system.Removable ? "la partition EFI de Windows est sur un disque USB"
                : "la partition EFI de Windows est presque pleine";
            return new EspChoice { Mode = EspMode.Create, Why = why };
        }

        public static List<Check> Checks(Facts f, SetupState existing)
        {
            var list = new List<Check>();
            if (!f.IsUefi)
            {
                list.Add(new Check(CheckLevel.Blocking, "Ce PC démarre en mode ancien (BIOS)",
                    "IXIN OS Setup a besoin d'un démarrage UEFI, que ce PC n'utilise pas actuellement. Installez IXIN OS avec une " +
                    "clé USB : le bouton « Aide » explique comment, pas à pas."));
                return list;
            }
            list.Add(new Check(CheckLevel.Ok, "PC compatible (UEFI 64 bits)", f.WindowsName));
            if (f.Problem != null)
            {
                list.Add(new Check(CheckLevel.Blocking, "Disque de Windows illisible", f.Problem));
                return list;
            }
            if (!string.Equals(f.PartitionStyle, "GPT", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Check(CheckLevel.Blocking, "Disque au format MBR",
                    "Le disque de Windows doit être au format GPT. Installez IXIN OS avec une clé USB."));
            }
            else
            {
                list.Add(new Check(CheckLevel.Ok, $"Disque GPT : {f.DiskName}", Facts.Gb(f.DiskSize)));
                var esp = ChooseEsp(f);
                if (esp.Mode != EspMode.Create && esp.Esp.Disk != f.DiskNumber)
                    list.Add(new Check(CheckLevel.Info, "Partition de démarrage (EFI) sur un autre disque",
                        $"Windows démarre depuis la partition EFI du disque {esp.Esp.Disk}" +
                        (string.IsNullOrEmpty(esp.Esp.DiskName) ? "" : $" ({esp.Esp.DiskName})") +
                        " : IXIN OS l'utilisera aussi. Ne retirez pas ce disque du PC."));
                else if (esp.Mode == EspMode.Create)
                    list.Add(new Check(CheckLevel.Info, "Une partition de démarrage (EFI) sera créée pour IXIN OS",
                        char.ToUpperInvariant(esp.Why[0]) + esp.Why.Substring(1) +
                        $" : IXIN OS Setup crée automatiquement une partition EFI de {Facts.Mb(NewEspSize)}, prise sur l'espace d'IXIN OS."));
            }

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
        public static SetupState Prepare(Facts f, long ixinSize, string isoPath, string isoSha256, SetupOptions options,
                                         IProgress<string> step, IProgress<Transfer> copy, CancellationToken ct,
                                         bool testMode = false, string testEspLetter = null)
        {
            options = options ?? new SetupOptions();
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
                KeepSetupPartition = options.KeepSetupPartition,
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
                var esp = OpenEsp(f, options.Esp, st, part, step, testMode, testEspLetter);
                try
                {
                    BootEntry.InstallLoader(esp.Letter, BootEntry.OptionsContent(esp.Guid, options.KeepSetupPartition));
                    st.LoaderInstalled = true;
                    st.Save();
                    if (!testMode)
                    {
                        step.Report("Ajout de « IXIN OS Setup » au menu de démarrage du PC…");
                        st.BootEntryId = BootEntry.Create(deviceLetter: esp.System ? null : esp.Letter);
                        st.Save();
                    }
                }
                finally
                {
                    if (esp.Mounted) BootEntry.Unmount(esp.Letter);
                }

                if (!testMode) WindowsSettings.Apply(st, options);
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

        private sealed class EspAccess
        {
            public string Letter;
            public string Guid;
            public bool System;     // partition qui démarre Windows : l'entrée copiée de Windows y pointe déjà
            public bool Mounted;    // lettre ajoutée par IXIN OS Setup, à retirer ensuite
        }

        /// <summary>
        /// Accès à la partition EFI qui recevra le chargeur : celle choisie par Plan.ChooseEsp, ou une
        /// partition EFI dédiée créée juste après IXIN-SETUP (sur l'espace d'IXIN OS) si aucune ne
        /// convient — y compris quand la partition prévue s'avère pleine ou inaccessible.
        /// </summary>
        private static EspAccess OpenEsp(Facts f, string preference, SetupState st, PartitionRef setupPart,
                                         IProgress<string> step, bool testMode, string testEspLetter)
        {
            var choice = Plan.ChooseEsp(f, preference);
            if (choice.Mode != EspMode.Create)
            {
                var esp = choice.Esp;
                EspAccess access = null;
                try
                {
                    if (choice.Mode == EspMode.Windows)
                        access = testMode
                            ? new EspAccess { Letter = testEspLetter, System = true }
                            : new EspAccess { Letter = BootEntry.MountSystemEsp(), System = true, Mounted = true };
                    else
                        access = new EspAccess { Letter = Storage.MountEsp(esp.Disk, esp.Number, esp.VolumePath), Mounted = true };
                    access.Guid = esp.Guid;
                    long free = new DriveInfo(access.Letter).AvailableFreeSpace;
                    if (free >= Plan.EspFreeNeeded || Directory.Exists(access.Letter + @":\" + BootEntry.EspDirectory))
                    {
                        st.EspMode = choice.Mode.ToString();
                        st.EspGuid = esp.Guid;
                        st.EspDisk = esp.Disk;
                        st.EspNumber = esp.Number;
                        st.EspVolumePath = esp.VolumePath;
                        st.Save();
                        Log.Info("Partition EFI utilisée : " + esp.Describe());
                        return access;
                    }
                    Log.Warn($"Partition EFI {esp.Describe()} : {Facts.Mb(free)} libres seulement");
                }
                catch (Exception ex) when (ex is SetupException || ex is IOException || ex is ArgumentException)
                {
                    Log.Warn($"Partition EFI {esp.Describe()} inaccessible : {ex.Message}");
                }
                if (access != null && access.Mounted) BootEntry.Unmount(access.Letter);
                step.Report("Création d'une partition de démarrage (EFI) pour IXIN OS (la partition EFI prévue ne convient pas)…");
            }
            else
            {
                Log.Info("Partition EFI dédiée : " + choice.Why);
                step.Report("Création d'une partition de démarrage (EFI) pour IXIN OS…");
            }

            var created = Storage.CreateEsp(setupPart.Disk, Storage.AlignUp(setupPart.Offset + setupPart.Size, Storage.MiB), Plan.NewEspSize);
            st.EspMode = EspMode.Create.ToString();
            st.EspCreated = true;
            st.EspGuid = created.Guid;
            st.EspDisk = created.Disk;
            st.EspNumber = created.Number;
            st.EspVolumePath = null;
            st.Save();
            return new EspAccess { Letter = Storage.FormatEsp(created), Guid = created.Guid, Mounted = true };
        }

        /// <summary>Monte la partition EFI qui a reçu le chargeur (selon l'état), null si inconnue.</summary>
        private static EspAccess MountUsedEsp(SetupState st, bool testMode, string testEspLetter)
        {
            bool system = st?.EspMode == null || st.EspMode == EspMode.Windows.ToString();
            if (system)
                return testMode
                    ? (testEspLetter == null ? null : new EspAccess { Letter = testEspLetter, System = true })
                    : new EspAccess { Letter = BootEntry.MountSystemEsp(), System = true, Mounted = true };
            return new EspAccess
            {
                Letter = Storage.MountEsp(st.EspDisk, st.EspNumber, st.EspCreated ? null : st.EspVolumePath),
                Guid = st.EspGuid,
                Mounted = true,
            };
        }

        /// <summary>Retire le chargeur d'IXIN OS Setup de la partition EFI où il a été copié.</summary>
        private static void RemoveLoader(SetupState st, bool testMode, string testEspLetter)
        {
            var esp = MountUsedEsp(st, testMode, testEspLetter);
            if (esp == null) return;
            try
            {
                BootEntry.RemoveLoader(esp.Letter);
            }
            finally
            {
                if (esp.Mounted) BootEntry.Unmount(esp.Letter);
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

            if (st != null && st.EspCreated)
            {
                step?.Report("Suppression de la partition EFI créée pour IXIN OS…");
                Storage.DeleteCreatedEsp(st.EspDisk, st.EspGuid);
            }
            else
            {
                step?.Report("Retrait du chargeur de la partition EFI…");
                RemoveLoader(st, testMode, testEspLetter);
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
                if (st.FastStartupChanged == true || st.ClockChanged == true ||
                    st.PreviousHiberboot.HasValue || st.PreviousRtcUtc.HasValue || st.Completed)
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
            string id = ids.Count > 0 ? ids[0] : RecreateBootEntry(st);
            BootEntry.BootNextOnce(id);
            BootEntry.Restart(toFirmwareSettings);
        }

        /// <summary>
        /// Recrée l'entrée « IXIN OS Setup » disparue (mise à jour du firmware, réinitialisation…),
        /// vers la partition EFI qui contient le chargeur.
        /// </summary>
        private static string RecreateBootEntry(SetupState st)
        {
            var esp = MountUsedEsp(st, false, null);
            string id;
            try
            {
                id = BootEntry.Create(deviceLetter: esp.System ? null : esp.Letter);
            }
            finally
            {
                if (esp.Mounted) BootEntry.Unmount(esp.Letter);
            }
            if (st != null)
            {
                st.BootEntryId = id;
                st.Save();
            }
            return id;
        }

        /// <summary>
        /// IXIN OS installé en gardant IXIN-SETUP (option avancée) : supprime la partition
        /// d'installation, l'entrée « IXIN OS Setup » et son chargeur, puis rend l'espace à Windows
        /// s'il est juste après lui. Une partition EFI créée pour IXIN OS est gardée : il démarre avec.
        /// </summary>
        public static void RemoveSetupAfterInstall(SetupState st, IProgress<string> step)
        {
            step?.Report("Retrait de « IXIN OS Setup » du menu de démarrage…");
            foreach (var id in BootEntry.Find()) BootEntry.Delete(id);
            RemoveLoader(st, false, null);
            foreach (var p in Storage.FindSetupPartitions())
            {
                if (st != null && st.DiskNumber >= 0 && p.Disk != st.DiskNumber) continue;
                step?.Report("Suppression de la partition IXIN-SETUP…");
                Storage.DeletePartition(p);
                Log.Info($"Partition IXIN-SETUP supprimée (disque {p.Disk}, partition {p.Number})");
            }
            Storage.ExtendToMax(st?.DriveLetter ?? "C");
            SetupState.Delete();
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
