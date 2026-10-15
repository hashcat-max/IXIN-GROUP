#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  Crée LA clé de signature des mises à jour IXIN OS (à faire une seule fois).
#
#    tools/create-signing-key.sh            (Linux, macOS, WSL ou IXIN OS)
#
#  Produit trois fichiers dans le dossier courant :
#    ixin-signing-key-PRIVEE.asc      → à coller dans le secret GitHub IXIN_SIGNING_KEY,
#                                       puis à ranger HORS LIGNE (clé USB chiffrée)
#    ixin-signing-key-publique.asc    → peut être publiée (empreinte à communiquer)
#    ixin-signing-key-revocation.asc  → à garder hors ligne : sert à révoquer la clé
#                                       si elle était un jour compromise
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail
command -v gpg >/dev/null || { echo "gpg (GnuPG) est nécessaire"; exit 1; }
NAME="${1:-ixin-signing-key}"
[[ -e "$NAME-PRIVEE.asc" ]] && { echo "$NAME-PRIVEE.asc existe déjà : rien n'est écrasé"; exit 1; }

export GNUPGHOME="$(mktemp -d)"
trap 'rm -rf "$GNUPGHOME"' EXIT
umask 077

# Pas d'expiration : une clé expirée bloquerait les mises à jour de tous les PC.
# En cas de compromission, on la révoque (certificat ci-dessous) et on la remplace.
gpg --batch --quiet --passphrase '' --quick-gen-key "IXIN OS Updates (équipe IXIN)" ed25519 sign never
FPR="$(gpg --list-keys --with-colons | awk -F: '/^fpr/ {print $10; exit}')"

gpg --armor --export-secret-keys "$FPR" > "$NAME-PRIVEE.asc"
gpg --armor --export "$FPR" > "$NAME-publique.asc"
cp "$GNUPGHOME/openpgp-revocs.d/$FPR.rev" "$NAME-revocation.asc"
chmod 600 "$NAME-PRIVEE.asc" "$NAME-revocation.asc"

cat <<EOF

Clé créée. Empreinte : $FPR

1. GitHub → dépôt IXIN-GROUP → Settings → Secrets and variables → Actions
   → New repository secret
      Name   : IXIN_SIGNING_KEY
      Secret : tout le contenu du fichier $NAME-PRIVEE.asc
               (de « -----BEGIN PGP PRIVATE KEY BLOCK----- » à « -----END … ----- »)
2. Copiez $NAME-PRIVEE.asc et $NAME-revocation.asc sur un support hors ligne
   (clé USB chiffrée, coffre KeePassXC…), puis supprimez-les de cet ordinateur.
3. Lancez l'action « Publier les mises à jour IXIN », puis « Construire l'ISO IXIN OS ».

Ne partagez JAMAIS la clé privée : quiconque la possède peut installer des logiciels
sur tous les PC IXIN OS.
EOF
