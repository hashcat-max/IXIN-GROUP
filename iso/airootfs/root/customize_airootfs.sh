#!/usr/bin/env bash
# IXIN OS — personnalisation du système live (exécuté dans le chroot par mkarchiso)
# Les fichiers IXIN (outils, réglages, thèmes, IXIN Update) viennent des paquets ixin-*
# afin d'être mis à jour après l'installation ; ce script ne règle que le reste.
set -euo pipefail

# ── Langue / heure ──
sed -i 's/^#\(fr_FR.UTF-8\)/\1/; s/^#\(en_US.UTF-8\)/\1/' /etc/locale.gen
locale-gen

# ── Identité du système (également réappliquée par le paquet ixin-settings) ──
/usr/lib/ixin/ixin-branding

# ── Utilisateur live (sans mot de passe, supprimé par l'installeur) ──
if ! id liveuser &>/dev/null; then
    useradd -m -u 1000 -G wheel,audio,video,input,storage,network,gamemode -s /bin/bash -c "IXIN Live" liveuser
    passwd -d liveuser
fi
install -d -o liveuser -g liveuser /home/liveuser/Desktop
install -m 755 -o liveuser -g liveuser /usr/share/applications/ixin-install.desktop /home/liveuser/Desktop/
install -m 755 -o liveuser -g liveuser /usr/share/applications/ixin-center.desktop /home/liveuser/Desktop/
# Autoriser l'exécution des raccourcis de bureau (KDE)
for f in /home/liveuser/Desktop/*.desktop; do gio set "$f" metadata::trusted true 2>/dev/null || true; done

# ── Services ──
systemctl enable NetworkManager.service sddm.service bluetooth.service \
    systemd-resolved.service systemd-timesyncd.service power-profiles-daemon.service \
    apparmor.service ufw.service fstrim.timer paccache.timer reflector.timer \
    ixin-update.timer ixin-update-bootcheck.service linux-modules-cleanup.service \
    cups.socket avahi-daemon.service
systemctl disable iwd.service systemd-networkd.service 2>/dev/null || true
systemctl set-default graphical.target

# ── Miroirs Arch : les 10 plus rapides parmi les 20 plus à jour (HTTPS), chaque semaine ──
cat > /etc/xdg/reflector/reflector.conf <<'EOF'
# IXIN OS — sélection automatique des miroirs Arch Linux (reflector.timer, hebdomadaire)
--save /etc/pacman.d/mirrorlist
--protocol https
--latest 20
--sort rate
--number 10
EOF

# ── Pare-feu : tout refuser en entrée, tout autoriser en sortie ──
sed -i 's/^ENABLED=.*/ENABLED=yes/' /etc/ufw/ufw.conf
sed -i 's/^DEFAULT_INPUT_POLICY=.*/DEFAULT_INPUT_POLICY="DROP"/; s/^DEFAULT_FORWARD_POLICY=.*/DEFAULT_FORWARD_POLICY="DROP"/; s/^DEFAULT_OUTPUT_POLICY=.*/DEFAULT_OUTPUT_POLICY="ACCEPT"/; s/^IPV6=.*/IPV6=yes/' /etc/default/ufw

# ── Imprimantes réseau : découverte (mDNS) sans annoncer le PC sur le réseau ──
sed -i 's/^#\?disable-publishing=.*/disable-publishing=yes/; s/^#\?publish-workstation=.*/publish-workstation=no/' /etc/avahi/avahi-daemon.conf
if ! grep -q mdns_minimal /etc/nsswitch.conf; then
    sed -i -E 's/^(hosts:.*) resolve /\1 mdns_minimal [NOTFOUND=return] resolve /' /etc/nsswitch.conf
fi
grep -E '^(disable-publishing|publish-workstation)=' /etc/avahi/avahi-daemon.conf || true
grep '^hosts:' /etc/nsswitch.conf || true

# ── Thème Plymouth IXIN (fourni par ixin-artwork) ──
plymouth-set-default-theme ixin || true
gtk-update-icon-cache -q -f /usr/share/icons/hicolor || true

# ── Flathub ──
flatpak remote-add --if-not-exists --system flathub https://dl.flathub.org/repo/flathub.flatpakrepo || true

# ── Dossiers utilisateur ──
runuser -u liveuser -- env LANG=fr_FR.UTF-8 xdg-user-dirs-update || true

# ── pacman du système : [ixin] (mises à jour IXIN signées) + multilib + Chaotic-AUR ──
if ! grep -q '^\[multilib\]' /etc/pacman.conf; then
    sed -i 's/^#\[multilib\]/[multilib]/; /^\[multilib\]/{n;s/^#Include/Include/}' /etc/pacman.conf
fi
grep -q '^\[chaotic-aur\]' /etc/pacman.conf || printf '\n[chaotic-aur]\nInclude = /etc/pacman.d/chaotic-mirrorlist\n' >> /etc/pacman.conf
if ! grep -q '^\[ixin\]' /etc/pacman.conf; then
    # Placé avant [core] : l'équipe IXIN peut ainsi publier un correctif urgent d'un paquet Arch.
    awk '/^\[core\]/ && !done { print "# Dépôt IXIN : paquets signés par l'"'"'équipe IXIN (mises à jour d'"'"'IXIN OS)\n[ixin]\nSigLevel = Required\nInclude = /etc/pacman.d/ixin-mirrorlist\n"; done=1 } { print }' \
        /etc/pacman.conf > /etc/pacman.conf.new && mv /etc/pacman.conf.new /etc/pacman.conf
fi
sed -i 's/^#ParallelDownloads.*/ParallelDownloads = 8/; s/^#Color/Color/' /etc/pacman.conf

# ── Live : pas de verrouillage ni d'extinction d'écran ──
install -d -o liveuser -g liveuser /home/liveuser/.config
cat > /home/liveuser/.config/kscreenlockerrc <<'CFG'
[Daemon]
Autolock=false
LockOnResume=false
CFG
cat > /home/liveuser/.config/powerdevilrc <<'CFG'
[AC][Display]
TurnOffDisplayWhenIdle=false
DimDisplayWhenIdle=false

[AC][SuspendAndShutdown]
AutoSuspendAction=0
CFG
chown -R liveuser:liveuser /home/liveuser/.config
