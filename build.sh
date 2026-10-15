#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
#  IXIN OS — construction de l'image ISO
#  À lancer en root sur Arch Linux (ou dans un conteneur archlinux --privileged)
#      sudo ./build.sh            → out/ixin-os-AAAA.MM.JJ-x86_64.iso
#
#  Les paquets IXIN (packages/*) sont signés avec la clé de l'équipe IXIN :
#      IXIN_SIGNING_KEY="$(cat cle-privee.asc)" sudo -E ./build.sh
#  Sans clé (tests locaux uniquement) : IXIN_ALLOW_UNSIGNED=1 sudo -E ./build.sh
# ═══════════════════════════════════════════════════════════════════
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROFILE="$ROOT/iso"
BUILD="${BUILD_DIR:-$ROOT/build}"
OUT="${OUT_DIR:-$ROOT/out}"
REPO="$BUILD/repo"
AUR_PKGS=(calamares)

msg(){ printf '\e[38;2;25;243;164m==>\e[0m \e[1m%s\e[0m\n' "$*"; }
die(){ printf '\e[31m==> ERREUR:\e[0m %s\n' "$*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "lancez ce script en root (sudo ./build.sh)"
command -v pacman >/dev/null || die "ce script doit tourner sur Arch Linux"

msg "Dépendances de construction"
grep -q '^\[multilib\]' /etc/pacman.conf || \
    printf '\n[multilib]\nInclude = /etc/pacman.d/mirrorlist\n' >> /etc/pacman.conf
pacman -Sy --noconfirm --needed archiso git base-devel sudo grub >/dev/null

msg "Clé et miroirs Chaotic-AUR"
if ! pacman-key --list-keys 3056513887B78AEB &>/dev/null; then
    pacman-key --init
    pacman-key --recv-key 3056513887B78AEB --keyserver keyserver.ubuntu.com
    pacman-key --lsign-key 3056513887B78AEB
fi
pacman -Q chaotic-keyring chaotic-mirrorlist &>/dev/null || pacman -U --noconfirm \
    'https://cdn-mirror.chaotic.cx/chaotic-aur/chaotic-keyring.pkg.tar.zst' \
    'https://cdn-mirror.chaotic.cx/chaotic-aur/chaotic-mirrorlist.pkg.tar.zst'

msg "Paquets AUR compilés localement : ${AUR_PKGS[*]}"
mkdir -p "$REPO" "$BUILD/aur"
id ixinbuild &>/dev/null || useradd -m ixinbuild
echo 'ixinbuild ALL=(ALL) NOPASSWD: /usr/bin/pacman' > /etc/sudoers.d/ixinbuild
chown -R ixinbuild: "$BUILD/aur"
for pkg in "${AUR_PKGS[@]}"; do
    if compgen -G "$REPO/$pkg-[0-9]*.pkg.tar.zst" >/dev/null; then
        msg "  $pkg déjà construit"; continue
    fi
    rm -rf "$BUILD/aur/$pkg"
    sudo -u ixinbuild git clone -q "https://aur.archlinux.org/$pkg.git" "$BUILD/aur/$pkg"
    (cd "$BUILD/aur/$pkg" && sudo -u ixinbuild makepkg -s --noconfirm --needed)
    find "$BUILD/aur/$pkg" -maxdepth 1 -name "$pkg-[0-9]*.pkg.tar.zst" -exec cp {} "$REPO/" \;
done

msg "Paquets IXIN (dépôt [ixin] signé)"
"$ROOT/tools/build-packages.sh" "$BUILD/ixin-repo"
rm -f "$REPO"/ixin-*.pkg.tar.zst "$REPO"/ixin-local.*
cp "$BUILD/ixin-repo"/ixin-*.pkg.tar.zst "$REPO/"
repo-add -q "$REPO/ixin-local.db.tar.gz" "$REPO"/*.pkg.tar.zst

msg "Préparation du profil"
WORKPROFILE="$BUILD/profile"
rm -rf "$WORKPROFILE"
cp -a "$PROFILE" "$WORKPROFILE"
sed -i "s|@LOCALREPO@|$REPO|" "$WORKPROFILE/pacman.conf"

msg "Construction de l'ISO (cela peut prendre 30 à 90 minutes)"
rm -rf "$BUILD/work"
mkdir -p "$OUT"
mkarchiso -v -w "$BUILD/work" -o "$OUT" "$WORKPROFILE"
rm -rf "$BUILD/work"

cd "$OUT"
for iso in ixin-os-*.iso; do sha256sum "$iso" > "$iso.sha256"; done
msg "Terminé :"
ls -lh "$OUT"
