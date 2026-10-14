#!/usr/bin/env bash
# IXIN OS — personnalisation du système live (exécuté dans le chroot par mkarchiso)
set -euo pipefail

# ── Langue / heure ──
sed -i 's/^#\(fr_FR.UTF-8\)/\1/; s/^#\(en_US.UTF-8\)/\1/' /etc/locale.gen
locale-gen

# ── Identité du système ──
/usr/local/bin/ixin-branding

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
    apparmor.service ufw.service fstrim.timer paccache.timer
systemctl disable iwd.service systemd-networkd.service 2>/dev/null || true
systemctl set-default graphical.target

# ── Pare-feu : tout refuser en entrée, tout autoriser en sortie ──
sed -i 's/^ENABLED=.*/ENABLED=yes/' /etc/ufw/ufw.conf
sed -i 's/^DEFAULT_INPUT_POLICY=.*/DEFAULT_INPUT_POLICY="DROP"/; s/^DEFAULT_FORWARD_POLICY=.*/DEFAULT_FORWARD_POLICY="DROP"/; s/^DEFAULT_OUTPUT_POLICY=.*/DEFAULT_OUTPUT_POLICY="ACCEPT"/; s/^IPV6=.*/IPV6=yes/' /etc/default/ufw

# ── DNS : resolv.conf géré par systemd-resolved ──
ln -sf /run/systemd/resolve/stub-resolv.conf /etc/resolv.conf

# ── Thème Plymouth (animation du thème spinner + logo IXIN) ──
cp -n /usr/share/plymouth/themes/spinner/{animation,throbber}-*.png /usr/share/plymouth/themes/ixin/ 2>/dev/null || true
cp -n /usr/share/plymouth/themes/spinner/{bullet,capslock,entry,keyboard,keymap-render,lock}.png /usr/share/plymouth/themes/ixin/ 2>/dev/null || true
plymouth-set-default-theme ixin || true

# ── Icône du menu Démarrer ──
for s in 16 22 24 32 48 64 128 256 512; do
    install -Dm644 /usr/share/ixin/branding/ixin-logo-$s.png /usr/share/icons/hicolor/${s}x${s}/apps/ixin.png
done
install -Dm644 /usr/share/ixin/branding/ixin-logo.svg /usr/share/icons/hicolor/scalable/apps/ixin.svg
gtk-update-icon-cache -q -f /usr/share/icons/hicolor || true

# ── Flathub ──
flatpak remote-add --if-not-exists --system flathub https://dl.flathub.org/repo/flathub.flatpakrepo || true

# ── Dossiers utilisateur ──
sudo -u liveuser LANG=fr_FR.UTF-8 xdg-user-dirs-update || true

# ── pacman du système (multilib + Chaotic-AUR pour les mises à jour) ──
if ! grep -q '^\[multilib\]' /etc/pacman.conf; then
    sed -i 's/^#\[multilib\]/[multilib]/; /^\[multilib\]/{n;s/^#Include/Include/}' /etc/pacman.conf
fi
grep -q '^\[chaotic-aur\]' /etc/pacman.conf || printf '\n[chaotic-aur]\nInclude = /etc/pacman.d/chaotic-mirrorlist\n' >> /etc/pacman.conf
sed -i 's/^#ParallelDownloads.*/ParallelDownloads = 8/; s/^#Color/Color/' /etc/pacman.conf

# ── Polices du thème GRUB ──
for f in "DejaVuSans:14" "DejaVuSans:16" "DejaVuSans:18" "DejaVuSans-Bold:18" "DejaVuSansMono:14"; do
    IFS=: read -r n s <<<"$f"
    grub-mkfont -s "$s" -o "/usr/share/grub/themes/ixin/${n}-${s}.pf2" "/usr/share/fonts/TTF/${n}.ttf" || true
done
