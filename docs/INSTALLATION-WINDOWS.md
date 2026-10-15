# Installer IXIN OS à côté de Windows — IXIN OS Setup

**IXIN-OS-Setup.exe** installe IXIN OS sur un PC Windows **sans clé USB** : il prépare
l'espace sur le disque depuis Windows, puis redémarre sur l'installation. Windows et vos
fichiers restent en place ; au démarrage du PC, un menu permet ensuite de choisir **IXIN OS**
ou **Windows**.

> Pour installer IXIN OS seul sur un PC, dans une machine virtuelle, ou si votre PC n'est pas
> compatible (voir ci-dessous), utilisez l'ISO sur une clé USB (Rufus ou Ventoy).

---

## 1. Ce qu'il faut

| | |
|---|---|
| Windows | Windows 10 ou 11, 64 bits |
| Démarrage | **UEFI** (cas de presque tous les PC depuis 2012) avec un disque **GPT** |
| Espace libre sur Windows (C:) | environ **75 Go** : 45 Go minimum pour IXIN OS (80 Go conseillés pour les jeux), 6 Go pour le programme d'installation, 5 Go de téléchargement temporaire, et 20 Go laissés libres à Windows |
| Alimentation | portable branché sur secteur |
| Précaution | une **sauvegarde** de vos fichiers importants (aucun fichier n'est supprimé, mais un disque se modifie toujours avec prudence) |

IXIN OS Setup vérifie tout cela lui-même et explique ce qui bloque.

---

## 2. Installation pas à pas

1. **Téléchargez IXIN-OS-Setup.exe** sur la page des versions (avec l'ISO d'IXIN OS) et lancez-le.
   Windows demande l'autorisation de modifier l'appareil : répondez **Oui**.
   *Si Windows affiche « Windows a protégé votre ordinateur »* : cliquez sur **Informations
   complémentaires → Exécuter quand même** (le programme n'est pas encore signé par un
   certificat de signature de code ; son code source est public, dossier `windows/`).
2. **Vérification du PC** : chaque point est expliqué (✔ bon, ⚠ à savoir, ✖ bloquant).
3. **Espace pour IXIN OS** : choisissez la taille (curseur). Le programme d'installation est
   téléchargé automatiquement (environ 5 Go), ou vous pouvez choisir une ISO déjà téléchargée
   (le fichier `.sha256` publié avec elle doit être dans le même dossier).
4. **BitLocker** (si Windows est chiffré) : votre **clé de récupération** de 48 chiffres est
   affichée ; notez-la ou enregistrez-la **ailleurs que sur ce PC** (clé USB, téléphone, papier).
5. **Confirmation**, puis **préparation** : téléchargement et vérification de l'ISO, réduction
   de Windows, création de la partition « IXIN-SETUP », menu de démarrage. Si Windows propose
   de « formater » un nouveau disque pendant cette étape, cliquez sur **Annuler**.
6. **Redémarrage** :
   - **Secure Boot désactivé** : le PC redémarre directement sur l'installation (une seule fois).
   - **Secure Boot activé** : voir la section 3, puis le PC démarre sur l'installation.
7. Dans le menu **IXIN OS Setup**, choisissez **« Installer IXIN OS (à côté de Windows) »**.
   Le bureau d'IXIN OS s'ouvre avec une fenêtre d'accueil : cliquez sur **« Lancer l'installation »**.
8. Dans l'installation, à l'étape **« Partitions »** :
   - **« Remplacer une partition »** est déjà sélectionné ;
   - cliquez sur la zone grise **« Espace libre »** dans la barre du disque ;
   - cochez **« Chiffrer le système »** et choisissez une phrase secrète (recommandé).

   Pour protéger Windows, effacer le disque ou réduire de nouveau Windows est refusé dans ce mode,
   et une partition Windows ne peut jamais être choisie pour être remplacée.
9. Terminez l'installation (nom, mot de passe), puis redémarrez. Au démarrage, le menu IXIN propose
   **IXIN OS**, **Windows** et les paramètres du firmware (UEFI). Choisir **Windows** redémarre
   aussitôt le PC directement sur Windows, par son entrée du firmware, exactement comme si IXIN
   n'était pas là : BitLocker ne demande pas sa clé de récupération.

Au premier démarrage d'IXIN OS, la partition « IXIN-SETUP » (6 Go) et le menu « IXIN OS Setup »
sont supprimés automatiquement. Pour rendre ces 6 Go à Windows : relancez IXIN OS Setup sous
Windows et cliquez sur **« Rendre … à Windows »** (ou *Gestion des disques → Étendre le volume*).

---

## 3. Secure Boot

IXIN OS ne démarre pas encore avec Secure Boot activé (prévu dans une prochaine version).
IXIN OS Setup redémarre directement dans les **paramètres UEFI** du PC :

1. cherchez **Secure Boot** (souvent dans l'onglet *Boot*, *Security* ou *Authentication*) ;
2. réglez-le sur **Disabled** ;
3. enregistrez et quittez (souvent **F10**).

Si le PC redémarre sur Windows au lieu de l'installation, relancez IXIN OS Setup et cliquez sur
**« Redémarrer et installer IXIN OS »**, ou choisissez **« IXIN OS Setup »** dans le menu de
démarrage du PC (touche **F12**, **F11**, **F8** ou **Échap** au démarrage selon la marque).

⚠️ **Jeux Windows** : certains jeux avec anti-triche (Valorant, Battlefield 6…) exigent Secure Boot
**sous Windows**. Tant que Secure Boot est désactivé, ces jeux ne se lancent plus sous Windows.

---

## 4. BitLocker et chiffrement de l'appareil

Si Windows est chiffré, IXIN OS Setup :
- affiche votre clé de récupération et ne continue qu'une fois qu'elle est notée (il en crée une
  si le disque n'en a pas) ;
- suspend la protection BitLocker pendant **3 démarrages** de Windows : les changements du
  démarrage (menu IXIN, désactivation de Secure Boot) ne déclenchent alors pas la demande de clé ;
  Windows réactive ensuite la protection tout seul.

Si Windows demande malgré tout la clé de récupération au démarrage, saisissez-la : elle est aussi
souvent enregistrée sur votre compte Microsoft (**aka.ms/myrecoverykey**).

---

## 5. Annuler

Tant qu'IXIN OS n'est pas installé, relancez IXIN OS Setup et choisissez
**« Annuler la préparation »** : la partition IXIN-SETUP, le menu de démarrage et les réglages
sont retirés, et l'espace est rendu à Windows. En cas d'erreur ou d'annulation pendant la
préparation, cette remise en état est automatique.

---

## 6. Dépannage

**« C: ne peut pas être réduit assez »** — Windows a assez d'espace libre, mais des fichiers système
impossibles à déplacer se trouvent en fin de disque (Windows ne peut pas réduire sa partition au-delà) :

1. cliquez sur **« Débloquer la réduction »** : la mise en veille prolongée est désactivée (le fichier
   `hiberfil.sys` disparaît) et l'espace libre est regroupé, puis l'analyse est refaite ;
2. si ce n'est pas suffisant, désactivez temporairement le **fichier d'échange** : menu Démarrer →
   « Afficher les paramètres système avancés » → *Performances : Paramètres* → *Avancé* →
   *Mémoire virtuelle : Modifier* → décochez « Gestion automatique », choisissez **« Aucun fichier
   d'échange »** → *Définir* → *OK*, **redémarrez** et relancez IXIN OS Setup. Une fois la préparation
   terminée, remettez « Gestion automatique » ;
3. si c'est encore insuffisant, les **points de restauration** de Windows peuvent bloquer aussi :
   menu Démarrer → « Créer un point de restauration » → *Configurer* → *Supprimer* (les anciens points
   de restauration sont perdus), puis relancez IXIN OS Setup.

**Le menu « IXIN OS Setup » n'apparaît pas au redémarrage** — certains PC ignorent la demande de
démarrage unique de Windows. Ouvrez le menu de démarrage du PC (**F12** sur Dell et Lenovo, **F9** sur HP,
**F8** sur Asus, **F11** sur MSI, **Échap** ou **F12** sur Acer) et choisissez **« IXIN OS Setup »**.

**Après l'installation, le PC démarre directement sur Windows, sans le menu IXIN** — certaines mises
à jour de Windows remettent Windows en premier dans l'ordre de démarrage. Relancez IXIN OS Setup et
cliquez sur **« Remettre le menu IXIN au démarrage »** (ou choisissez « IXIN » dans le menu de démarrage
du PC, touches ci-dessus).

**Windows demande la clé de récupération BitLocker** — voir la section 4.

---

## 7. Ce qui est modifié sur le PC

| Modification | Retour en arrière |
|---|---|
| Windows (C:) réduit de la taille choisie + 6 Go | « Annuler la préparation » le rend à Windows |
| Partition exFAT « IXIN-SETUP » (6 Go, sans lettre de lecteur) contenant l'ISO vérifiée | supprimée au premier démarrage d'IXIN OS, ou par « Annuler » |
| `\EFI\IXIN-SETUP\grubx64.efi` dans la partition EFI de Windows (3 Mo) | idem |
| Entrée « IXIN OS Setup » du menu de démarrage du PC (Windows reste le choix par défaut) | idem |
| Démarrage rapide de Windows désactivé, horloge matérielle en UTC | « Annuler » rétablit les anciennes valeurs |
| BitLocker suspendu 3 démarrages (si actif) | réactivation automatique par Windows |

Journal détaillé : `C:\ProgramData\IXIN\setup.log` ; état : `C:\ProgramData\IXIN\setup-state.json`.

---

## 8. Sécurité

- L'ISO est toujours vérifiée avec l'**empreinte SHA-256** publiée avec la version, après le
  téléchargement puis après sa copie dans la partition IXIN-SETUP.
- Le programme utilise les commandes de stockage de Windows lui-même (celles de *Gestion des
  disques*) et ne touche jamais aux fichiers de Windows.
- Le code source est public : `windows/IxinSetup/` (programme) et `windows/boot/` (menu de
  démarrage). Il est testé à chaque modification sur un vrai Windows, sur un disque virtuel
  (préparation complète puis annulation).

## 9. Limites connues

- Démarrage BIOS (Legacy) ou disque MBR : non pris en charge, utilisez une clé USB.
- PC avec plusieurs disques : IXIN OS s'installe sur le disque de Windows.
- Secure Boot : à désactiver (section 3).
- Testé sur Windows (disque virtuel) ; à valider sur une grande variété de PC réels (firmwares de
  marques différentes) : signalez tout problème avec le fichier `C:\ProgramData\IXIN\setup.log`.
