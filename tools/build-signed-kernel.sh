#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS — noyau signé pour Secure Boot (linux-zen-ixin, linux-zen-ixin-headers)
#
#    tools/build-signed-kernel.sh <dossier des paquets> <clé> <certificat>      (root, Arch Linux)
#
#  Sous Secure Boot, shim ne laisse démarrer qu'un noyau signé par une clé de confiance. Ce script
#  reprend le noyau linux-zen officiel d'Arch Linux (paquets téléchargés par pacman, signatures
#  d'Arch vérifiées), signe son image avec la clé Secure Boot d'IXIN et le republie sous le nom
#  linux-zen-ixin (même version, mêmes fichiers) : il remplace linux-zen sur les PC IXIN OS.
#  Lancé à chaque publication du dépôt [ixin] (au moins toutes les 6 heures) : une nouvelle
#  version du noyau d'Arch arrive signée sur les PC IXIN OS en quelques heures.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

OUT="$(realpath -m "${1:?dossier des paquets}")"
KEY="${2:?clé Secure Boot (PEM)}"
CRT="${3:?certificat Secure Boot (PEM)}"
BUILD_USER=${IXIN_BUILD_USER:-ixinbuild}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
chmod 755 "$WORK"
msg(){ printf '\e[38;2;25;243;164m==>\e[0m \e[1m%s\e[0m\n' "$*"; }

pacman -S --needed --noconfirm sbsigntools >/dev/null
mkdir -p "$WORK/cache" "$OUT"
install -dm755 -o "$BUILD_USER" "$WORK/pkgout"
pacman -Sy >/dev/null
# Paquets officiels d'Arch (dépôt [extra]) : pacman vérifie leurs signatures au téléchargement
pacman -Sw --noconfirm --cachedir "$WORK/cache" extra/linux-zen extra/linux-zen-headers >/dev/null

info(){ bsdtar -xOf "$1" .PKGINFO | sed -n "s/^$2 = //p"; }

# Tableau bash d'une liste de valeurs (une par ligne), chaque valeur entre guillemets simples
array(){ local v out=""; while IFS= read -r v; do [ -n "$v" ] && out+=" '${v//\'/\'\\\'\'}'"; done; printf '(%s )' "$out"; }

for base in linux-zen linux-zen-headers; do
  pkg="$(ls "$WORK"/cache/"$base"-[0-9]*.pkg.tar.zst | head -n 1)"
  full="$(info "$pkg" pkgver)"                 # ex. 6.17.5.zen1-1
  ver="${full%-*}"; rel="${full##*-}"
  name="${base/linux-zen/linux-zen-ixin}"
  dir="$WORK/$name"
  mkdir -p "$dir/root"
  bsdtar -xf "$pkg" -C "$dir/root" --exclude '.PKGINFO' --exclude '.BUILDINFO' --exclude '.MTREE' --exclude '.INSTALL'
  if [ "$base" = linux-zen ]; then
    img="$(ls "$dir"/root/usr/lib/modules/*/vmlinuz)"
    sbsign --key "$KEY" --cert "$CRT" --output "$img.signe" "$img" >/dev/null
    sbverify --cert "$CRT" "$img.signe" >/dev/null
    mv "$img.signe" "$img"
    msg "Noyau $full signé ($(basename "$(dirname "$img")"))"
  fi
  (cd "$dir/root" && bsdtar -cf "$dir/contenu.tar" .)
  rm -rf "$dir/root"
  provides="$( { info "$pkg" provides; echo "$base=$full"; } | array)"
  cat > "$dir/PKGBUILD" <<EOF
# Généré par tools/build-signed-kernel.sh : $base $full d'Arch Linux, image du noyau signée IXIN (Secure Boot)
pkgname=$name
pkgver=$ver
pkgrel=$rel
pkgdesc="$(info "$pkg" pkgdesc) — signé pour Secure Boot (IXIN OS)"
arch=(x86_64)
url="$(info "$pkg" url)"
license=$(info "$pkg" license | array)
depends=$(info "$pkg" depend | array)
optdepends=$(info "$pkg" optdepend | array)
provides=$provides
conflicts=('$base')
replaces=('$base')
options=(!strip !debug !emptydirs)
source=(contenu.tar)
noextract=(contenu.tar)
sha256sums=(SKIP)

package() {
  bsdtar -xf "\$srcdir/contenu.tar" -C "\$pkgdir"
}
EOF
  chown -R "$BUILD_USER:" "$dir"
  (cd "$dir" && sudo -u "$BUILD_USER" env PKGDEST="$WORK/pkgout" makepkg --force --nodeps --noconfirm >/dev/null)
  cp "$WORK"/pkgout/"$name"-[0-9]*.pkg.tar.zst "$OUT/"
  msg "$name $full prêt"
done
