#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  Construit le chargeur de démarrage de l'installation lancée depuis Windows :
#  un grubx64.efi autonome (menu windows/boot/setup-grub.cfg intégré) que
#  IXIN OS Setup copie dans la partition EFI de Windows (\EFI\IXIN-SETUP\).
#
#    tools/build-windows-boot.sh [dossier de sortie]   (Arch Linux, paquet grub)
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/build/windows-boot}")"

command -v grub-mkstandalone >/dev/null || { echo "grub-mkstandalone introuvable (paquet grub)" >&2; exit 1; }
[[ -d /usr/lib/grub/x86_64-efi ]] || { echo "modules GRUB x86_64-efi introuvables" >&2; exit 1; }
mkdir -p "$OUT"

grub-mkstandalone -O x86_64-efi --compress=xz \
    --modules="part_gpt part_msdos fat exfat ntfs iso9660 loopback linux chain search search_label search_fs_file test echo sleep all_video gfxterm font normal configfile efi_gop" \
    --locales="" --themes="" --fonts="unicode" \
    -o "$OUT/grubx64.efi" \
    "boot/grub/grub.cfg=$ROOT/windows/boot/setup-grub.cfg"

( cd "$OUT" && sha256sum grubx64.efi > grubx64.efi.sha256 )
ls -l "$OUT/grubx64.efi"
