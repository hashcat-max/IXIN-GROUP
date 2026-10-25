#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS — démarrage de l'ISO avec Secure Boot activé
#
#    tools/secureboot-iso.sh <dossier de travail mkarchiso> <dépôt local>     (appelé par build.sh)
#
#  Chaîne : firmware → \EFI\BOOT\BOOTX64.EFI = shim (signé Microsoft) → grubx64.efi = systemd-boot
#  signé par IXIN → noyau linux-zen-ixin (signé). Au premier démarrage avec Secure Boot, shim ne
#  connaît pas encore la clé IXIN : il ouvre MokManager, où l'on enregistre ixin-secureboot.cer
#  (« Enroll key from disk », fichier à la racine de la clé USB). Une seule fois par PC.
#  Clé : IXIN_SECUREBOOT_KEY (ou IXIN_SECUREBOOT_KEY_FILE), certificat : IXIN_SECUREBOOT_CERT
#  (défaut repo/secureboot/ixin-secureboot.crt).
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
W="${1:?dossier de travail}"
REPO="${2:?dépôt local}"
CRT="${IXIN_SECUREBOOT_CERT:-$ROOT/repo/secureboot/ixin-secureboot.crt}"
T="$(mktemp -d)"
trap 'rm -rf "$T"' EXIT
if [[ -n "${IXIN_SECUREBOOT_KEY:-}" ]]; then
  KEY="$T/key"; (umask 077; printf '%s\n' "$IXIN_SECUREBOOT_KEY" > "$KEY")
else
  KEY="${IXIN_SECUREBOOT_KEY_FILE:?clé Secure Boot (IXIN_SECUREBOOT_KEY)}"
fi

pkg="$(ls "$REPO"/ixin-secureboot-*.pkg.tar.zst | head -n 1)"
bsdtar -xf "$pkg" -C "$T" usr/lib/ixin/secureboot
SB="$T/usr/lib/ixin/secureboot"

# 1. Arborescence de l'ISO (copies sur clé USB par Ventoy, Rufus en mode ISO…)
efi="$W/iso/EFI/BOOT"
sbsign --key "$KEY" --cert "$CRT" --output "$efi/grubx64.efi" "$efi/BOOTx64.EFI" >/dev/null
cp "$SB/shimx64.efi" "$efi/BOOTx64.EFI"
cp "$SB/mmx64.efi" "$efi/"
cp "$SB/ixin-secureboot.cer" "$W/iso/"

# 2. Image FAT de démarrage (partition EFI de la clé USB, image El Torito) : reconstruite un peu
#    plus grande avec les mêmes fichiers, plus shim, MokManager et le certificat
img="$W/efiboot.img"
mkdir -p "$T/fat"
mcopy -s -n -i "$img" ::/ "$T/fat/"
mv "$T/fat/EFI/BOOT/BOOTx64.EFI" "$T/fat/EFI/BOOT/sd-boot.efi"
sbsign --key "$KEY" --cert "$CRT" --output "$T/fat/EFI/BOOT/grubx64.efi" "$T/fat/EFI/BOOT/sd-boot.efi" >/dev/null
rm "$T/fat/EFI/BOOT/sd-boot.efi"
cp "$SB/shimx64.efi" "$T/fat/EFI/BOOT/BOOTx64.EFI"
cp "$SB/mmx64.efi" "$SB/ixin-secureboot.cer" "$T/fat/EFI/BOOT/"
cp "$SB/ixin-secureboot.cer" "$T/fat/"
kib=$(( $(du -sk "$T/fat" | cut -f1) + 4096 ))
rm -f "$img"
mkfs.fat -C -n ARCHISO_EFI "$img" "$kib" >/dev/null
mcopy -s -i "$img" "$T"/fat/* ::/

# 3. Vérifications : shim reconnu, chargeur et noyau signés par IXIN
for f in "$efi/grubx64.efi" "$W"/iso/*/boot/x86_64/vmlinuz-*; do
  sbverify --cert "$CRT" "$f" >/dev/null || { echo "signature IXIN absente : $f" >&2; exit 1; }
done
mcopy -n -i "$img" ::/EFI/BOOT/grubx64.efi "$T/check.efi"
sbverify --cert "$CRT" "$T/check.efi" >/dev/null
echo "ISO compatible Secure Boot : shim + systemd-boot signé, image de démarrage de ${kib} Kio"
