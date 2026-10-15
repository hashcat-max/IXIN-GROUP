# IXIN OS: complete context (to hand to another Claude project)

> This document summarizes everything that has been built for **IXIN OS**. It is meant to
> serve as the brief for building the site **os.ixin.online** (presentation, downloads,
> documentation). Everything stated here has been built and tested unless explicitly
> marked otherwise. Please do not invent features that are not listed here.

---

## 1. What IXIN OS is

- A **Linux distribution based on Arch Linux** (a rolling release, so always up to date), created by **IXIN Group**.
  It is the "operating system" companion to the IXIN ecosystem:
  - **getixin.com**: "IXin, Communication absolue". End-to-end encrypted private messaging, voice and video calls, voice Hubs, disposable email, anonymous VPN. Available on Windows, Mac, Linux, Android, Chromebook and the browser.
  - **ixin.online**: the IXIN web app.
- Positioning: **security · privacy · performance**. A system that is secure by default, comfortable for everyday use (the desktop looks like Windows), and capable of running Steam and Windows games.
- Tagline used across the OS: **« Sécurité · Vie privée · Performance »**.
- Source code: GitHub repository `hashcat-max/IXIN-GROUP`, branch `ccr-f4f50bff-01pzqi`.
  ⚠️ If the repository is **private**, the GitHub download links will not work for the
  public. In that case, make the repo public or host the ISO elsewhere (see §8).

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

## 3. Screenshots (real, taken in a VM during testing)

In `docs/screenshots/`:
- `bureau.png`: desktop with the Windows 11-style taskbar
- `centre-ixin.png`: the IXIN Center application (welcome screen)
- `menu-demarrage-live.png`: boot menu of the USB stick / ISO
- `deverrouillage-chiffrement.png`: IXIN boot screen asking for the encrypted disk password
- `ecran-connexion.png`: login screen of the installed system
- `ixin-secure.png`: security status in the terminal (installed system)
- `grub-installe.png`: IXIN boot screen of the installed system
- `installeur-emplacement.png`, `installeur-partitions-chiffrement.png`, `installeur-utilisateur.png`, `installeur-diaporama.png`: graphical installer

(Screenshots at 1280×800, taken in a VM without 3D acceleration.)

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
An in-house graphical application that opens on first login. 4 tabs:
- **Accueil**: system information (kernel, CPU, GPU, RAM), Install button (in live mode), Update, links to getixin.com / ixin.online.
- **Sécurité**: protection status (firewall, AppArmor, disk encryption, encrypted DNS, OpenSnitch, USBGuard) and one-click buttons to enable them; Tor Browser, KeePassXC, VeraCrypt, metadata cleaning, Lynis audit, antivirus scan, rootkit check.
- **Jeux**: Steam, Heroic, Lutris, Bottles, ProtonUp-Qt, power profiles (performance / balanced), NVIDIA settings, and a Steam tip.
- **Système**: full update, Discover / Flathub software store, Timeshift snapshots, partition manager, settings, system monitor, BleachBit, system log.

### Gaming and Windows compatibility
- **Steam** + **Proton** (runs most Windows games), **Heroic** (Epic, GOG, Amazon), **Lutris**, **Bottles**,
  **Wine-staging** + Winetricks, **ProtonUp-Qt** (Proton-GE), **GameMode**, **MangoHud**, **Gamescope**.
- **linux-zen** kernel (tuned for responsiveness), zram (compressed RAM), BBR network congestion control, gaming-oriented sysctl settings.
- AMD, Intel and NVIDIA drivers (open NVIDIA kernel modules), 32- and 64-bit Vulkan.
- `ixin-gaming %command%` command for Steam launch options (GameMode + MangoHud).
- **Honest limitation, to state on the site**: games whose anti-cheat blocks Linux
  (e.g. Valorant, Fortnite, some Call of Duty titles, Rainbow Six Siege) **do not work**, on any Linux distribution.
  For those, keep Windows in dual boot. You can check a game on protondb.com.

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
- `ixin-update` command: system + Flatpak + firmware updates, plus a list of packages with known vulnerabilities (arch-audit).
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
- "Mises à jour" tab in the IXIN Center (status, security fixes with their CVEs, history, mode, restore points).

### Privacy and tools
Tor Browser, Tor + torsocks, Signal, KeePassXC, VeraCrypt, GnuPG + Kleopatra, mat2 / Metadata Cleaner,
BleachBit, WireGuard / OpenVPN (NetworkManager integration).

### Preinstalled applications
Firefox, Thunderbird, LibreOffice, VLC, mpv, OBS Studio, GIMP, Dolphin, Kate, Okular, Gwenview,
Spectacle, Discover + Flathub, Timeshift + Snapper (Btrfs snapshots), KDE Connect.

## 5. Installation (graphical installer: Calamares)

- Boot from a USB stick → **live** desktop (try it without installing) → "Installer IXIN OS" icon.
- Steps: Welcome → Location → Keyboard → **Partitions** → User → Summary → Install → Finish.
- Partitioning options: **"Install alongside Windows"** (automatic Windows shrinking, offered when Windows is detected),
  **"Erase disk"**, or **manual partitioning**.
- Layout: **1 GB EFI** partition (boot) + **Btrfs** system (subvolumes `@`, `@home`, `@cache`, `@log`,
  compatible with Timeshift snapshots), with optional **LUKS2** encryption.
- **GRUB** bootloader with **automatic Windows detection** (dual-boot menu).
- Requirements: 64-bit (x86_64) PC with **UEFI** (legacy BIOS works for the live system), **4 GB RAM** minimum (8+ recommended), **40 GB** of disk.

### Dual-boot guide with Windows (to put in the docs)
1. In Windows: turn off **Fast Startup** and **suspend BitLocker** if the disk is encrypted.
2. Write the ISO to a USB stick with **Rufus** (GPT / UEFI) or **Ventoy**.
3. In the BIOS / UEFI: **disable Secure Boot** (IXIN OS does not yet provide a signed bootloader), then boot from the stick.
4. Live desktop → "Installer IXIN OS" → **Install alongside** → drag the slider → check **Encrypt the system**.
5. On reboot, the IXIN menu offers IXIN OS **and Windows**.
6. Clock off by an hour under Windows: in an administrator terminal on Windows, run
   `reg add "HKLM\System\CurrentControlSet\Control\TimeZoneInformation" /v RealTimeIsUniversal /d 1 /t REG_DWORD /f`

### Live boot menu options
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
- ✅ **Virtual machines**: VirtualBox, VMware, QEMU / KVM, Hyper-V (guest tools included).
- ⚠️ **Chromebooks**: only x86 (Intel / AMD) models with alternative UEFI firmware (MrChromebox). Not tested.
- ❌ **Android phones / ARM tablets**: not supported (that would require a separate ARM port per device).
  The IXIN app on Android is the ecosystem's answer for phones.
- ❌ **Mac with Apple Silicon (M1–M4)**: not supported. Intel Macs: possible, but not tested.
- ❌ **Secure Boot**: must be disabled (signing planned for a later version).

## 8. Downloads

- The ISO (~4.8 GB, `ixin-os-YYYY.MM.DD-x86_64.iso`) is built automatically by **GitHub Actions**
  (workflow "Construire l'ISO IXIN OS") and published in the repository's **GitHub Releases**,
  **split into 1.9 GB parts** (GitHub limit of 2 GB per file), with a `.sha256` file.
- Latest release page: `https://github.com/hashcat-max/IXIN-GROUP/releases/latest`
- Reassemble the ISO:
  - Linux / macOS: `cat ixin-os-*.iso.part* > ixin-os.iso` then `sha256sum -c ixin-os-*.iso.sha256`
  - Windows (cmd): `copy /b ixin-os-….iso.part00+ixin-os-….iso.part01+ixin-os-….iso.part02 ixin-os.iso`
- **Recommendation for the site**: a single direct download is far more convenient for the public than split parts.
  Options: host the full ISO on object storage (Cloudflare R2, Backblaze B2, a CDN) and add a **torrent** link,
  showing the size, version, date and SHA-256 hash on the download page.
- Writing the ISO to a USB stick: **Ventoy** (recommended) or **Rufus** (Windows), **balenaEtcher** (Windows / Mac / Linux).

## 9. Project status (to stay honest on the site)

- Version: **first version (v0.1)**, rolling, dated builds.
- Tested in a VM: ISO build ✅, UEFI boot ✅, live desktop + taskbar ✅, IXIN Center ✅,
  graphical installer ✅, full encrypted install ✅, **reboot of the installed system ✅** (GRUB → IXIN unlock screen →
  login → desktop; firewall, AppArmor, LUKS2 and encrypted DNS checked on the installed system).
  Several bugs found during these tests have been fixed (missing `unsquashfs`, installer readability,
  GRUB unable to read the kernel on encrypted Btrfs; the latter was fixed by moving `/boot` to the EFI partition).
- ⚠️ The **first published release (`build-2026.10.15-1`)** predates the GRUB fix: an install from it
  may fail to boot. Use the **next** build (`build-2026.10.15-2` or later).
- Not tested on real hardware yet (VM only): label it **"Beta"** on the site.
- IXIN Update tested in a VM, including a full install from the ISO: restore point on first boot, automatic
  updates, urgent IXIN pushes installed automatically (restore point, notification, IXIN announcement),
  forged policy / replayed policy / tampered repo database all rejected, repair after a power cut mid-update.
- ⚠️ Releases `build-2026.10.15-1` and `-2` predate IXIN Update. PCs installed from them enable it with
  `tools/ixin-enable-updates.sh` (see docs/MISES-A-JOUR.md); the next ISO, built once the IXIN signing key
  is configured on GitHub, includes it.
- Ideas for later versions: Secure Boot signing, an optional "macOS-style" layout (dock), an optional security toolbox,
  a Windows helper that prepares the USB stick, a dedicated package repository, translations.

## 10. Suggested structure for os.ixin.online

1. **Home**: hero with the shield + "IXIN OS", tagline, big "Télécharger" button (version, size), desktop screenshot.
2. **Features**: 5 sections. **Sécurité** (firewall, AppArmor, LUKS2, encrypted DNS, MAC), **Mises à jour automatiques**
   (IXIN Update: security fixes as soon as they're published, restore points, IXIN push), **Jeux** (Steam / Proton, Heroic, Lutris…),
   **Vie privée** (Tor, Signal, KeePassXC, VeraCrypt…), **Simplicité** (Windows-style desktop, IXIN Center, graphical installer).
3. **Download**: link(s), SHA-256, requirements, how to write the USB stick, "Beta" notice.
4. **Documentation**: installation, dual boot with Windows, first steps, IXIN commands, gaming (Proton, anti-cheat), security, FAQ, troubleshooting (black screen → compatibility mode, Secure Boot, BitLocker).
5. **Compatibility**: supported / unsupported table (§7).
6. **Ecosystem**: links to getixin.com and ixin.online (the IXIN app on Android / Windows / Mac).
7. Footer: "Basé sur Arch Linux", notice that IXIN OS is not affiliated with the Arch Linux project, GitHub link.

**Site style**: the same design language as getixin.com (black background `#050505`, emerald / sapphire accents, Inter,
shield logo, slow smooth animations). French as the primary language.
