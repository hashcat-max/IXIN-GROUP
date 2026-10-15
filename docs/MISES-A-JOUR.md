# IXIN Update — mises à jour automatiques d'IXIN OS

IXIN OS se met à jour tout seul, comme Windows ou macOS : correctifs de sécurité dès qu'ils
sont disponibles, point de restauration avant chaque mise à jour, notifications quand un
redémarrage est nécessaire. L'équipe IXIN peut **pousser** des mises à jour et des correctifs
urgents sur tous les PC IXIN OS, de façon **signée** (aucun tiers ne peut se faire passer pour elle).

---

## 1. Côté utilisateur

**Centre IXIN → onglet « Mises à jour »** (ou l'icône IXIN Update dans la barre des tâches) :
état, liste des mises à jour (🛡 = correctif de sécurité, avec ses CVE), historique, mode.

| Mode | Comportement |
|---|---|
| **Automatique** (par défaut) | Correctifs de sécurité installés dès qu'ils sont publiés ; le reste une fois par jour, en arrière-plan |
| **Me prévenir seulement** | Recherche et notification ; vous cliquez sur « Installer maintenant » |
| **Manuel** | Rien d'automatique (déconseillé) |

Garde-fous :
- **point de restauration Btrfs** (Timeshift) avant chaque mise à jour, y compris celles lancées à la main avec `pacman` ;
- mises à jour non urgentes **reportées** pendant un jeu, sur batterie ou sur une connexion limitée (forfait mobile) ;
- **jamais d'extinction pendant une installation** : l'arrêt du PC est retardé jusqu'à la fin ;
- si une annonce Arch Linux récente signale une intervention manuelle possible, les mises à jour
  non urgentes attendent jusqu'à 3 jours (le temps que l'équipe IXIN vérifie) ;
- si le dépôt IXIN ou Chaotic-AUR est injoignable ou **non vérifiable**, il est ignoré pour cette fois
  et les correctifs Arch continuent d'arriver.

**Revenir en arrière** : Centre IXIN → Mises à jour → *Points de restauration (Timeshift)* →
choisir le point « IXIN Update — avant … » → *Restaurer* → redémarrer. Au redémarrage suivant,
IXIN Update remet automatiquement le noyau correspondant sur la partition de démarrage.
Si le PC ne démarre plus du tout : démarrer sur la clé IXIN OS, lancer Timeshift et restaurer.

Commandes :

| Commande | Rôle |
|---|---|
| `ixin-update` | état + mise à jour interactive dans le terminal |
| `ixin-update status` | état (`--json` pour les scripts) |
| `sudo ixin-update check` / `apply` | rechercher / installer maintenant |
| `sudo ixin-update snapshot` | point de restauration manuel |
| `journalctl -u ixin-update` ou `/var/log/ixin-update.log` | journal |

Réglages avancés : `/etc/ixin/update.conf` (valeurs par défaut commentées dans `/usr/lib/ixin/update.conf`).

---

## 2. Côté équipe IXIN : pousser une mise à jour

Tout ce qu'IXIN installe est dans des **paquets pacman** du dossier `packages/` :

| Paquet | Contenu |
|---|---|
| `ixin-tools` | Centre IXIN, `ixin-secure`, `ixin-gaming` |
| `ixin-update` | IXIN Update (agent, notifications, minuteries, règles polkit) |
| `ixin-settings` | durcissement noyau, DNS chiffré, vie privée réseau, réglages KDE, identité IXIN OS |
| `ixin-artwork` | logos, fonds d'écran, thèmes Plasma / GRUB / Plymouth / SDDM |
| `ixin-keyring` | clé publique de l'équipe IXIN (générée automatiquement) |
| `ixin-mirrorlist` | serveurs du dépôt `[ixin]` |
| `ixin-desktop` | méta-paquet : **ajouter une dépendance ici l'installe sur tous les PC** |

### Publier une mise à jour normale
1. Modifier les fichiers dans `packages/<paquet>/rootfs/…` (chemins identiques à ceux du système).
2. Commit + push sur la branche principale.
3. L'action **« Publier les mises à jour IXIN »** construit, signe et publie le dépôt `[ixin]` sur
   **GitHub Pages** (`https://hashcat-max.github.io/IXIN-GROUP/repo`) et sur la branche **`ixin-repo`**
   (adresse de secours, `raw.githubusercontent.com`). La version d'un paquet = date du dernier commit
   qui l'a modifié : seuls les paquets modifiés sont retéléchargés par les PC.
   (Les versions — « releases » — du dépôt GitHub sont immuables : on ne peut pas y remplacer des
   fichiers, elles servent donc uniquement aux ISO.)
4. Les PC l'installent dans les **24 h** (mode automatique) ou préviennent l'utilisateur (mode « prévenir »).

### Correctif urgent (faille grave)
Dans le même commit, mettre `"urgent": true` dans `repo/policy.json` (et éventuellement une
`"notice"`). Les PC vérifient la politique **toutes les heures** et installent immédiatement,
sans attendre la fenêtre quotidienne. Remettre `"urgent": false` au commit suivant.

### Bloquer une mise à jour Arch défectueuse
`"hold": ["mesa"]` dans `repo/policy.json` → en moins d'une heure, plus aucun PC ne l'installe.
À retirer une fois le problème corrigé (le blocage expire de toute façon avec la politique, 14 jours).

### Valider une annonce Arch Linux
Après vérification d'une annonce https://archlinux.org/news/, ajouter son lien dans `"news_ack"` :
les mises à jour automatiques reprennent sans attendre 3 jours.

### Message à tous les utilisateurs
`"notice": {"level": "info", "text": "…"}` (`"critical"` pour une alerte) → notification KDE
et affichage dans le Centre IXIN.

### Correctif urgent d'un paquet Arch
Créer `packages/<nom>/PKGBUILD` qui reconstruit le paquet corrigé avec une version supérieure :
le dépôt `[ixin]` est placé avant `[core]`, il est donc prioritaire. À réserver aux urgences.

---

## 3. Créer la clé de signature (une seule fois)

Sans cette clé, **aucune** mise à jour IXIN n'est acceptée par les PC (c'est voulu).

1. Sur un ordinateur de confiance : IXIN OS, Linux, macOS, ou Windows avec **Git Bash**
   (installé avec [Git for Windows](https://git-scm.com/download/win)) :
   ```bash
   git clone https://github.com/hashcat-max/IXIN-GROUP && cd IXIN-GROUP
   tools/create-signing-key.sh
   ```
   (Autre possibilité sous Windows : Kleopatra/Gpg4win → *Nouvelle paire de clés* OpenPGP,
   **sans date d'expiration**, puis *Exporter les clés secrètes*.)
2. GitHub → *Settings → Secrets and variables → Actions → New repository secret* :
   nom **`IXIN_SIGNING_KEY`**, valeur = contenu complet de `ixin-signing-key-PRIVEE.asc`.
   (Si la clé a une phrase de passe : second secret `IXIN_SIGNING_KEY_PASSPHRASE`.)
3. Ranger la clé privée et le certificat de révocation **hors ligne**, puis les supprimer de l'ordinateur.
4. Activer **GitHub Pages** (hébergement principal des mises à jour) : *Settings → Pages →
   Build and deployment → Source : **GitHub Actions***. Sans cela, les PC utilisent l'adresse de
   secours (branche `ixin-repo`), que GitHub limite davantage.
5. Lancer l'action **« Publier les mises à jour IXIN »** puis **« Construire l'ISO IXIN OS »** :
   la clé publique est intégrée automatiquement à l'ISO (`ixin-keyring`).

**Changer de clé** :
1. créer la nouvelle clé (`tools/create-signing-key.sh nouvelle-cle`) et copier sa clé **publique** dans
   `packages/ixin-keyring/extra-keys.asc`, commit + push : `ixin-keyring` (encore signé par l'ancienne clé)
   apprend la nouvelle clé à tous les PC ;
2. quelques jours plus tard, remplacer le secret `IXIN_SIGNING_KEY` par la nouvelle clé privée ;
3. enfin, inscrire l'empreinte de l'ancienne clé dans `packages/ixin-keyring/ixin-revoked`
   (une ligne par empreinte) et supprimer `extra-keys.asc`.

En cas de **vol** de la clé : faire immédiatement les étapes 1 à 3 et prévenir les utilisateurs
(la clé volée reste dangereuse tant que les PC n'ont pas reçu la révocation).

**Bon à savoir** : GitHub met en pause les tâches planifiées d'un dépôt resté 60 jours sans
activité. Si l'action « Publier les mises à jour IXIN » est en pause (onglet *Actions*), la
réactiver : sinon la politique n'est plus re-signée et expire au bout de 14 jours (les
mises à jour continuent, mais sans blocages ni messages IXIN).

---

## 4. Modèle de sécurité

| Élément | Protection |
|---|---|
| Paquets IXIN | signature OpenPGP obligatoire (`SigLevel = Required`) |
| Base du dépôt `[ixin]` | signée ; si elle est modifiée, le dépôt est ignoré et l'incident est signalé |
| Politique (urgent / blocages / messages) | signée **par une clé IXIN uniquement** (pas les clés Arch), numéro de série croissant (pas de rejeu d'une ancienne politique), validité 14 jours |
| Clé privée | uniquement dans les secrets GitHub (jamais dans le dépôt) |
| Dépôts Arch / Chaotic-AUR | signatures de leurs mainteneurs (trousseaux `archlinux-keyring`, `chaotic-keyring` mis à jour en premier) |

Testé en machine virtuelle :
- **installation complète depuis l'ISO** (disque chiffré) : au premier démarrage, point de restauration
  initial puis mises à jour installées automatiquement ; mises à jour IXIN poussées en « urgent »
  → installées toutes seules, avec point de restauration, notification et annonce IXIN ; le
  notificateur se met lui-même à jour sans fermer la session ;
- sur une bêta migrée : politique falsifiée → rejetée ; ancienne politique correctement signée →
  rejetée ; base du dépôt modifiée → dépôt ignoré, correctifs Arch maintenus ; coupure de courant
  pendant une mise à jour → installation réparée au démarrage suivant.

---

## 5. PC installés avec une bêta (avant l'intégration d'IXIN Update)

```bash
curl -fsSL https://raw.githubusercontent.com/hashcat-max/IXIN-GROUP/HEAD/tools/ixin-enable-updates.sh | sudo bash
```
Le script ajoute le dépôt `[ixin]`, installe la clé IXIN et remplace les fichiers de la bêta par les
paquets IXIN (désormais mis à jour automatiquement). Pour exiger une empreinte précise :
`… | sudo IXIN_KEY_FPR=<empreinte> bash`.
