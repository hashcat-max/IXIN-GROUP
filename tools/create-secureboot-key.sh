#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
#  Crée LA clé Secure Boot d'IXIN OS (à faire une seule fois).
#
#    tools/create-secureboot-key.sh    (Linux, macOS, IXIN OS, ou « Git Bash » sous Windows)
#
#  IXIN OS démarre avec Secure Boot activé grâce à « shim » (signé par Microsoft) : shim fait
#  confiance aux programmes signés par cette clé une fois son certificat enregistré sur le PC
#  (écran « MokManager », une seule fois). Elle signe le chargeur de démarrage et le noyau.
#
#  Produit deux fichiers dans le dossier courant :
#    ixin-secureboot-PRIVEE.key   → à coller dans le secret GitHub IXIN_SECUREBOOT_KEY,
#                                   puis à ranger HORS LIGNE (clé USB chiffrée)
#    ixin-secureboot.crt          → certificat public : à copier dans le dépôt,
#                                   repo/secureboot/ixin-secureboot.crt
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail
command -v openssl >/dev/null || { echo "openssl est nécessaire"; exit 1; }
[[ -e ixin-secureboot-PRIVEE.key ]] && { echo "ixin-secureboot-PRIVEE.key existe déjà : rien n'est écrasé"; exit 1; }
umask 077

# 20 ans : un certificat expiré n'empêche pas shim de vérifier une signature, mais on évite la question.
# RSA 2048 : seule taille acceptée par tous les firmwares et par shim.
openssl req -new -x509 -newkey rsa:2048 -sha256 -nodes -days 7300 \
  -subj "/CN=IXIN OS Secure Boot (équipe IXIN)/O=IXIN Group/" \
  -addext "extendedKeyUsage=codeSigning" \
  -keyout ixin-secureboot-PRIVEE.key -out ixin-secureboot.crt 2>/dev/null
chmod 644 ixin-secureboot.crt
FPR="$(openssl x509 -in ixin-secureboot.crt -noout -fingerprint -sha256 | cut -d= -f2)"

cat <<EOF

Clé Secure Boot créée. Empreinte SHA-256 du certificat :
  $FPR

1. GitHub → dépôt IXIN-GROUP → Settings → Secrets and variables → Actions → New repository secret
     Name   : IXIN_SECUREBOOT_KEY
     Secret : tout le contenu de ixin-secureboot-PRIVEE.key
2. Copiez ixin-secureboot.crt dans le dépôt : repo/secureboot/ixin-secureboot.crt (commit + push).
3. Rangez ixin-secureboot-PRIVEE.key hors ligne, puis supprimez-le de cet ordinateur.

Tant que le secret et le certificat ne sont pas en place, IXIN OS est construit sans prise en
charge de Secure Boot (comme avant : Secure Boot à désactiver).
EOF
