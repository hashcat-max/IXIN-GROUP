#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS dans une fenêtre, sous Linux (machine virtuelle IXIN)
#
#    bash ixin-os-vm.sh                 prépare tout la première fois, puis lance IXIN OS
#    bash ixin-os-vm.sh --mettre-a-jour télécharge la dernière ISO d'IXIN OS (IXIN OS installé : non touché)
#    bash ixin-os-vm.sh --supprimer     supprime la machine virtuelle et ses raccourcis
#    bash ixin-os-vm.sh --infos         ce qui est installé, où, et avec quels réglages
#    bash ixin-os-vm.sh --derniere-version   dernière version publiée d'IXIN OS (sans rien télécharger)
#
#  Tout est automatique : installe QEMU avec le gestionnaire de paquets de la distribution (apt, dnf,
#  pacman, zypper), télécharge IXIN OS et vérifie son empreinte, crée la machine virtuelle (moitié de la
#  mémoire, jusqu'à 8 Go ; disque de 120 Go qui grandit à l'usage) et ajoute « IXIN OS » au menu des
#  applications. Rien n'est modifié sur le disque du PC en dehors de ~/.local/share/ixin-os-vm.
#  Équivalent Linux de « IXIN OS dans une fenêtre » d'IXIN OS Setup (Windows).
#
#  Pour les tests : IXIN_VM_ISO=<iso locale> (pas de téléchargement), IXIN_VM_DIR=<dossier>,
#  IXIN_VM_HEADLESS=1 (sans fenêtre), IXIN_VM_EXTRA="<options QEMU en plus>".
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

REPO=hashcat-max/IXIN-GROUP
API="https://api.github.com/repos/$REPO/releases/latest"
MIRROR="https://hashcat-max.github.io/IXIN-GROUP/site/release.json"
SELF_URL="https://hashcat-max.github.io/IXIN-GROUP/site/ixin-os-vm.sh"
DIR="${IXIN_VM_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/ixin-os-vm}"
DISK="$DIR/ixin-os.qcow2"
VARS="$DIR/uefi-vars.fd"
ISO="$DIR/ixin-os.iso"
CONF="$DIR/reglages"
APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="ixin-os-vm.desktop"
DISK_SIZE=120G

# ── messages (terminal, ou fenêtre si lancé depuis le menu) ──────────────────
gui() { [[ ! -t 1 ]] && { command -v zenity >/dev/null || command -v kdialog >/dev/null; }; }
say() { printf '\e[38;2;25;243;164m==>\e[0m \e[1m%s\e[0m\n' "$*"; }
warn() { printf '\e[33m ! \e[0m%s\n' "$*" >&2; }
fail() {
    printf '\e[31mErreur :\e[0m %s\n' "$*" >&2
    if gui; then
        if command -v zenity >/dev/null; then zenity --error --title "IXIN OS" --text "$*" 2>/dev/null || true
        else kdialog --title "IXIN OS" --error "$*" 2>/dev/null || true; fi
    fi
    exit 1
}

[[ "$(uname -m)" == x86_64 ]] || fail "IXIN OS fonctionne sur les PC à processeur Intel ou AMD 64 bits (ce PC : $(uname -m))."
[[ $EUID -ne 0 ]] || warn "Lancé en administrateur (root) : la machine virtuelle sera rangée dans le dossier de root. Lancez plutôt ce script sans sudo."

# ── 1. QEMU et le firmware UEFI ────────────────────────────────────────────────
as_root() {
    if [[ $EUID -eq 0 ]]; then "$@"
    elif [[ -t 0 ]] && command -v sudo >/dev/null; then sudo "$@"
    elif command -v pkexec >/dev/null; then pkexec "$@"
    else fail "Impossible d'obtenir les droits d'administrateur pour installer QEMU. Installez-le vous-même, puis relancez."; fi
}

# Paires firmware UEFI (code + variables) des distributions, sans Secure Boot
FIRMWARES=(
    "/usr/share/edk2/x64/OVMF_CODE.4m.fd /usr/share/edk2/x64/OVMF_VARS.4m.fd"
    "/usr/share/OVMF/OVMF_CODE_4M.fd /usr/share/OVMF/OVMF_VARS_4M.fd"
    "/usr/share/edk2/ovmf/OVMF_CODE.fd /usr/share/edk2/ovmf/OVMF_VARS.fd"
    "/usr/share/OVMF/OVMF_CODE.fd /usr/share/OVMF/OVMF_VARS.fd"
    "/usr/share/qemu/ovmf-x86_64-code.bin /usr/share/qemu/ovmf-x86_64-vars.bin"
    "/usr/share/edk2-ovmf/x64/OVMF_CODE.fd /usr/share/edk2-ovmf/x64/OVMF_VARS.fd"
    "/usr/share/qemu/edk2-x86_64-code.fd /usr/share/qemu/edk2-i386-vars.fd"
)
find_firmware() {
    local pair code vars
    for pair in "${FIRMWARES[@]}"; do
        read -r code vars <<<"$pair"
        [[ -f "$code" && -f "$vars" ]] && { echo "$code $vars"; return 0; }
    done
    return 1
}

install_qemu() {
    local pm
    for pm in apt-get dnf pacman zypper; do command -v "$pm" >/dev/null && break; pm=""; done
    say "Installation de QEMU (logiciel de machine virtuelle) avec ${pm:-?}…"
    case "$pm" in
        apt-get) as_root apt-get update &&
                 as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y qemu-system-x86 qemu-utils ovmf ;;
        dnf)     as_root dnf install -y qemu-kvm qemu-img edk2-ovmf ;;
        pacman)  as_root pacman -S --needed --noconfirm qemu-desktop edk2-ovmf ||
                 fail "Installation de QEMU impossible : mettez d'abord le système à jour (sudo pacman -Syu), puis relancez." ;;
        zypper)  as_root zypper --non-interactive install qemu-x86 qemu-tools qemu-ovmf-x86_64 ;;
        *) fail "Distribution non reconnue (ni apt, ni dnf, ni pacman, ni zypper) : installez QEMU et le firmware OVMF, puis relancez. Sur SteamOS, Fedora Silverblue et autres systèmes en lecture seule, utilisez plutôt GNOME Boxes (Flatpak) avec l'ISO d'IXIN OS." ;;
    esac
}

ensure_qemu() {
    if ! command -v qemu-system-x86_64 >/dev/null || ! command -v qemu-img >/dev/null || ! find_firmware >/dev/null; then
        install_qemu || warn "L'installation de QEMU a signalé une erreur."
    fi
    command -v qemu-system-x86_64 >/dev/null || fail "QEMU n'a pas pu être installé."
    command -v qemu-img >/dev/null || fail "qemu-img n'a pas pu être installé."
    find_firmware >/dev/null || fail "Firmware UEFI (OVMF) introuvable après l'installation."
}

# Accélération matérielle (KVM) : sans elle, IXIN OS fonctionne mais très lentement
check_kvm() {
    if [[ -r /dev/kvm && -w /dev/kvm ]]; then return 0; fi
    if [[ -e /dev/kvm ]]; then
        if getent group kvm >/dev/null && ! id -nG | tr ' ' '\n' | grep -qx kvm; then
            say "Autorisation de la virtualisation pour $(id -un) (groupe kvm)…"
            as_root usermod -aG kvm "$(id -un)" || true
            warn "Fermez la session puis reconnectez-vous pour qu'IXIN OS soit rapide (cette fois, il sera lent)."
        else
            warn "Virtualisation matérielle non autorisée (/dev/kvm) : IXIN OS sera lent."
        fi
    else
        warn "Virtualisation matérielle indisponible : IXIN OS sera très lent."
        warn "Activez « Intel VT-x » / « AMD-V » / « SVM » dans les paramètres du PC (BIOS / UEFI)."
    fi
    return 1
}

# ── 2. IXIN OS (ISO) ────────────────────────────────────────────────────────────
release_json() {
    local j
    j="$(curl -fsSL --retry 3 --max-time 60 -H 'Accept: application/vnd.github+json' "$API" 2>/dev/null)" &&
        grep -q '"browser_download_url"' <<<"$j" && { echo "$j"; return 0; }
    # API de GitHub indisponible ou limitée : copie publiée avec le site
    j="$(curl -fsSL --retry 3 --max-time 60 "$MIRROR")" || return 1
    echo "$j"
}

# Dernière version : tag, adresses des morceaux de l'ISO et de son empreinte (variables tag, urls, sha_url)
latest_release() {
    command -v curl >/dev/null || fail "curl est nécessaire pour télécharger IXIN OS."
    local json
    json="$(release_json)" || fail "Impossible de joindre GitHub. Vérifiez la connexion Internet, puis relancez."
    tag="$(grep -o '"tag_name": *"[^"]*"' <<<"$json" | head -n1 | sed 's/.*"\([^"]*\)"$/\1/')"
    mapfile -t urls < <(grep -o '"browser_download_url": *"[^"]*\.iso\.part[0-9]*"' <<<"$json" | sed 's/.*"\(http[^"]*\)"$/\1/' | sort)
    sha_url="$(grep -o '"browser_download_url": *"[^"]*\.iso\.sha256"' <<<"$json" | head -n1 | sed 's/.*"\(http[^"]*\)"$/\1/')"
    ((${#urls[@]})) && [[ -n "$sha_url" ]] || fail "Version $tag : fichiers d'IXIN OS introuvables."
}

# Un morceau, avec reprise ; connexion coupée : nouvel essai ; reprise refusée par le serveur : depuis le début
download_part() {
    local url="$1" out="$2" try rc
    for try in 1 2 3 4 5 6 7 8 9 10; do
        rc=0
        curl -fL --retry 10 --retry-delay 5 --retry-connrefused --connect-timeout 30 --speed-limit 1024 --speed-time 60 \
            -C - --progress-bar -o "$out" "$url" || rc=$?
        (( rc == 0 )) && return 0
        if (( rc == 33 || rc == 36 )); then
            rm -f "$out"; warn "Reprise impossible : cette partie recommence depuis le début."
        else
            warn "Connexion interrompue (code $rc), nouvel essai ($try/10)…"
        fi
        sleep 5
    done
    return 1
}

download_iso() {
    say "Recherche de la dernière version d'IXIN OS…"
    local tag urls sha_url url name dl="$DIR/telechargement"
    latest_release

    local _f _s _u avail
    read -r _f _s _u avail _ < <(df -Pk "$DIR" | tail -n 1)
    avail=$(( avail / 1024 / 1024 ))
    (( avail >= 12 )) || fail "Espace libre insuffisant dans $DIR : ${avail} Go (12 Go nécessaires, 30 Go conseillés)."

    mkdir -p "$dl"
    # Les morceaux sont ajoutés un à un à l'ISO puis effacés (pas besoin de deux fois la place) ;
    # un téléchargement interrompu reprend où il s'était arrêté
    [[ "$(cat "$dl/version" 2>/dev/null)" == "$tag" ]] || { rm -rf "$dl"; mkdir -p "$dl"; echo "$tag" > "$dl/version"; }
    local i=0 n=${#urls[@]}
    for url in "${urls[@]}"; do
        i=$((i + 1)); name="${url##*/}"
        grep -qx "$name" "$dl/assembles" 2>/dev/null && continue
        say "Téléchargement d'IXIN OS $tag : partie $i sur $n"
        download_part "$url" "$dl/$name" ||
            fail "Téléchargement interrompu. Relancez : il reprendra où il s'est arrêté."
        cat "$dl/$name" >> "$dl/ixin-os.iso"
        echo "$name" >> "$dl/assembles"
        rm -f "$dl/$name"
    done
    say "Vérification de l'empreinte (SHA-256)…"
    local want got
    want="$(curl -fsSL --retry 5 "$sha_url")"; want="${want%%[[:space:]]*}"
    got="$(sha256sum "$dl/ixin-os.iso")"; got="${got%%[[:space:]]*}"
    if [[ -z "$want" || "$want" != "$got" ]]; then
        rm -rf "$dl"
        fail "Le fichier téléchargé est abîmé (empreinte différente). Relancez pour le télécharger à nouveau."
    fi
    mv -f "$dl/ixin-os.iso" "$ISO"
    echo "$tag" > "$DIR/version"
    rm -rf "$dl"
    say "IXIN OS $tag téléchargé et vérifié."
}

# ── 3. machine virtuelle ────────────────────────────────────────────────────────
memory_mb() {   # moitié de la mémoire, entre 2 et 8 Go
    local _k kb
    read -r _k kb _ < <(grep '^MemTotal:' /proc/meminfo)
    local total=$(( kb / 1024 ))
    local m=$(( total / 2 ))
    (( m < 2048 )) && m=2048
    (( m > 8192 )) && m=8192
    echo "$m"
}
processors() {  # moitié des cœurs, entre 2 et 8
    local c=$(( $(nproc) / 2 ))
    (( c < 2 )) && c=2
    (( c > 8 )) && c=8
    echo "$c"
}

create_vm() {
    local code vars
    read -r code vars < <(find_firmware)
    say "Création de la machine virtuelle IXIN…"
    [[ -f "$DISK" ]] || qemu-img create -q -f qcow2 "$DISK" "$DISK_SIZE"
    [[ -f "$VARS" ]] || { cp "$vars" "$VARS"; chmod 644 "$VARS"; }
    # Le firmware choisi est mémorisé : ses variables (fichier ci-dessus) ne vont qu'avec lui
    printf 'FIRMWARE=%q\n' "$code" > "$CONF"
}

install_self() {
    # Copie de ce script dans le dossier de la machine virtuelle (le raccourci la lance)
    local src="${BASH_SOURCE[0]:-}"
    if [[ -f "$src" && "$(realpath "$src")" != "$(realpath -m "$DIR/ixin-os-vm.sh")" ]]; then
        cp "$src" "$DIR/ixin-os-vm.sh"
    elif [[ ! -f "$DIR/ixin-os-vm.sh" ]]; then
        curl -fsSL --retry 3 -o "$DIR/ixin-os-vm.sh" "$SELF_URL" || fail "Impossible d'enregistrer le lanceur d'IXIN OS."
    fi
    chmod 755 "$DIR/ixin-os-vm.sh"
    cat > "$DIR/ixin.svg" <<'SVG'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#19f3a4"/>
      <stop offset="0.55" stop-color="#1c6bff"/>
      <stop offset="1" stop-color="#a855f7"/>
    </linearGradient>
  </defs>
  <!-- bouclier hexagonal -->
  <path d="M128 10 L230 62 L230 150 C230 200 186 232 128 248 C70 232 26 200 26 150 L26 62 Z"
        fill="#050505" stroke="url(#g)" stroke-width="14" stroke-linejoin="round"/>
  <!-- X stylisé -->
  <path d="M82 78 L128 128 L174 78 M82 182 L128 132 L174 182"
        fill="none" stroke="url(#g)" stroke-width="22" stroke-linecap="round" stroke-linejoin="round"/>
  <circle cx="128" cy="130" r="9" fill="#efe6d2"/>
</svg>
SVG
}

create_shortcuts() {
    mkdir -p "$APPS"
    cat > "$APPS/$DESKTOP_FILE" <<EOF
[Desktop Entry]
Type=Application
Name=IXIN OS
GenericName=IXIN OS dans une fenêtre
Comment=IXIN OS dans une fenêtre (machine virtuelle IXIN)
Exec=bash "$DIR/ixin-os-vm.sh"
Icon=$DIR/ixin.svg
Terminal=false
Categories=System;Emulator;
StartupNotify=true
EOF
    command -v update-desktop-database >/dev/null && update-desktop-database -q "$APPS" 2>/dev/null || true
    # Raccourci sur le bureau, s'il existe
    local desk
    desk="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
    if [[ -n "$desk" && -d "$desk" && "$desk" != "$HOME" ]]; then
        cp "$APPS/$DESKTOP_FILE" "$desk/$DESKTOP_FILE"
        chmod 755 "$desk/$DESKTOP_FILE"
        command -v gio >/dev/null && gio set "$desk/$DESKTOP_FILE" metadata::trusted true 2>/dev/null || true
    fi
    say "« IXIN OS » ajouté au menu des applications$([[ -n "$desk" && -d "$desk" && "$desk" != "$HOME" ]] && echo " et sur le bureau")."
}

audio_args() {
    local list d
    list="$(qemu-system-x86_64 -audiodev help 2>/dev/null || true)"
    for d in pipewire pa alsa; do
        grep -qx "$d" <<<"$list" && { echo "-audiodev $d,id=son -device intel-hda -device hda-duplex,audiodev=son"; return; }
    done
}
display_args() {
    local list
    list="$(qemu-system-x86_64 -display help 2>/dev/null || true)"
    if grep -qx gtk <<<"$list"; then echo "-display gtk,zoom-to-fit=on,show-menubar=off"
    elif grep -qx sdl <<<"$list"; then echo "-display sdl,window-close=on"
    fi
}

launch() {
    [[ -f "$CONF" ]] || fail "Machine virtuelle incomplète : relancez « bash ixin-os-vm.sh » dans un terminal."
    # shellcheck disable=SC1090
    source "$CONF"
    [[ -f "$FIRMWARE" ]] || fail "Firmware UEFI introuvable ($FIRMWARE) : relancez « bash ixin-os-vm.sh » dans un terminal."
    local args=(
        -name "IXIN OS" -machine q35
        # Virtualisation matérielle (KVM) si possible, sinon émulation
        -accel kvm -accel tcg -cpu max
        -m "$(memory_mb)" -smp "$(processors)"
        -drive "if=pflash,format=raw,readonly=on,file=$FIRMWARE"
        -drive "if=pflash,format=raw,file=$VARS"
        # Disque d'IXIN OS d'abord ; l'ISO ne sert que tant qu'IXIN OS n'est pas installé
        -drive "file=$DISK,if=none,id=hd0,format=qcow2,cache=writeback,discard=unmap"
        -device virtio-blk-pci,drive=hd0,bootindex=1
        -nic user,model=virtio-net-pci -device virtio-rng-pci
        -device qemu-xhci -device usb-tablet
    )
    [[ -f "$ISO" ]] && args+=(-drive "file=$ISO,if=none,id=cd0,media=cdrom,readonly=on" -device ide-cd,drive=cd0,bootindex=2)
    if [[ "${IXIN_VM_HEADLESS:-0}" == 1 ]]; then
        args+=(-display none -vga none)
    else
        # shellcheck disable=SC2207
        args+=(-device virtio-vga $(display_args) $(audio_args))
    fi
    # shellcheck disable=SC2206
    [[ -n "${IXIN_VM_EXTRA:-}" ]] && args+=($IXIN_VM_EXTRA)
    say "Démarrage d'IXIN OS ($(memory_mb) Mo de mémoire, $(processors) processeurs)…"
    exec qemu-system-x86_64 "${args[@]}"
}

setup() {
    mkdir -p "$DIR"
    ensure_qemu
    check_kvm || true
    if [[ -n "${IXIN_VM_ISO:-}" ]]; then
        [[ -f "$IXIN_VM_ISO" ]] || fail "ISO introuvable : $IXIN_VM_ISO"
        ln -sf "$(realpath "$IXIN_VM_ISO")" "$ISO"
    elif [[ ! -f "$ISO" && ! -f "$DIR/version" ]]; then
        download_iso
    fi
    create_vm
    install_self
    create_shortcuts
    cat <<EOF

IXIN OS est prêt. La fenêtre va s'ouvrir sur le menu d'IXIN OS : essayez-le, puis cliquez sur
« Installer IXIN OS » sur le bureau pour l'installer dans la machine virtuelle (« Effacer le disque » :
c'est le disque virtuel, pas celui du PC). Ensuite, ouvrez « IXIN OS » depuis le menu des applications.
Clavier et souris capturés par la fenêtre : Ctrl + Alt + G pour les libérer.

EOF
}

case "${1:-}" in
    "")
        if [[ -f "$CONF" && -f "$DISK" && -f "$VARS" ]]; then launch; fi
        setup; launch ;;
    --preparer)
        setup ;;
    --mettre-a-jour)
        mkdir -p "$DIR"; rm -f "$DIR/version"; download_iso ;;
    --supprimer)
        if [[ "${2:-}" != --oui && -t 0 ]]; then
            read -r -p "Supprimer la machine virtuelle IXIN OS et tout ce qu'elle contient ? [o/N] " r
            [[ "$r" == [oOyY]* ]] || exit 0
        fi
        rm -rf "$DIR"
        rm -f "$APPS/$DESKTOP_FILE"
        desk="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
        [[ -n "$desk" ]] && rm -f "$desk/$DESKTOP_FILE"
        say "Machine virtuelle IXIN OS supprimée." ;;
    --derniere-version)
        latest_release
        echo "$tag : ${#urls[@]} morceaux d'ISO"; printf '  %s\n' "${urls[@]}" "$sha_url" ;;
    --infos)
        echo "Dossier      : $DIR"
        echo "Version ISO  : $(cat "$DIR/version" 2>/dev/null || echo '—')"
        echo "Disque       : $( [[ -f "$DISK" ]] && du -h "$DISK" | cut -f1 || echo '—') utilisés (sur $DISK_SIZE)"
        echo "Mémoire      : $(memory_mb) Mo, $(processors) processeurs"
        echo "Firmware     : $(find_firmware 2>/dev/null | cut -d' ' -f1 || echo 'introuvable')"
        echo "QEMU         : $(qemu-system-x86_64 --version 2>/dev/null | head -n1 || echo 'non installé')"
        echo "Accélération : $( [[ -r /dev/kvm && -w /dev/kvm ]] && echo 'KVM' || echo 'aucune (lent)')" ;;
    -h|--help|--aide)
        sed -n '2,18p' "$0" | sed 's/^#  \{0,1\}//' ;;
    *)
        fail "Option inconnue : $1 (voir --aide)" ;;
esac
