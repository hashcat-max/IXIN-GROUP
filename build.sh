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
# Sources téléchargées une fois pour toutes dans $BUILD/srcdest (on peut aussi les y déposer
# à l'avance). Correctifs IXIN : iso/patches/<paquet>/*.patch, appliqués après extraction ;
# leur empreinte est notée à côté du paquet, reconstruit dès qu'un correctif change.
mkdir -p "$REPO" "$BUILD/aur" "$BUILD/srcdest"
id ixinbuild &>/dev/null || useradd -m ixinbuild
echo 'ixinbuild ALL=(ALL) NOPASSWD: /usr/bin/pacman' > /etc/sudoers.d/ixinbuild
chown -R ixinbuild: "$BUILD/aur" "$BUILD/srcdest"
for pkg in "${AUR_PKGS[@]}"; do
    patches=()
    [[ -d "$ROOT/iso/patches/$pkg" ]] && mapfile -t patches < <(find "$ROOT/iso/patches/$pkg" -name '*.patch' | sort)
    stamp="$(cat /dev/null "${patches[@]}" | sha256sum | cut -c1-16)"
    if compgen -G "$REPO/$pkg-[0-9]*.pkg.tar.zst" >/dev/null && [[ "$(cat "$REPO/$pkg.ixin-patches" 2>/dev/null)" == "$stamp" ]]; then
        msg "  $pkg déjà construit"; continue
    fi
    rm -f "$REPO/$pkg"-[0-9]*.pkg.tar.zst
    rm -rf "$BUILD/aur/$pkg"
    sudo -u ixinbuild git clone -q "https://aur.archlinux.org/$pkg.git" "$BUILD/aur/$pkg"
    (
        cd "$BUILD/aur/$pkg"
        mk=(sudo -u ixinbuild env SRCDEST="$BUILD/srcdest" makepkg --noconfirm)
        if (( ${#patches[@]} )); then
            "${mk[@]}" -s --needed --nobuild
            for p in "${patches[@]}"; do
                target="$(sed -n 's#^+++ b/##p' "$p" | head -1)"
                top="$(for d in src/*/; do [[ -f "$d$target" ]] && echo "$d" && break; done)"
                [[ -n "$top" ]] || die "correctif $(basename "$p") : $target introuvable dans les sources de $pkg"
                msg "  correctif IXIN : $(basename "$p")"
                sudo -u ixinbuild patch -d "$top" -p1 --forward --no-backup-if-mismatch < "$p" \
                    || die "correctif $(basename "$p") non applicable"
            done
            "${mk[@]}" --noextract
        else
            "${mk[@]}" -s --needed
        fi
    )
    find "$BUILD/aur/$pkg" -maxdepth 1 -name "$pkg-[0-9]*.pkg.tar.zst" -exec cp {} "$REPO/" \;
    echo "$stamp" > "$REPO/$pkg.ixin-patches"
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
