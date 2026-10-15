#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  Prépare le site du dépôt [ixin] (GitHub Pages et branche ixin-repo)
#
#    tools/prepare-repo-site.sh <dépôt construit> <dépôt précédent> <site>
#
#  <site>/repo/ reçoit le dépôt construit par build-packages.sh (base, paquets, politique,
#  signatures) ainsi que les paquets de la publication précédente encore cités par
#  l'ancienne base : un PC peut garder l'ancienne base en cache quelques minutes après une
#  publication, ses téléchargements doivent continuer à fonctionner.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

NEW="${1:?dépôt construit}" PREV="${2:?dépôt précédent}" SITE="${3:?dossier du site}"
REPOSITORY="${GITHUB_REPOSITORY:-hashcat-max/IXIN-GROUP}"

[[ -f "$NEW/ixin.db" && -f "$NEW/ixin.db.sig" && -f "$NEW/policy.json.sig" ]] ||
  { echo "dépôt incomplet : $NEW" >&2; exit 1; }
rm -rf "$SITE"
mkdir -p "$SITE/repo"
cp -a "$NEW/." "$SITE/repo/"

kept=0
if [[ -f "$PREV/ixin.db" ]]; then
  # tar reconnaît seul la compression de la base (gzip, zstd…)
  while read -r f; do
    [[ "$f" =~ ^[A-Za-z0-9@._+-]+\.pkg\.tar\.zst$ ]] || continue
    [[ -e "$SITE/repo/$f" ]] && continue
    [[ -f "$PREV/$f" && -f "$PREV/$f.sig" ]] || continue
    cp "$PREV/$f" "$PREV/$f.sig" "$SITE/repo/"
    kept=$((kept + 1))
  done < <(tar -xOf "$PREV/ixin.db" --wildcards '*/desc' 2>/dev/null | sed -n '/^%FILENAME%$/{n;p}')
fi
echo "paquets de la publication précédente conservés : $kept"

touch "$SITE/.nojekyll"   # GitHub Pages servi depuis la branche : fichiers tels quels
cat > "$SITE/README.md" <<EOF
# Dépôt de mises à jour IXIN OS — branche générée automatiquement

Ne pas modifier : cette branche est remplacée à chaque publication par l'action
« Publier les mises à jour IXIN ». Les sources sont dans la branche principale
(\`packages/\`, \`repo/policy.json\`).
EOF
cat > "$SITE/index.html" <<EOF
<!doctype html>
<html lang="fr">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Dépôt IXIN OS</title>
<style>
  body { font-family: system-ui, sans-serif; background: #0b0c10; color: #e9ecef;
         max-width: 640px; margin: 10vh auto; padding: 0 16px; line-height: 1.6; }
  a { color: #19f3a4; }
</style>
<h1>Dépôt de mises à jour IXIN OS</h1>
<p>Ce site sert les mises à jour signées d'IXIN OS (dépôt <code>[ixin]</code>), installées
automatiquement par IXIN Update. Il n'y a rien à télécharger ici à la main.</p>
<p>Pour installer IXIN OS : <a href="https://github.com/${REPOSITORY}/releases/latest">dernière version</a>.</p>
</html>
EOF
ls -la "$SITE/repo"
