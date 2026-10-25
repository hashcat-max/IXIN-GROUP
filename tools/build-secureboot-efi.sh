#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS — fichiers EFI pour démarrer avec Secure Boot activé
#
#    tools/build-secureboot-efi.sh <dossier> <clé> <certificat>        (root, Arch Linux)
#
#  Chaîne de démarrage : firmware → shim (signé par Microsoft) → GRUB d'IXIN (signé avec la clé
#  Secure Boot d'IXIN) → noyau linux-zen-ixin (signé, tools/build-signed-kernel.sh).
#  Produit dans <dossier> :
#    shimx64.efi, mmx64.efi, fbx64.efi   shim, MokManager, fallback (paquet shim-signed)
#    grubx64.efi                          GRUB autonome d'IXIN OS installé, signé, modules intégrés
#                                         (sous Secure Boot, GRUB refuse les modules lus sur le disque)
#    ixin-secureboot.cer                  certificat (format DER) à enregistrer dans MokManager
#    sbat.csv                             générations SBAT déclarées
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

OUT="$(realpath -m "${1:?dossier de sortie}")"
KEY="${2:?clé Secure Boot (PEM)}"
CRT="${3:?certificat Secure Boot (PEM)}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$OUT"

# shim signé par Microsoft : paquet shim-signed du dépôt Chaotic-AUR (shim de Fedora)
if ! pacman -Si shim-signed >/dev/null 2>&1; then
  if ! pacman-key --list-keys 3056513887B78AEB >/dev/null 2>&1; then
    pacman-key --recv-key 3056513887B78AEB --keyserver keyserver.ubuntu.com >/dev/null 2>&1
    pacman-key --lsign-key 3056513887B78AEB >/dev/null 2>&1
  fi
  pacman -U --noconfirm --needed 'https://cdn-mirror.chaotic.cx/chaotic-aur/chaotic-keyring.pkg.tar.zst' \
    'https://cdn-mirror.chaotic.cx/chaotic-aur/chaotic-mirrorlist.pkg.tar.zst' >/dev/null
  grep -q '^\[chaotic-aur\]' /etc/pacman.conf || printf '\n[chaotic-aur]\nInclude = /etc/pacman.d/chaotic-mirrorlist\n' >> /etc/pacman.conf
  pacman -Sy >/dev/null
fi
pacman -S --needed --noconfirm grub sbsigntools shim-signed >/dev/null
SHIM=/usr/share/shim-signed
cp "$SHIM/shimx64.efi" "$SHIM/mmx64.efi" "$SHIM/fbx64.efi" "$OUT/"
openssl x509 -in "$CRT" -outform DER -out "$OUT/ixin-secureboot.cer"

# SBAT : génération de GRUB exigée par shim (ici celle de GRUB lui-même), plus une ligne IXIN
grub_ver="$(pacman -Q grub | awk '{print $2}' | sed 's/^[0-9]*://; s/-.*//')"
# Génération SBAT de ce GRUB : grub-mkimage l'indique quand on lui en déclare une autre
printf 'sbat,1,SBAT Version,sbat,1,https://github.com/rhboot/shim/blob/main/SBAT.md\ngrub,1,x,grub,1,x\n' > "$WORK/probe.csv"
grub_gen="$(grub-mkimage -O x86_64-efi -o "$WORK/probe.efi" -p / --sbat "$WORK/probe.csv" normal 2>&1 |
            sed -n 's/.*build-time Global SBAT level \([0-9]*\).*/\1/p')"
[ -n "$grub_gen" ] || { echo "génération SBAT de GRUB introuvable" >&2; exit 1; }
cat > "$OUT/sbat.csv" <<EOF
sbat,1,SBAT Version,sbat,1,https://github.com/rhboot/shim/blob/main/SBAT.md
grub,$grub_gen,Free Software Foundation,grub,$grub_ver,https://www.gnu.org/software/grub/
grub.ixin,1,IXIN Group,grub,$grub_ver,https://github.com/hashcat-max/IXIN-GROUP
EOF

# Modules intégrés : tout ce que le menu d'IXIN OS (grub-mkconfig, thème IXIN, entrée Windows) utilise
MODULES="normal configfile echo test true sleep reboot halt minicmd loadenv regexp probe
  part_gpt part_msdos fat exfat ext2 btrfs ntfs iso9660 loopback gzio zstd xzio lzopio
  search search_fs_uuid search_fs_file search_label linux chain efifwsetup efibootnext bli tpm
  all_video efi_gop gfxterm gfxterm_background gfxmenu png jpeg font"

# Configuration intégrée : retrouve la partition /boot d'IXIN OS (fichier repère posé par
# ixin-secureboot), puis lit le menu habituel /boot/grub/grub.cfg
cat > "$WORK/early.cfg" <<'EOF'
search --no-floppy --file --set=root /grub/ixin-boot
if [ -n "$root" ]; then
    set prefix=($root)/grub
else
    search --no-floppy --file --set=root /boot/grub/ixin-boot
    set prefix=($root)/boot/grub
fi
configfile $prefix/grub.cfg
EOF
grub-mkimage -O x86_64-efi -o "$WORK/grubx64.efi" -p /grub --sbat "$OUT/sbat.csv" -c "$WORK/early.cfg" $MODULES
sbsign --key "$KEY" --cert "$CRT" --output "$OUT/grubx64.efi" "$WORK/grubx64.efi" >/dev/null
sbverify --cert "$CRT" "$OUT/grubx64.efi" >/dev/null
echo "Fichiers Secure Boot prêts dans $OUT (GRUB $grub_ver, SBAT grub,$grub_gen)"
ls -1 "$OUT"
