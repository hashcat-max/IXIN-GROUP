# IXIN OS — reste à faire

Liste des travaux mis de côté volontairement, pour les reprendre plus tard (dans n'importe quelle session).

## Secure Boot — en pause (décision du 26 octobre 2026)

**État** : tout est construit et testé en machine virtuelle, mais **inactif** tant que la clé n'existe pas.
Les versions publiées (jusqu'à `build-2026.10.25-6` comprise) demandent donc de **désactiver Secure Boot**,
et le site doit continuer à l'indiquer.

Ce qui est fait :
- Chaîne de démarrage : shim (signé par Microsoft) → GRUB d'IXIN signé (polices du thème intégrées) → noyau
  `linux-zen-ixin` signé (republié depuis linux-zen d'Arch, toutes les 6 heures).
- Clé USB / ISO : shim + systemd-boot signé, `ixin-secureboot.cer` à la racine (« Enroll key from disk »).
- Système installé : paquet `ixin-secureboot` (installateur, mises à jour, entrée « IXIN OS » du firmware).
- IXIN OS Setup (Windows) : demande d'autorisation de la clé IXIN (MokManager, mot de passe `ixinos`) au lieu
  de faire désactiver Secure Boot.
- Testé en VM (firmware avec les seules clés Microsoft) : clé USB, demande écrite comme depuis Windows,
  système installé (menu graphique, noyau signé), noyau non signé refusé.
- Guides : étapes MokManager avec captures (déjà dans INSTALLATION-CLE-USB.md et INSTALLATION-WINDOWS.md).

Pour l'activer :
1. Créer la clé, une seule fois : `tools/create-secureboot-key.sh`.
2. GitHub → Settings → Secrets and variables → Actions : secret **`IXIN_SECUREBOOT_KEY`** = contenu de
   `ixin-secureboot-PRIVEE.key`, puis ranger ce fichier hors ligne.
3. Commiter le certificat public : `repo/secureboot/ixin-secureboot.crt`.
4. Avant de publier, tester en VM le chargeur « depuis Windows » (`\EFI\IXIN-SETUP` : shim + GRUB autonome)
   avec Secure Boot activé : **pas encore testé**.
5. Lancer « Construire l'ISO IXIN OS » ; écrire « compatible Secure Boot » dans les notes de la version.
6. Mettre à jour : `tools/prepare-site-feed.py` (`"secure_boot": "à désactiver"` → pris en charge),
   `docs/site/nouveautes.json`, `docs/CONTEXTE-IXIN-OS.md` (§7), puis donner au projet du site
   os.ixin.online le prompt pour retirer « Secure Boot à désactiver ».

## Autres

- Raspberry Pi (version ARM séparée, sans les jeux Windows) : **seulement si confirmé**.
- Secrets Cloudflare pour reconstruire le site automatiquement (voir `docs/SITE.md`) : quand le site est prêt.
