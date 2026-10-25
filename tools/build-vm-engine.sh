#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS — moteur de la machine virtuelle IXIN pour Windows (ixin-vm-engine-win64.zip)
#
#  À lancer dans MSYS2 (UCRT64), sur Windows :   tools/build-vm-engine.sh <dossier de sortie>
#
#  IXIN OS Setup propose d'utiliser IXIN OS « dans une fenêtre », sans toucher au disque du PC :
#  il télécharge ce moteur avec IXIN OS. Le moteur est QEMU (logiciel libre, GPL) tel que compilé
#  par le projet MSYS2, réduit à ce qui sert : le PC virtuel x86_64, l'outil de disque virtuel,
#  leurs bibliothèques et le firmware UEFI. Accélération par l'hyperviseur de Windows (WHPX).
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

OUT="${1:?dossier de sortie}"
PREFIX=/ucrt64
STAGE="$(mktemp -d)/ixin-vm-engine"
mkdir -p "$STAGE/share" "$OUT"

# Miroirs MSYS2 parfois lents ou indisponibles : jusqu'à 5 essais
for try in 1 2 3 4 5; do
  pacman -S --noconfirm --needed mingw-w64-ucrt-x86_64-qemu mingw-w64-ucrt-x86_64-ntldd >/dev/null && break
  [ "$try" = 5 ] && { echo "Installation de QEMU impossible (miroirs MSYS2)" >&2; exit 1; }
  echo "Miroir MSYS2 en échec, nouvel essai dans $((try * 15)) s" >&2
  sleep $((try * 15))
done
version="$(pacman -Q mingw-w64-ucrt-x86_64-qemu | awk '{print $2}')"

# Programmes
for exe in qemu-system-x86_64.exe qemu-system-x86_64w.exe qemu-img.exe; do
  cp "$PREFIX/bin/$exe" "$STAGE/"
done

# Bibliothèques nécessaires (récursivement), hors bibliothèques de Windows
for exe in "$STAGE"/*.exe; do
  ntldd -R "$exe" | tr '\\' '/' | grep -i "$PREFIX/bin/" | sed -E 's/.*=> *([^ ]+).*/\1/'
done | sort -u | while read -r dll; do
  dll="$(cygpath -u "$dll")"
  cp -n "$dll" "$STAGE/"
done

# Firmware UEFI (OVMF) et fichiers d'origine de QEMU utiles au PC virtuel
for f in edk2-x86_64-code.fd edk2-i386-vars.fd bios-256k.bin efi-virtio.rom efi-e1000e.rom \
         kvmvapic.bin linuxboot_dma.bin vgabios-virtio.bin vgabios-stdvga.bin; do
  [ -f "$PREFIX/share/qemu/$f" ] && cp "$PREFIX/share/qemu/$f" "$STAGE/share/"
done
cp -r "$PREFIX/share/qemu/keymaps" "$STAGE/share/" 2>/dev/null || true

# Licences (GPL : sources sur https://github.com/msys2/MINGW-packages/tree/master/mingw-w64-qemu)
mkdir -p "$STAGE/licences"
cp "$PREFIX/share/licenses/qemu/"* "$STAGE/licences/" 2>/dev/null || true
cat > "$STAGE/LISEZMOI.txt" <<EOF
Moteur de la machine virtuelle IXIN (installé par IXIN OS Setup)

Il s'agit de QEMU ${version} pour Windows, logiciel libre (GPL 2), tel que compilé par le
projet MSYS2, accompagné des bibliothèques dont il a besoin. Sources :
  https://www.qemu.org
  https://github.com/msys2/MINGW-packages/tree/master/mingw-w64-qemu
IXIN OS Setup : https://github.com/hashcat-max/IXIN-GROUP (dossier windows/)
EOF
echo "$version" > "$STAGE/VERSION"

# Vérifications : le moteur démarre et connaît l'accélération de Windows
"$STAGE/qemu-system-x86_64.exe" -version
"$STAGE/qemu-system-x86_64.exe" -accel help | tee "$STAGE/accel.txt"
grep -qx whpx "$STAGE/accel.txt" || { echo "WHPX absent de ce QEMU" >&2; exit 1; }
rm "$STAGE/accel.txt"

(cd "$(dirname "$STAGE")" && zip -qr9 "$(cygpath -u "$OUT")/ixin-vm-engine-win64.zip" ixin-vm-engine)
(cd "$OUT" && sha256sum ixin-vm-engine-win64.zip > ixin-vm-engine-win64.zip.sha256)
du -sh "$STAGE" "$OUT/ixin-vm-engine-win64.zip"
