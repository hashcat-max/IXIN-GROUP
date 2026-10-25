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

# Secure Boot (clé IXIN_SECUREBOOT_KEY ou IXIN_SECUREBOOT_KEY_FILE + certificat) : chargeur signé, avec
# shim (signé par Microsoft), MokManager et le certificat IXIN, que IXIN OS Setup installe devant lui
SB_CRT="${IXIN_SECUREBOOT_CERT:-$ROOT/repo/secureboot/ixin-secureboot.crt}"
SB_KEY="${IXIN_SECUREBOOT_KEY_FILE:-}"
SB_TMP="$(mktemp -d)"
trap 'rm -rf "$SB_TMP"' EXIT
if [[ -n "${IXIN_SECUREBOOT_KEY:-}" ]]; then
    SB_KEY="$SB_TMP/key"; (umask 077; printf '%s\n' "$IXIN_SECUREBOOT_KEY" > "$SB_KEY")
fi
SBAT=()
rm -f "$OUT/shimx64.efi" "$OUT/mmx64.efi" "$OUT/ixin-secureboot.cer"
if [[ -n "$SB_KEY" && -f "$SB_CRT" ]]; then
    "$ROOT/tools/build-secureboot-efi.sh" "$SB_TMP/efi" "$SB_KEY" "$SB_CRT" >/dev/null
    cp "$SB_TMP/efi/shimx64.efi" "$SB_TMP/efi/mmx64.efi" "$SB_TMP/efi/ixin-secureboot.cer" "$OUT/"
    SBAT=(--sbat "$SB_TMP/efi/sbat.csv")
    echo "Secure Boot : chargeur signé, shim et MokManager ajoutés"
else
    echo "Secure Boot : pas de clé — chargeur non signé (Secure Boot à désactiver)"
fi

grub-mkstandalone -O x86_64-efi --compress=xz "${SBAT[@]}" \
    --modules="part_gpt part_msdos fat exfat ntfs iso9660 loopback linux chain search search_label search_fs_file test echo sleep all_video gfxterm font normal configfile efi_gop" \
    --locales="" --themes="" --fonts="unicode" \
    -o "$OUT/grubx64.efi" \
    "boot/grub/grub.cfg=$ROOT/windows/boot/setup-grub.cfg"

if (( ${#SBAT[@]} )); then
    sbsign --key "$SB_KEY" --cert "$SB_CRT" --output "$OUT/grubx64.signe.efi" "$OUT/grubx64.efi" >/dev/null
    mv "$OUT/grubx64.signe.efi" "$OUT/grubx64.efi"
    sbverify --cert "$SB_CRT" "$OUT/grubx64.efi" >/dev/null
fi
( cd "$OUT" && sha256sum grubx64.efi > grubx64.efi.sha256 )
ls -l "$OUT/grubx64.efi"
