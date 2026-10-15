#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  IXIN OS — construit les paquets IXIN (packages/*) et le dépôt signé [ixin]
#
#  Usage (root, sur Arch Linux) :  tools/build-packages.sh <dossier de sortie>
#
#  Variables d'environnement :
#    IXIN_SIGNING_KEY            clé privée OpenPGP (texte ASCII) — secret GitHub
#    IXIN_SIGNING_KEY_FILE       … ou chemin vers ce fichier
#    IXIN_SIGNING_KEY_PASSPHRASE phrase de passe de la clé (facultatif)
#    IXIN_ALLOW_UNSIGNED=1       construction non signée (tests uniquement)
#    IXIN_REUSE_DIR              paquets déjà publiés : une version inchangée est
#                                réutilisée telle quelle (fichier identique)
#    IXIN_POLICY                 politique à publier (défaut : repo/policy.json)
#    IXIN_POLICY_DAYS            durée de validité de la politique (défaut : 14 j)
#
#  Résultat : <sortie>/ixin.db(.sig), *.pkg.tar.zst(.sig), policy.json(.sig)
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/build/ixin-repo}")"
REPO_NAME=ixin
BUILD_USER=${IXIN_BUILD_USER:-ixinbuild}
POLICY_SRC=${IXIN_POLICY:-$ROOT/repo/policy.json}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
chmod 755 "$WORK"

msg(){ printf '\e[38;2;25;243;164m==>\e[0m \e[1m%s\e[0m\n' "$*"; }
die(){ printf '\e[31m==> ERREUR :\e[0m %s\n' "$*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || die "à lancer en root (makepkg est exécuté sous l'utilisateur « $BUILD_USER »)"
command -v makepkg >/dev/null || die "makepkg introuvable : ce script doit tourner sur Arch Linux"

# ── 1. Clé de signature ──────────────────────────────────────────────────────
export GNUPGHOME="$WORK/gnupg"
install -dm700 "$GNUPGHOME"
if [[ -n "${IXIN_SIGNING_KEY:-}" ]]; then
  printf '%s\n' "$IXIN_SIGNING_KEY" | gpg --batch --quiet --import 2>/dev/null || die "clé IXIN_SIGNING_KEY illisible"
elif [[ -n "${IXIN_SIGNING_KEY_FILE:-}" ]]; then
  gpg --batch --quiet --import "$IXIN_SIGNING_KEY_FILE" 2>/dev/null || die "clé $IXIN_SIGNING_KEY_FILE illisible"
fi
FPR="$(gpg --list-secret-keys --with-colons 2>/dev/null | awk -F: '/^fpr/ {print $10; exit}')"
SIGN_OPTS=(--batch --yes --detach-sign --no-armor)
if [[ -n "${IXIN_SIGNING_KEY_PASSPHRASE:-}" ]]; then
  SIGN_OPTS+=(--pinentry-mode loopback --passphrase "$IXIN_SIGNING_KEY_PASSPHRASE")
  printf 'allow-loopback-pinentry\n' > "$GNUPGHOME/gpg-agent.conf"
fi
if [[ -n "$FPR" ]]; then
  msg "Clé de signature : $FPR"
else
  [[ "${IXIN_ALLOW_UNSIGNED:-0}" == 1 ]] || die "aucune clé de signature (secret IXIN_SIGNING_KEY) — voir docs/MISES-A-JOUR.md"
  msg "ATTENTION : paquets NON signés (tests uniquement)"
fi
sign(){ [[ -n "$FPR" ]] && gpg "${SIGN_OPTS[@]}" --local-user "$FPR" --output "$1.sig" "$1"; return 0; }

# ── 2. Copie de travail : versions automatiques + trousseau ─────────────────
cp -a "$ROOT/packages" "$WORK/packages"
git config --global --add safe.directory "$ROOT" 2>/dev/null || true
for dir in "$WORK"/packages/*/; do
  name="$(basename "$dir")"
  # Version = date (UTC) du dernier commit qui a modifié ce paquet : elle n'augmente
  # que si le paquet change, et les PC IXIN OS ne téléchargent alors que ce paquet.
  ver="$(TZ=UTC git -C "$ROOT" log -1 --format=%cd --date=format-local:%Y.%m.%d.%H%M -- "packages/$name" 2>/dev/null || true)"
  if [[ -z "$ver" || -n "$(git -C "$ROOT" status --porcelain -- "packages/$name" 2>/dev/null)" ]]; then
    ver="$(date -u +%Y.%m.%d.%H%M)"          # modifications locales non commitées
  fi
  sed -i "s/^pkgver=.*/pkgver=$ver/" "$dir/PKGBUILD"
  printf '   %-16s %s\n' "$name" "$ver"
done
K="$WORK/packages/ixin-keyring"
[[ -f "$K/ixin-revoked" ]] || : > "$K/ixin-revoked"
# Clés publiques supplémentaires (changement de clé : ancienne + nouvelle pendant la transition)
[[ -f "$K/extra-keys.asc" ]] && gpg --batch --quiet --import "$K/extra-keys.asc" 2>/dev/null
mapfile -t TRUSTED_FPRS < <(gpg --list-keys --with-colons 2>/dev/null | awk -F: '/^pub/ {getline; if ($1 == "fpr") print $10}' |
                            grep -vxFf <(cut -d: -f1 "$K/ixin-revoked" | grep . || echo NONE))
if (( ${#TRUSTED_FPRS[@]} )); then
  gpg --export "${TRUSTED_FPRS[@]}" > "$K/ixin.gpg"
  printf '%s:4:\n' "${TRUSTED_FPRS[@]}" > "$K/ixin-trusted"
else
  : > "$K/ixin.gpg"; : > "$K/ixin-trusted"
fi
rm -f "$K/extra-keys.asc"

# ── 3. Construction ──────────────────────────────────────────────────────────
pacman -S --needed --noconfirm --color never grub ttf-dejavu plymouth >/dev/null 2>&1
id "$BUILD_USER" &>/dev/null || useradd -m "$BUILD_USER"
install -dm755 -o "$BUILD_USER" "$WORK/pkgout" "$WORK/src"
chown -R "$BUILD_USER:" "$WORK/packages"
for dir in "$WORK"/packages/*/; do
  msg "Construction de $(basename "$dir")"
  (cd "$dir" && sudo -u "$BUILD_USER" env PKGDEST="$WORK/pkgout" SRCDEST="$WORK/src" \
     makepkg --force --nodeps --noconfirm --cleanbuild >/dev/null) || die "échec de construction : $(basename "$dir")"
done

# ── 4. Dépôt signé ───────────────────────────────────────────────────────────
mkdir -p "$OUT"
rm -f "$OUT"/*.pkg.tar.zst "$OUT"/*.pkg.tar.zst.sig "$OUT/$REPO_NAME".{db,files}* "$OUT"/policy.json*
for pkg in "$WORK"/pkgout/*.pkg.tar.zst; do
  base="$(basename "$pkg")"
  if [[ -n "${IXIN_REUSE_DIR:-}" && -f "$IXIN_REUSE_DIR/$base" ]]; then
    cp "$IXIN_REUSE_DIR/$base" "$OUT/"                  # version déjà publiée : fichier identique
    [[ -f "$IXIN_REUSE_DIR/$base.sig" ]] && cp "$IXIN_REUSE_DIR/$base.sig" "$OUT/"
    printf '   %s (inchangé)\n' "$base"
  else
    cp "$pkg" "$OUT/"
    printf '   %s (nouveau)\n' "$base"
  fi
  [[ -f "$OUT/$base.sig" ]] || sign "$OUT/$base"
done
if [[ -n "$FPR" ]]; then
  repo-add --quiet --sign --key "$FPR" "$OUT/$REPO_NAME.db.tar.gz" "$OUT"/*.pkg.tar.zst
else
  repo-add --quiet "$OUT/$REPO_NAME.db.tar.gz" "$OUT"/*.pkg.tar.zst
fi
# GitHub Releases ne gère pas les liens symboliques : vrais fichiers ixin.db / ixin.files
for f in db files; do
  rm -f "$OUT/$REPO_NAME.$f" "$OUT/$REPO_NAME.$f.sig"
  cp "$OUT/$REPO_NAME.$f.tar.gz" "$OUT/$REPO_NAME.$f"
  [[ -f "$OUT/$REPO_NAME.$f.tar.gz.sig" ]] && cp "$OUT/$REPO_NAME.$f.tar.gz.sig" "$OUT/$REPO_NAME.$f.sig"
done

# ── 5. Politique IXIN signée (mise à jour urgente, blocages, annonces) ───────
if [[ -f "$POLICY_SRC" ]]; then
  python3 - "$POLICY_SRC" "$OUT/policy.json" "${IXIN_POLICY_DAYS:-14}" <<'PY'
import datetime as dt, json, sys, time
src, dest, days = sys.argv[1], sys.argv[2], float(sys.argv[3])
policy = {k: v for k, v in json.load(open(src, encoding="utf-8")).items() if not k.startswith("_")}
now = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
policy.update(version=1, serial=int(time.time()), issued=now.isoformat().replace("+00:00", "Z"),
              expires=(now + dt.timedelta(days=days)).isoformat().replace("+00:00", "Z"))
policy.setdefault("urgent", False); policy.setdefault("hold", []); policy.setdefault("news_ack", [])
policy.setdefault("notice", None)
json.dump(policy, open(dest, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print(f"   politique : serial {policy['serial']}, urgent={policy['urgent']}, hold={policy['hold']}")
PY
  sign "$OUT/policy.json"
fi

msg "Dépôt [$REPO_NAME] prêt dans $OUT"
ls -1 "$OUT"
