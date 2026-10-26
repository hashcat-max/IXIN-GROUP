# IXIN OS: complete context (to hand to another Claude project)

> This document summarizes everything that has been built for **IXIN OS**. It is the brief for the site
> **os.ixin.online** (presentation, downloads, documentation). Everything stated here exists and has been
> tested, unless explicitly marked otherwise. **Please do not invent features that are not listed here.**
>
> **Last update: October 26, 2026 — current release `build-2026.10.25-6`** (new: §0 release `-6` — Windows 7 → 11
> support, IXIN OS in a window, "Jouer sous Windows", hardware —, §4, §7, §8 new downloads, §9; Secure Boot: ready,
> not yet active in a published release, see §7).
> Companion file (French, ready to adapt into a page): `docs/INSTALLATION-WINDOWS.md`, the complete
> "Installer IXIN OS à côté de Windows" user guide.

---

## 0. What's new (for a "Nouveautés" section)

### `build-2026.10.25-6` (October 25, 2026) — current release, ready for the first public tests
- **IXIN OS Setup opens on every Windows PC and checks it first** (installation itself: Windows 10 / 11 64-bit). It
  says plainly what blocks and points to the USB stick guide when needed: ARM or 32-bit processor/Windows, Windows
  7 / 8 / 8.1, disk in RAID /
  Intel RST, Windows on a USB disk, "dynamic" disk, disabled Windows storage services (re-enabled automatically).
  Small screens are handled (the window always fits).
- **New: IXIN OS in a window** ("machine virtuelle IXIN"): from IXIN OS Setup, choose "Essayer IXIN OS dans une
  fenêtre" — nothing is changed on the disk. IXIN OS Setup downloads a bundled engine (QEMU, 50 MB) and IXIN OS, enables
  the Windows hypervisor platform if needed (one restart), creates the virtual machine (half the RAM, up to 8 GB;
  120 GB virtual disk that grows as needed) and puts "IXIN OS" shortcuts on the desktop and Start menu. "Supprimer"
  removes everything. Windows 10 / 11 64-bit only.
- **"Jouer sous Windows" button** in IXIN OS (Jeux tab of the IXIN Center, and a "Valorant (sous Windows)" shortcut):
  one click restarts **directly into Windows**, once, for games whose anti-cheat requires Windows (Valorant…). Next
  restart goes back to the normal menu. Works on UEFI and BIOS PCs.
- **If the PC comes back to Windows instead of the installation**, IXIN OS Setup **reopens on its own** at logon and
  explains what to do (Secure Boot still on, or the boot-menu key for the PC brand: F12 for Lenovo/Dell, F9 for HP…).
- **Hardware**: older NVIDIA cards (GeForce GTX 10xx and older) automatically get the open-source driver, so the
  screen no longer stays black; **printers and scanners** recognized automatically (USB and network, HP and
  Gutenprint drivers); screen rotation sensors of convertible PCs.
- **New beginner's guide** "installer IXIN OS avec une clé USB" (`docs/INSTALLATION-CLE-USB.md`): what an ISO is,
  making the USB stick with Rufus/Ventoy/balenaEtcher, the boot-menu key for each brand, troubleshooting table.
- Installer: confirmation buttons correctly translated ("Revenir en arrière", "Installer maintenant").

### `build-2026.10.17-5` (October 17, 2026)
- **IXIN OS Setup works on more PCs**: it no longer refuses PCs whose EFI (boot) partition is on another disk (PCs
  installed with two disks plugged in — they used to get "Partition EFI introuvable"). It looks at every disk, and
  if no EFI partition is usable (none, almost full, on a USB disk) it **creates one automatically** (300 MB).
- **Even simpler for beginners**: the space page only asks for the size; on the IXIN side, the right disk, the free
  space and the right EFI partition are **already selected** at the "Partitions" step — just tick encryption.
- **Advanced options for experienced users** ("Options avancées…"): exact size, choice of EFI partition (existing or
  dedicated), ISO already downloaded, keep the installer partition, Windows settings.
- Clearer message on PCs in BIOS mode, with a step-by-step USB stick guide.

### `build-2026.10.16-4` (October 16, 2026)
Two fixes on top of `-3`, same features:
- **Installation started from Windows**: at the "Partitions" step the installer now offers only "Remplacer une
  partition" (the free space prepared by IXIN OS Setup) and manual partitioning. The "Installer à côté" option, which
  would have shrunk Windows a second time, is gone — as the welcome window already said.
- **IXIN OS Setup**: a button that cannot be used yet (e.g. "Continuer" before the BitLocker key is confirmed) is now
  clearly greyed out instead of staying bright green.

### `build-2026.10.15-3` (October 15, 2026) — the first complete release
- **Install from Windows, without a USB stick**: new program **IXIN OS Setup** (`IXIN-OS-Setup.exe`, 3.7 MB,
  Windows 10 / 11). It checks the PC, downloads IXIN OS, makes room on the disk and restarts into the installer.
  Windows and its files stay in place; everything can be undone until IXIN OS is installed. (§5.1)
- **Dual-boot menu**: at power-on, the IXIN menu offers **IXIN OS**, **Windows** and the firmware settings.
  "Windows" restarts the PC straight into Windows through the PC's own firmware, exactly as without IXIN —
  designed so that BitLocker does not ask for its recovery key. (§4)
- **IXIN Update is built in**: automatic updates and security fixes, signed by the IXIN team. This is the
  first release that includes it out of the box. (§4)
- **Safer installer**: Windows partitions (NTFS / BitLocker) can never be erased or replaced by mistake; new disk
  layout with a separate `/boot` partition, compatible with the small EFI partition of Windows PCs; the NTFS tools
  needed to shrink Windows from a USB installation are included again.
- Releases `build-2026.10.15-1` and `-2` are **obsolete** (no IXIN Update, no Windows installer, known boot issue on
  `-1`), and `-3` / `-4` are replaced by `-5`: the site should only link to the **latest** release.

## 1. What IXIN OS is

- A **Linux distribution based on Arch Linux** (a rolling release, so always up to date), created by **IXIN Group**.
  It is the "operating system" companion to the IXIN ecosystem:
  - **getixin.com**: "IXin, Communication absolue". End-to-end encrypted private messaging, voice and video calls, voice Hubs, disposable email, anonymous VPN. Available on Windows, Mac, Linux, Android, Chromebook and the browser.
  - **ixin.online**: the IXIN web app.
- Positioning: **security · privacy · performance**. A system that is secure by default, comfortable for everyday use (the desktop looks like Windows), and capable of running Steam and Windows games.
- Tagline used across the OS: **« Sécurité · Vie privée · Performance »**.
- Source code: public GitHub repository **`hashcat-max/IXIN-GROUP`** (default branch `ccr-f4f50bff-01pzqi`).

## 2. Visual identity (taken from getixin.com, to reuse as-is on the site)

| Token | Value | Usage |
|---|---|---|
| `--void` | `#050505` | main background (very dark) |
| `--emerald` | `#19f3a4` | primary accent (selection, buttons, titles) |
| `--sapphire` | `#1c6bff` | secondary accent |
| violet | `#a855f7` / `#7c3aed` | gradient accents |
| `--whitegold` | `#efe6d2` | logo dot, fine details |
| `--ink` | `#e9ecef` | main text |
| `--titanium` | `#8d9299` | secondary text |
| Signature gradient | `#19f3a4 → #1c6bff → #a855f7` | logo, progress bars |
| Fonts | **Inter / Inter Tight** (text), **JetBrains Mono** (code, small labels) | |

- **Logo**: a hexagonal **shield** with a stylized **X** in the center (emerald → sapphire → violet gradient)
  and a small white-gold dot at the center. Files in the repo:
  - `iso/airootfs/usr/share/ixin/branding/ixin-logo.svg` (shield only)
  - `iso/airootfs/usr/share/ixin/branding/ixin-logo-text.svg` (shield + "IXIN OS")
  - PNG exports from 16 to 512 px in the same folder
- **Wallpaper**: `iso/airootfs/usr/share/ixin/wallpapers/ixin-void.png` (1080p) and `ixin-void-4k.png`.
  Black background with a faint grid, blue/green/violet halos, the shield in the center, "IXIN OS" in thin, widely spaced letters, and the tagline in JetBrains Mono.
- Tone: dark, premium, minimalist, "tech / security".

## 3. Screenshots (real, taken during testing)

All in `docs/screenshots/`. Raw URL for any of them:
`https://raw.githubusercontent.com/hashcat-max/IXIN-GROUP/ccr-f4f50bff-01pzqi/docs/screenshots/<file>`
(better: copy the files into the site itself).

**Desktop and installed system** (1280×800, full screen, VM without 3D acceleration):
- `bureau.png`: desktop with the Windows 11-style taskbar
- `centre-ixin.png`: the IXIN Center application (welcome screen)
- `menu-demarrage-ixin-windows.png`: **dual-boot menu** of an installed PC (IXIN / Windows / UEFI Firmware Settings)
- `deverrouillage-chiffrement.png`: IXIN boot screen asking for the encrypted disk password
- `ecran-connexion.png`: login screen of the installed system
- `ixin-secure.png`: security status in the terminal (installed system)
- `grub-installe.png`: IXIN boot screen of the installed system
- `menu-demarrage-live.png`: boot menu of the USB stick / ISO

**Install from Windows, part 1: the IXIN OS Setup program** (`ixin-os-setup-*.png`, 780×600, the program's
window without its title bar). Generated by the program itself (`IXIN-OS-Setup.exe --screenshots <folder>`, run by
GitHub Actions on a real Windows) with **sample data**: a Windows 11 PC with a 1 TB SSD, 412 GB free on C:,
BitLocker on, Secure Boot on. Nothing is real: the BitLocker key shown is fake. In the order of the journey:
1. `ixin-os-setup-1-analyse.png`: "Analyse du PC…"
2. `ixin-os-setup-2-verification.png`: check of the PC (✔ compatible UEFI PC, ✔ GPT disk, ✔ space available,
   ⚠ Secure Boot on, ⚠ Windows encrypted with BitLocker)
3. `ixin-os-setup-3-espace.png`: choosing the space for IXIN OS (slider, 80 GB) — the only choice for beginners
   - `ixin-os-setup-3b-options-avancees.png`: the "Options avancées" page for experienced users (exact size,
     EFI partition, ISO already downloaded, keep IXIN-SETUP, Windows settings)
4. `ixin-os-setup-4-cle-bitlocker.png`: the BitLocker recovery key to note (button greyed until confirmed)
5. `ixin-os-setup-5-confirmation.png`: "Vérifiez avant de commencer", the full list of what will be changed
6. `ixin-os-setup-6-preparation.png`: preparation in progress (download of IXIN OS)
7. `ixin-os-setup-7-secure-boot.png`: last step when Secure Boot is on ("Redémarrer vers les paramètres UEFI")
8. `ixin-os-setup-8-tout-est-pret.png`: "Tout est prêt !" when Secure Boot is already off
9. `ixin-os-setup-9-installation-prete.png`: the program reopened before installing ("Annuler la préparation")
10. `ixin-os-setup-10-ixin-installe.png`: the program reopened once IXIN OS is installed ("Rendre 6 Go à Windows",
    "Remettre le menu IXIN au démarrage")
- `ixin-os-setup-efi-autre-disque.png`: check of a PC whose EFI partition is on another disk — shown as information,
  not blocking (such PCs used to be refused).
- `ixin-os-setup-reduction-bloquee.png`: a blocking check ("C: ne peut pas être réduit assez") with the
  "Débloquer la réduction" button — for a troubleshooting section.

**Install from Windows, part 2: on the IXIN side** (1280×800, the published ISO `build-2026.10.16-4` in a VM,
on a simulated Windows PC prepared like IXIN OS Setup does):
- `depuis-windows-guide.png`: the welcome window shown when the PC restarts into IXIN OS ("IXIN OS Setup a préparé
  un espace libre de 73 Go pour IXIN OS"), with "Lancer l'installation"
- `depuis-windows-partitions.png`: the "Partitions" step in this mode: only "Remplacer une partition" (preselected)
  and manual partitioning; the "Espace libre" zone selected and "Chiffrer le système" ticked; the "Après" bar shows
  IXIN-BOOT (1 GB) + IXIN (72 GB, LUKS2) in the free space, every Windows partition untouched.
- then `menu-demarrage-ixin-windows.png` (above): the IXIN / Windows menu after installation.

**USB installer** (1280×800):
- `installeur-partitions-chiffrement.png`: whole-disk install, "Effacer le disque" + "Chiffrer le système" — current
  layout: EFI system partition 512 MB, IXIN-BOOT 1 GB (ext4), IXIN (LUKS2). *(Replaced: the old file
  showed an outdated layout.)*
- `installeur-emplacement.png` (time zone map), `installeur-utilisateur.png` (user account),
  `installeur-diaporama.png` (installation in progress). Taken with the installer in debug mode: a small "Debug"
  button appears at the bottom left, which users do not see.

## 4. Features (what is actually in the OS)

### Desktop and ease of use
- **KDE Plasma 6** desktop (Wayland), **IXIN** global theme.
- **Taskbar at the bottom, centered, Windows 11 style**: IXIN Start button (shield logo),
  search, pinned apps (Files, Firefox, Steam, Terminal, KeePassXC, IXIN Center),
  system tray, clock, show desktop button.
- Dark **IXIN** color scheme, Papirus icons, Inter / JetBrains Mono fonts.
- Everything branded IXIN: boot menu, Plymouth boot animation, KDE splash screen,
  login screen (SDDM), lock screen, GRUB menu of the installed system.
- **French by default** (language, AZERTY keyboard, Europe/Paris time zone); changeable in the installer.

### IXIN Center (`ixin-center`)
An in-house graphical application that opens on first login. 5 tabs:
- **Accueil**: system information (kernel, CPU, GPU, RAM), Install button (in live mode), Update, links to getixin.com / ixin.online.
- **Sécurité**: protection status (firewall, AppArmor, disk encryption, encrypted DNS, OpenSnitch, USBGuard) and one-click buttons to enable them; Tor Browser, KeePassXC, VeraCrypt, metadata cleaning, Lynis audit, antivirus scan, rootkit check.
- **Jeux**: Steam, Heroic, Lutris, Bottles, ProtonUp-Qt, power profiles (performance / balanced), NVIDIA settings, and a Steam tip.
- **Système**: full update, Discover / Flathub software store, Timeshift snapshots, partition manager, settings, system monitor, BleachBit, system log.
- **Mises à jour**: IXIN Update status, security fixes with their CVEs, history, update mode, restore points.

### Gaming and Windows compatibility
- **Steam** + **Proton** (runs most Windows games), **Heroic** (Epic, GOG, Amazon), **Lutris**, **Bottles**,
  **Wine-staging** + Winetricks, **ProtonUp-Qt** (Proton-GE), **GameMode**, **MangoHud**, **Gamescope**.
- **linux-zen** kernel (tuned for responsiveness), zram (compressed RAM), BBR network congestion control, gaming-oriented sysctl settings.
- AMD, Intel and NVIDIA drivers (open NVIDIA kernel modules), 32- and 64-bit Vulkan.
- `ixin-gaming %command%` command for Steam launch options (GameMode + MangoHud).
- **Honest limitation, to state on the site**: games whose anti-cheat blocks Linux
  (e.g. Valorant, Fortnite, some Call of Duty titles, Rainbow Six Siege) **do not work**, on any Linux distribution,
  and IXIN OS does **not** try to trick anti-cheat systems (that gets accounts banned). For those, keep Windows in
  dual boot: the **"Jouer sous Windows"** button (IXIN Center → Jeux, or the "Valorant (sous Windows)" shortcut) restarts
  straight into Windows in one click. You can check a game on protondb.com.

### Dual boot with Windows
- The IXIN boot menu offers **IXIN OS**, **Windows** and **UEFI Firmware Settings** (screenshot `menu-demarrage-ixin-windows.png`).
- Choosing **Windows** restarts the PC directly into Windows **through the PC's firmware**, the same way Windows starts
  without IXIN. This is designed so that **BitLocker does not ask for its recovery key** (it often does when a Linux
  boot loader starts Windows itself). If the firmware entry is missing, the menu starts Windows directly instead.
- During installation, Windows partitions (NTFS, BitLocker) **can never be selected to be replaced or erased**.

### Security (defensive, enabled by default)
- **UFW firewall**: incoming connections blocked, outgoing allowed.
- **AppArmor** enabled (application confinement), **Firejail** available.
- **Hardened kernel** (sysctl): kernel pointers and dmesg restricted, unprivileged BPF disabled, ptrace limited, ICMP redirects refused, SYN cookies, and more.
- **Encrypted DNS** (DNS-over-TLS via Quad9, which also filters malicious domains).
- **Randomized Wi-Fi MAC address** per network, IPv6 privacy addresses, hostname not sent over DHCP.
- **Full disk encryption** offered in the installer: **LUKS2 (argon2id)**, unlocked at boot on the IXIN screen.
- No network service exposed (no SSH); sudo requires a password.
- `ixin-secure` command: security status and enabling the optional protections
  (OpenSnitch application firewall, USBGuard blocking of unknown USB devices, Tor service, Lynis audit, ClamAV scan, rkhunter).
- **Honesty**: no system is "100% secure". IXIN OS applies reasonable defense in depth without breaking games.
  Performance tweaks that weaken security (e.g. `mitigations=off`) are deliberately left out.

### Automatic updates: IXIN Update (Windows-style)
- **Automatic security fixes**: as soon as Arch Linux publishes a fix for a known flaw (CVE), it is installed (detected via the Arch security tracker).
- Other updates: once a day in the background. Choice of mode: **Automatique** (default) / **Me prévenir seulement** / **Manuel**.
- **Restore point** (Btrfs snapshot via Timeshift) before every update: possible rollback in a few clicks.
- **Smart**: no non-urgent updates during a game, on battery or on a metered connection; shutdown is delayed until an installation finishes.
- **Notifications** (taskbar icon): updates installed, restart needed, IXIN announcements.
- **Push by the IXIN team**: the team can publish updates, **urgent fixes** (installed within the hour),
  block a faulty update, or send a message to all PCs — all **cryptographically signed** (no third party can impersonate IXIN).
- Where updates come from (for a transparency / FAQ section):
  - IXIN packages and the IXIN policy are published by GitHub Actions on **GitHub Pages**
    (`https://hashcat-max.github.io/IXIN-GROUP/repo`, live since October 16, 2026) with a fallback copy on the
    `ixin-repo` branch of the repository;
  - every package, the package database and the policy are **signed with the IXIN key**; PCs refuse anything unsigned.
    Fingerprint to publish on the site: **`74DF FA0C 6B29 1E07 27EA  8A59 891C 90DD 6094 ABDF`**
    ("IXIN OS Updates (équipe IXIN)");
  - Arch Linux packages come from the official Arch mirrors, signed by Arch Linux.
- Optional idea for the site: a small **"État des mises à jour"** widget. The signed IXIN policy is public JSON that a
  browser can fetch directly (GitHub Pages sends `Access-Control-Allow-Origin: *`; cached 10 minutes):
  `https://hashcat-max.github.io/IXIN-GROUP/repo/policy.json`. Useful fields:
  - `issued` / `expires`: ISO 8601 UTC dates. `issued` is when the policy was last **signed** — at every IXIN
    publication and automatically every Monday (valid 14 days) — not the date of the latest package, so label it
    e.g. "Service de mises à jour vérifié le …", not "dernière mise à jour";
  - `urgent`: `true` while the IXIN team is pushing an urgent fix;
  - `notice`: `null`, or the announcement currently shown on every PC: `{"level": "info" | "critical", "text": "…"}`
    (the PCs display at most 500 characters; show it the same way, "critical" as an alert);
  - other fields (`version`, `serial`, `hold`, `news_ack`) are internal.
  Example (October 16, 2026): `{"urgent": false, "hold": [], "news_ack": [], "notice": null, "version": 1,
  "serial": 1792117820, "issued": "2026-10-16T02:30:20Z", "expires": "2026-10-30T02:30:20Z"}`.
  If the request fails, simply hide the widget.

### Privacy and tools
Tor Browser, Tor + torsocks, Signal, KeePassXC, VeraCrypt, GnuPG + Kleopatra, mat2 / Metadata Cleaner,
BleachBit, WireGuard / OpenVPN (NetworkManager integration).

### Preinstalled applications
Firefox, Thunderbird, LibreOffice, VLC, mpv, OBS Studio, GIMP, Dolphin, Kate, Okular, Gwenview,
Spectacle, Discover + Flathub, Timeshift + Snapper (Btrfs snapshots), KDE Connect.

## 5. Installation

Two ways, to present side by side on the site:
- **From Windows, with IXIN OS Setup** — the simplest for Windows users: no USB stick, no ISO to reassemble. (§5.1)
- **With a USB stick** — for a PC without Windows, a whole-disk install, a virtual machine, or a PC that IXIN OS Setup
  does not support (BIOS / MBR). (§5.2)

Requirements (both): 64-bit (x86_64) PC, **UEFI** recommended (legacy BIOS works for the live system),
**4 GB RAM** minimum (8+ recommended), **40 GB** of disk minimum, **Secure Boot disabled** (see FAQ).

### 5.1 From Windows, without a USB stick: IXIN OS Setup
- **`IXIN-OS-Setup.exe`** (3.7 MB) is attached to every release. It runs on **Windows 10 / 11 (64-bit)** with a
  **UEFI** boot and a **GPT** disk (nearly all PCs since 2012) and needs about **75 GB free on C:**
  (45 GB minimum for IXIN OS — 80 GB recommended for games —, 6 GB for the installer, 5 GB of temporary download,
  and 20 GB left free for Windows). A laptop should be plugged in (below 30 % battery the preparation is refused).
- What the user does:
  1. download and run `IXIN-OS-Setup.exe` (Windows asks for administrator rights: "Oui");
  2. **check of the PC**: each point is explained (✔ ok, ⚠ good to know, ✖ blocking);
  3. **choose the space** for IXIN OS (slider, the only choice on this page); IXIN OS is downloaded automatically
     (≈5 GB) and its SHA-256 is verified. An **"Options avancées…"** button (for experienced users, screenshot
     `ixin-os-setup-3b-options-avancees.png`) gives: exact size, which EFI (boot) partition to use — automatic,
     an existing one, or a new one dedicated to IXIN OS (300 MB, Windows' own left untouched) —, an ISO already
     downloaded, keeping the IXIN-SETUP partition after installation (to reinstall without downloading again),
     and the two Windows settings (Fast Startup, UTC clock). Defaults suit nearly every PC;
  4. if Windows is encrypted (**BitLocker** / device encryption): the **recovery key** is shown and can be saved to a
     file; the user confirms having noted it;
  5. confirmation, then **preparation**: Windows is shrunk, a 6 GB "IXIN-SETUP" partition receives the verified ISO,
     an "IXIN OS Setup" entry is added to the boot menu (Windows stays the default);
  6. **restart**: straight into the installer, or first into the UEFI settings if **Secure Boot** must be turned off;
  7. in the IXIN OS live system, a welcome window explains the single choice to make: at the **"Partitions"** step,
     the disk and the free space prepared by IXIN OS Setup are **already selected** (even on a PC with several
     disks), as is the right EFI partition → just tick **"Chiffrer le système"** (recommended) and continue. In this
     mode the disk cannot be erased and Windows partitions cannot be selected; experienced users can still pick
     "Partitionnement manuel" (screenshots `depuis-windows-guide.png`, `depuis-windows-partitions.png`);
  8. finish the installation (name, password), restart: the IXIN menu offers IXIN OS and Windows.
- On the first start of IXIN OS, the temporary "IXIN-SETUP" partition (6 GB) and the "IXIN OS Setup" menu are
  removed automatically. Running IXIN OS Setup again in Windows then offers **"Rendre 6 Go à Windows"**.
- **Everything can be undone** until IXIN OS is installed ("Annuler la préparation"); on any error during the
  preparation, Windows is put back automatically in its original state.
- **EFI (boot) partition, handled automatically**: IXIN OS Setup looks at every disk and uses the EFI partition Windows
  starts from, even when it is on another disk (common on PCs installed with two disks plugged in — such PCs used to be
  refused with "Partition EFI introuvable"). If there is no usable one (none, almost full, or on a USB disk), it
  **creates a dedicated 300 MB EFI partition "IXIN-EFI" by itself**, taken from IXIN OS's space. The IXIN OS
  installer then picks that same partition automatically.
- Extra help built into the program: **"Débloquer la réduction"** (when Windows cannot be shrunk because of system
  files: hibernation file, page file — it becomes the main button when it is the only problem), **"Remettre le menu
  IXIN au démarrage"** (if a Windows update put Windows first in the boot order); it recognises a PC where IXIN OS is
  already installed.
- What it changes on the PC (all reversible before installation): Windows partition shrunk; exFAT "IXIN-SETUP"
  partition without a drive letter; `\EFI\IXIN-SETUP\` boot loader (3 MB) + `options.cfg` in the EFI partition;
  a dedicated "IXIN-EFI" partition (300 MB) only when needed or asked for; "IXIN OS Setup" firmware boot entry;
  Windows Fast Startup turned off and hardware clock set to UTC (needed for dual boot; both can be kept as they are
  in the advanced options); BitLocker suspended for 3 restarts (Windows re-enables it by itself).
  Log: `C:\ProgramData\IXIN\setup.log`.
- Not supported: BIOS (legacy) or MBR disk → the program explains it and the guide (section 10) walks through a USB
  stick installation; IXIN OS goes on the disk where Windows is (its EFI partition may be on another disk).
- The program is open source (`windows/` in the repository) but **not code-signed yet**: Windows SmartScreen
  shows "Windows a protégé votre ordinateur" → "Informations complémentaires" → "Exécuter quand même".
- Full user guide in French: **`docs/INSTALLATION-WINDOWS.md`** (requirements, step-by-step, Secure Boot,
  BitLocker, undo, troubleshooting, what is modified). Good material for an "Installer depuis Windows" page.

### 5.2 With a USB stick (graphical installer: Calamares)
- Boot from a USB stick → **live** desktop (try it without installing) → "Installer IXIN OS" icon.
- Steps: Welcome → Location → Keyboard → **Partitions** → User → Summary → Install → Finish.
- Partitioning options: **"Effacer le disque"** (whole disk), **"Installer à côté"** (shrinks Windows, offered when
  Windows is detected — not yet tested end to end with a real Windows), or **manual partitioning**.
- Writing the ISO to a USB stick: **Ventoy** (recommended) or **Rufus** (Windows, mode GPT/UEFI), **balenaEtcher**.
- Dual-boot tips with a USB stick: in Windows, turn off **Fast Startup** and **suspend BitLocker** first (IXIN OS Setup
  does both automatically); disable **Secure Boot**; if the clock is off by an hour under Windows, run in an
  administrator terminal on Windows:
  `reg add "HKLM\System\CurrentControlSet\Control\TimeZoneInformation" /v RealTimeIsUniversal /d 1 /t REG_DWORD /f`

### Disk layout (both ways)
EFI partition (512 MB, or the existing Windows one) + separate **1 GB `/boot`** (ext4) + **Btrfs** system
(subvolumes `@`, `@home`, `@cache`, `@log`, compatible with Timeshift snapshots), with optional **LUKS2** encryption.

### Live boot menu options (USB stick / ISO)
- IXIN OS: Live / Installation
- IXIN OS: Live loaded into RAM (faster, the USB stick can be removed)
- IXIN OS: Graphics compatibility mode (nomodeset), if the screen stays black
- Memory test (Memtest86+), boot the existing system (BIOS)

## 6. IXIN commands (for the "Documentation" page)

| Command | Purpose |
|---|---|
| `ixin-center` | IXIN Center (graphical interface) |
| `ixin-secure [status\|firewall-on\|opensnitch-on\|usbguard-on\|tor-on\|audit\|scan [folder]\|rootkit]` | Security status and settings |
| `ixin-update` | Updates (IXIN Update): `status`, `check`, `apply`, `snapshot` |
| `ixin-gaming <cmd>` / `ixin-gaming --gamescope <cmd>` | Launch with GameMode + MangoHud (+ Gamescope) |
| `ixin-install` | Launch the installer (live mode) |

## 7. Compatibility (what is true today)

- ✅ **64-bit Intel and AMD PCs** (desktops, laptops), UEFI and BIOS (live). AMD / Intel / NVIDIA GPUs.
- ✅ **Install from Windows** (IXIN OS Setup): Windows 10 / 11 64-bit, UEFI + GPT disk, BitLocker supported.
- ✅ **Virtual machines**: VirtualBox, VMware, QEMU / KVM, Hyper-V (guest tools included).
- ⚠️ **Chromebooks**: only x86 (Intel / AMD) models with alternative UEFI firmware (MrChromebox). Not tested.
- ❌ **Android phones / ARM tablets**: not supported (that would require a separate ARM port per device).
  The IXIN app on Android is the ecosystem's answer for phones.
- ❌ **Mac with Apple Silicon (M1–M4)**: not supported. Intel Macs: possible, but not tested.
- ⏳ **Secure Boot**: support is **built and tested** (Microsoft-signed shim → IXIN-signed boot loader → IXIN-signed
  kernel; the PC is asked once to trust the IXIN key on a blue "MokManager" screen; IXIN OS Setup prepares this from
  Windows with the one-time password `ixinos`). It becomes active in the **first release built after the IXIN team
  configures its signing key**; that release's notes will say "compatible Secure Boot". Until then, in `-6` and
  older, Secure Boot must be disabled. Some Windows games with anti-cheat (Valorant, Battlefield 6…) require Secure
  Boot **under Windows**: this is why Secure Boot support matters to gamers. Do not announce it as available yet.
- ✅ **Windows 10 / 11 (64-bit)** for IXIN OS Setup (install and "IXIN OS in a window"); on Windows 7 / 8 / 8.1 it opens
  and points to the USB stick guide.

## 8. Downloads

Current release: **`build-2026.10.25-6`** (October 25, 2026) — https://github.com/hashcat-max/IXIN-GROUP/releases/tag/build-2026.10.25-6

| File | Size | For |
|---|---|---|
| `IXIN-OS-Setup.exe` | 4.2 MB | Windows 10 / 11: install without a USB stick, or try IXIN OS in a window |
| `ixin-os-2026.10.25-x86_64.iso.part00` / `.part01` / `.part02` | 1.99 + 1.99 + 1.15 GB (**5.13 GB** in total) | USB stick, virtual machine |
| `ixin-os-2026.10.25-x86_64.iso.sha256` | — | integrity check of the reassembled ISO |
| `ixin-vm-engine-win64.zip` (+ `.sha256`) | 50.8 MB | engine of "IXIN OS in a window", downloaded by IXIN OS Setup itself (no need to list it on the site) |

- ISO SHA-256: `d2bff2a52c183575628fb6418c525cb7a00a6260585b7b34778b730b5b954458`
- `IXIN-OS-Setup.exe` SHA-256: `c8a7107ddcf16bf6ba30cc708b66b1356b98659008b2f16cd5a25c62b290ff0e`
- These values change with every release: if the site shows them, read them from the API below (each asset has a
  `digest` field, e.g. `sha256:7dfc…4c02`) rather than hard-coding them.
- **Stable links** (always the latest release, good for buttons):
  - `https://github.com/hashcat-max/IXIN-GROUP/releases/latest/download/IXIN-OS-Setup.exe`
  - release page: `https://github.com/hashcat-max/IXIN-GROUP/releases/latest`
  - the ISO part names contain the build date: link to the release page, or read the file list, sizes and dates
    from the public API `https://api.github.com/repos/hashcat-max/IXIN-GROUP/releases/latest` (field `assets`).
- The ISO is built automatically by **GitHub Actions** and published **split into 1.9 GB parts** (GitHub limit of
  2 GB per file). Reassemble it:
  - Linux / macOS: `cat ixin-os-*.iso.part* > ixin-os.iso` then `sha256sum -c ixin-os-*.iso.sha256`
  - Windows (cmd): `copy /b ixin-os-….iso.part00+ixin-os-….iso.part01+ixin-os-….iso.part02 ixin-os.iso`
- **Recommendation for the site**: put IXIN OS Setup forward for Windows users (one small file, no reassembly). For the
  ISO, a single direct download is more convenient than split parts: host the full ISO on object storage
  (Cloudflare R2, Backblaze B2, a CDN) and add a **torrent** link, showing size, version, date and SHA-256.

## 9. Project status (to stay honest on the site)

- Version: **Beta (v0.1)**, rolling, dated builds. Latest: **`build-2026.10.25-6`** (§0). Older releases are obsolete.
- `-6` tests: IXIN OS Setup built and self-tested on Windows Server 2022 and 2025 (GitHub Actions) including the
  virtual-machine engine (started headless, UEFI firmware reached); legacy BIOS PC in a VM: live boot, full encrypted
  install, installed system boots; "Jouer sous Windows" in BIOS and UEFI; NVIDIA driver choice on 7 simulated cards.
- Secure Boot (not yet active, §7) tested in a VM whose firmware trusts only Microsoft keys: USB stick → MokManager
  "Enroll key from disk" → live system with Secure Boot on; key request written exactly as IXIN OS Setup does from
  Windows → MokManager password `ixinos` accepted; installed layout (shim + IXIN GRUB on the disk) → IXIN graphical
  menu → signed kernel; an unsigned kernel is refused.
- Tested in a VM: ISO build, UEFI boot, live desktop + taskbar, IXIN Center, graphical installer, full encrypted install,
  reboot of the installed system (GRUB → IXIN unlock screen → login → desktop; firewall, AppArmor, LUKS2 and encrypted
  DNS checked on the installed system).
- IXIN Update tested in a VM on an installed system: restore point on first boot, automatic updates, urgent IXIN pushes
  installed automatically (restore point, notification, IXIN announcement), forged policy / replayed policy / tampered
  repository database all rejected, repair after a power cut mid-update.
- IXIN OS Setup (Windows): tested automatically on every change on a real Windows 11 (GitHub Actions) with a virtual
  disk — full preparation then full undo, firmware boot entries, "put the IXIN menu back first". The IXIN side was tested
  end to end in a VM on a simulated Windows PC: firmware boot entry → "IXIN OS Setup" menu → live system started from the
  ISO stored on the disk → guided install into the free space (Windows partitions cannot be selected), encrypted → first
  start with automatic cleanup → "IXIN / Windows" menu, Windows started by the firmware, Windows files intact.
- **Not tested on real hardware yet** (VM and Windows test machines only): label everything **"Bêta"** on the site.
- Known limits: Secure Boot must be off until a release says "compatible Secure Boot"; IXIN OS Setup is not code-signed (SmartScreen warning); BIOS / MBR PCs need a USB
  stick; IXIN OS Setup installs on the Windows disk only; no automatic **uninstall** of IXIN OS yet; "Installer à côté"
  from a USB stick not yet tested with a real Windows.
- EFI partition handling (`-5`) tested on a real Windows (GitHub Actions, virtual disks) for three PCs: EFI partition
  on the Windows disk, on another disk, none (created then removed by "Annuler"); and in a VM on a simulated PC with
  two disks (EFI partition on the first, Windows on the second): disk, free space and EFI partition preselected,
  full encrypted install, IXIN / Windows menu, automatic cleanup at first start. Dedicated EFI partition: chosen
  automatically by the installer, Windows found in the other EFI partition for the boot menu.
- `build-2026.10.16-4` checked after publication: SHA-256 of the reassembled ISO, then the published ISO started in a
  VM on a simulated Windows PC in the "launched from Windows" mode: the "Partitions" step offers only "Remplacer une
  partition" and manual partitioning.
- Ideas for later versions: code signing for IXIN OS Setup, "IXIN OS in a window" from Linux, uninstall from Windows,
  an optional "macOS-style" layout (dock), an optional security toolbox, translations.

## 10. Suggested structure for os.ixin.online

1. **Accueil**: hero with the shield + "IXIN OS", tagline, **two buttons**: "Installer depuis Windows"
   (IXIN-OS-Setup.exe, stable link) and "Télécharger l'ISO"; version + "Bêta" badge; desktop screenshot.
2. **Fonctionnalités**: **Sécurité** (firewall, AppArmor, LUKS2, encrypted DNS, MAC), **Mises à jour automatiques**
   (IXIN Update: security fixes as soon as they're published, restore points, signed IXIN push), **Jeux**
   (Steam / Proton, Heroic, Lutris…), **Vie privée** (Tor, Signal, KeePassXC, VeraCrypt…), **Simplicité**
   (Windows-style desktop, IXIN Center, install from Windows, IXIN / Windows boot menu).
3. **Télécharger**: the two ways (§5), files, sizes, SHA-256 (§8), requirements, "Bêta" notice.
4. **Installer depuis Windows**: page adapted from `docs/INSTALLATION-WINDOWS.md` (steps, Secure Boot, BitLocker,
   undo, troubleshooting).
5. **Documentation**: USB installation, first steps, IXIN commands, gaming (Proton, anti-cheat), security, updates
   (signed, fingerprint), troubleshooting (black screen → compatibility mode, Secure Boot, BitLocker), FAQ (§11).
6. **Compatibilité**: supported / unsupported table (§7).
7. **Écosystème**: links to getixin.com and ixin.online (the IXIN app on Android / Windows / Mac).
8. Footer: "Basé sur Arch Linux", notice that IXIN OS is not affiliated with the Arch Linux project, GitHub link.

**Site style**: the same design language as getixin.com (black background `#050505`, emerald / sapphire accents, Inter,
shield logo, slow smooth animations). French as the primary language.

## 11. Ready-to-use French copy (facts checked against the program)

**Installer depuis Windows (bloc de la page d'accueil)**
> **Installez IXIN OS depuis Windows, sans clé USB.** Téléchargez IXIN OS Setup, lancez-le : il vérifie votre PC,
> télécharge IXIN OS, prépare la place sur le disque et redémarre sur l'installation. Windows et vos fichiers restent
> en place, et tout peut être annulé tant qu'IXIN OS n'est pas installé. Au démarrage, un menu vous laisse choisir
> entre IXIN OS et Windows.

**FAQ**
- **Windows affiche « Windows a protégé votre ordinateur ».** IXIN OS Setup n'est pas encore signé par un certificat
  de signature de code. Cliquez sur « Informations complémentaires » puis « Exécuter quand même ». Son code source
  est public.
- **Faut-il désactiver Secure Boot ?** Oui, pour l'instant : IXIN OS ne démarre pas encore avec Secure Boot activé.
  IXIN OS Setup redémarre directement dans les paramètres UEFI pour le faire. Attention : certains jeux Windows avec
  anti-triche (Valorant, Battlefield 6…) exigent Secure Boot sous Windows.
- **Mon Windows est chiffré (BitLocker).** IXIN OS Setup affiche votre clé de récupération avant de commencer :
  notez-la ailleurs que sur le PC. Il suspend BitLocker pendant 3 démarrages ; Windows le réactive ensuite tout seul.
  Dans le menu de démarrage, « Windows » démarre Windows comme d'habitude, par le firmware du PC.
- **Mes fichiers Windows sont-ils touchés ?** Non : Windows est seulement réduit pour libérer de la place, avec les
  outils de Windows lui-même. Une sauvegarde de vos fichiers importants reste conseillée avant toute modification
  d'un disque.
- **Combien de place faut-il ?** Environ 75 Go libres sur C: (45 Go minimum pour IXIN OS, 80 Go conseillés pour les
  jeux).
- **Peut-on annuler ?** Oui, tant qu'IXIN OS n'est pas installé : relancez IXIN OS Setup et choisissez « Annuler la
  préparation ». En cas d'erreur pendant la préparation, Windows est remis dans son état d'origine automatiquement.
- **Windows refuse d'être réduit.** Cliquez sur « Débloquer la réduction » dans IXIN OS Setup (fichiers système qui
  ne peuvent pas être déplacés), puis suivez le guide si cela ne suffit pas.
- **Après une mise à jour de Windows, le PC démarre directement sur Windows.** Relancez IXIN OS Setup et cliquez sur
  « Remettre le menu IXIN au démarrage », ou choisissez IXIN dans le menu de démarrage du PC (F12, F9, F8, F11 ou
  Échap selon la marque).
- **Mon PC n'est pas compatible avec IXIN OS Setup (BIOS, disque MBR).** Utilisez l'ISO sur une clé USB (Ventoy ou
  Rufus).
- **Comment IXIN OS se met-il à jour ?** Tout seul, comme Windows : les correctifs de sécurité dès leur publication,
  le reste une fois par jour, avec un point de restauration avant chaque mise à jour. Les mises à jour IXIN sont signées
  par la clé de l'équipe IXIN (empreinte publiée sur ce site).
