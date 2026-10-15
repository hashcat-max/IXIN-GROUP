#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  Active IXIN Update sur un PC installé avec une BÊTA d'IXIN OS
#  (les ISO publiées à partir du 15/10/2026 l'intègrent déjà).
#
#    curl -fsSL https://raw.githubusercontent.com/hashcat-max/IXIN-GROUP/HEAD/tools/ixin-enable-updates.sh | sudo bash
#
#  Ce que fait le script :
#    1. ajoute le dépôt signé [ixin] à pacman ;
#    2. installe la clé de l'équipe IXIN (récupérée sur le dépôt officiel en HTTPS ;
#       vous pouvez exiger une empreinte précise avec IXIN_KEY_FPR=…) ;
#    3. installe ixin-desktop (IXIN Update, outils et réglages IXIN, désormais
#       mis à jour automatiquement) à la place des copies de la bêta.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

REPO_URL="${IXIN_REPO_URL:-https://github.com/hashcat-max/IXIN-GROUP/releases/download/repo}"
G='\e[38;2;25;243;164m'; R='\e[31m'; N='\e[0m'
msg(){ printf "${G}==>${N} %s\n" "$*"; }
die(){ printf "${R}==> ERREUR :${N} %s\n" "$*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "lancez ce script avec sudo"
[[ -d /run/archiso ]] && die "à lancer sur IXIN OS installé, pas sur la clé live"
command -v pacman >/dev/null || die "pacman introuvable : ce n'est pas IXIN OS"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

msg "Dépôt [ixin] : $REPO_URL"
if [[ ! -f /etc/pacman.d/ixin-mirrorlist ]]; then
  printf '# IXIN OS — serveurs du dépôt de mises à jour [ixin]\nServer = %s\n' "$REPO_URL" > /etc/pacman.d/ixin-mirrorlist
fi
if ! grep -q '^\[ixin\]' /etc/pacman.conf; then
  cp /etc/pacman.conf /etc/pacman.conf.avant-ixin-update
  awk '/^\[core\]/ && !done { print "[ixin]\nSigLevel = Required\nInclude = /etc/pacman.d/ixin-mirrorlist\n"; done=1 } { print }' \
    /etc/pacman.conf.avant-ixin-update > /etc/pacman.conf
fi

msg "Clé de signature de l'équipe IXIN"
curl -fsSL --retry 3 -o "$TMP/ixin.db" "$REPO_URL/ixin.db" || die "dépôt [ixin] injoignable"
desc="$(bsdtar -tf "$TMP/ixin.db" | grep -m1 '^ixin-keyring-.*/desc$')" || die "ixin-keyring absent du dépôt"
keyring_file="$(bsdtar -xOf "$TMP/ixin.db" "$desc" | sed -n '/^%FILENAME%$/{n;p;q}')"
[[ "$keyring_file" =~ ^ixin-keyring-[^/]+\.pkg\.tar\.zst$ ]] || die "nom de paquet ixin-keyring invalide"
curl -fsSL --retry 3 -o "$TMP/keyring.pkg.tar.zst" "$REPO_URL/$keyring_file" ||
  die "téléchargement de $keyring_file impossible"
bsdtar -xf "$TMP/keyring.pkg.tar.zst" -C "$TMP" usr/share/pacman/keyrings
fpr="$(cut -d: -f1 "$TMP/usr/share/pacman/keyrings/ixin-trusted" | head -1)"
[[ "$fpr" =~ ^[0-9A-F]{40}$ ]] || die "clé IXIN invalide dans le dépôt"
if [[ -n "${IXIN_KEY_FPR:-}" && "${IXIN_KEY_FPR^^}" != "$fpr" ]]; then
  die "empreinte inattendue : $fpr (attendue : $IXIN_KEY_FPR)"
fi
echo "    empreinte : $fpr"
pacman-key --add "$TMP/usr/share/pacman/keyrings/ixin.gpg" >/dev/null
pacman-key --lsign-key "$fpr" >/dev/null

msg "Installation d'IXIN Update et des composants IXIN"
# Les fichiers installés par la bêta (hors pacman) sont remplacés par ceux des paquets.
pacman -Sy --noconfirm --needed \
  --overwrite '/usr/share/ixin/*' --overwrite '/usr/share/color-schemes/IXIN.colors' \
  --overwrite '/usr/share/plasma/look-and-feel/org.ixin.desktop/*' \
  --overwrite '/usr/share/plymouth/themes/ixin/*' --overwrite '/usr/share/grub/themes/ixin/*' \
  --overwrite '/usr/share/sddm/themes/breeze/theme.conf.user' --overwrite '/usr/share/icons/hicolor/*/apps/ixin.*' \
  --overwrite '/etc/xdg/kdeglobals' --overwrite '/etc/xdg/kwinrc' --overwrite '/etc/xdg/ksplashrc' \
  --overwrite '/etc/modprobe.d/ixin-nvidia.conf' \
  --overwrite '/etc/xdg/kscreenlockerrc' --overwrite '/etc/xdg/plasmarc' \
  --overwrite '/etc/xdg/autostart/ixin-*' --overwrite '/etc/skel/.config/autostart/*' \
  --overwrite '/usr/share/applications/ixin-*' --overwrite '/usr/share/pacman/keyrings/ixin*' \
  ixin-desktop

systemctl enable --now ixin-update.timer >/dev/null
msg "Terminé : IXIN Update est actif (Centre IXIN → Mises à jour)."
