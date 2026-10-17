# Installer IXIN OS avec une clé USB — guide pas à pas

Ce guide est écrit pour quelqu'un qui n'a **jamais installé de système**. Il faut compter environ
**une heure**, dont la moitié d'attente. Lisez une étape en entier avant de la faire.

> **Vous êtes sous Windows 10 ou 11 et vous voulez garder Windows ?** Il y a plus simple, sans clé USB :
> **IXIN OS Setup** (`IXIN-OS-Setup.exe`) fait tout depuis Windows — voir le
> [guide « Installer depuis Windows »](INSTALLATION-WINDOWS.md). Ce guide-ci est pour les autres cas :
> PC sans Windows, IXIN OS sur tout le disque, vieux PC (BIOS), machine virtuelle, ou PC refusé par IXIN OS Setup.

---

## 1. Ce qu'il vous faut

| | |
|---|---|
| **Un PC** | Processeur **64 bits Intel ou AMD** (presque tous les PC depuis 2008). Pas de Mac M1/M2/M3/M4 ni de PC à processeur ARM (Snapdragon). |
| **Mémoire (RAM)** | **4 Go** minimum, 8 Go ou plus conseillés. |
| **Disque** | **40 Go** libres minimum, 80 Go conseillés pour les jeux. |
| **Une clé USB** | **8 Go** ou plus. **Tout son contenu sera effacé** : copiez ailleurs ce qu'elle contient. |
| **Une connexion Internet** | Pour télécharger IXIN OS (environ 5 Go). |
| **Un autre ordinateur ?** | Pas obligatoire : la clé peut être préparée sur le PC où IXIN OS sera installé. |

> ⚠️ **Sauvegardez vos fichiers importants** (photos, documents) sur un disque externe ou un cloud avant
> d'installer un système. L'installation ne touche que ce que vous lui indiquez, mais une erreur de clic
> est vite arrivée.

---

## 2. Télécharger IXIN OS

1. Allez sur **[os.ixin.online/telecharger](https://os.ixin.online/telecharger)**.
2. Cliquez sur **« Télécharger l'ISO »**. Vous obtenez **un seul fichier** d'environ 5 Go dont le nom finit
   par **`.iso`** (par exemple `ixin-os-2026.10.17-x86_64.iso`). Le téléchargement peut reprendre s'il est coupé.
3. Laissez le fichier dans votre dossier **Téléchargements**.

> Une **ISO** est une « image » : un fichier unique qui contient tout le système. On ne l'ouvre pas, on
> la copie sur une clé USB avec un petit logiciel (étape 3).

**Vérifier le fichier (facultatif, conseillé)** — pour être sûr qu'il n'est pas abîmé ou modifié.
L'empreinte attendue (une longue suite de chiffres et de lettres, « SHA-256 ») est affichée sur la page de
téléchargement.
- **Windows** : clic droit sur le menu Démarrer → **Terminal** (ou PowerShell), puis tapez
  `Get-FileHash $HOME\Downloads\ixin-os-*.iso` et appuyez sur Entrée. Comparez le résultat avec l'empreinte du site.
- **macOS** : Terminal → `shasum -a 256 ~/Downloads/ixin-os-*.iso`
- **Linux** : `sha256sum ~/Téléchargements/ixin-os-*.iso`

Si les deux empreintes sont différentes, supprimez le fichier et téléchargez-le de nouveau.

<details>
<summary>Vous avez téléchargé l'ISO depuis GitHub, en plusieurs morceaux (<code>.part00</code>, <code>.part01</code>…) ?</summary>

GitHub limite chaque fichier à 2 Go : l'ISO y est découpée. Il faut recoller les morceaux, dans l'ordre :
- **Windows** (Terminal, dans le dossier Téléchargements) :
  `cmd /c copy /b ixin-os-AAAA.MM.JJ-x86_64.iso.part00+ixin-os-AAAA.MM.JJ-x86_64.iso.part01+ixin-os-AAAA.MM.JJ-x86_64.iso.part02 ixin-os.iso`
  (remplacez `AAAA.MM.JJ` par la date du fichier ; ajoutez les morceaux suivants s'il y en a plus) ;
- **macOS / Linux** : `cat ixin-os-*.iso.part* > ixin-os.iso`

Le plus simple reste le téléchargement en un seul fichier sur os.ixin.online.
</details>

---

## 3. Préparer la clé USB

Branchez la clé USB, puis suivez **une seule** des méthodes ci-dessous.

### Sous Windows : Rufus (recommandé)

1. Téléchargez **Rufus** sur **[rufus.ie](https://rufus.ie)** (« Rufus 4.x Portable » suffit : rien à installer)
   et lancez-le. Répondez **Oui** aux questions de Windows.
2. **Périphérique** : choisissez votre clé USB. Vérifiez bien son nom et sa taille : c'est elle qui sera effacée.
3. **Type de démarrage** : cliquez sur **SÉLECTION** et choisissez le fichier `ixin-os-….iso`.
4. **Schéma de partition** : **GPT** pour un PC récent (2012 et plus). Pour un vieux PC qui démarre en
   « BIOS » (« Legacy »), choisissez **MBR**. Laissez le reste tel quel.
5. Cliquez sur **DÉMARRER**.
6. Rufus demande sous quelle forme écrire l'image : choisissez **« Écrire en mode Image DD »** puis **OK**.
   *(C'est important : l'autre mode ne fonctionne pas avec IXIN OS, dont un fichier dépasse 4 Go.)*
7. Confirmez que la clé sera effacée (**OK**) et attendez « **PRÊT** » (5 à 15 minutes). Fermez Rufus.

> Windows peut ensuite dire que la clé « doit être formatée » ou ne plus afficher sa taille réelle :
> c'est normal, **ne la formatez pas**. Elle redeviendra une clé ordinaire quand vous la reformaterez
> plus tard, une fois IXIN OS installé.

### Sous Windows, macOS ou Linux : balenaEtcher (le plus simple)

1. Téléchargez **balenaEtcher** sur **[etcher.balena.io](https://etcher.balena.io)** et lancez-le.
2. **Flash from file** → choisissez `ixin-os-….iso`.
3. **Select target** → cochez votre clé USB (vérifiez son nom et sa taille).
4. **Flash!** → donnez votre mot de passe si l'ordinateur le demande, puis attendez « Flash Complete ».

### Pour les habitués : Ventoy, ou la ligne de commande

- **[Ventoy](https://www.ventoy.net)** : installez Ventoy sur la clé une fois, puis copiez simplement le
  fichier `.iso` dessus (plusieurs systèmes sur la même clé). Au démarrage, choisissez l'ISO, puis
  « Boot in normal mode ».
- **Linux** : `sudo dd if=ixin-os-….iso of=/dev/sdX bs=4M status=progress oflag=sync`
  (`/dev/sdX` = la clé entière, vérifiée avec `lsblk` ; une erreur de lettre efface un autre disque).

---

## 4. Démarrer le PC sur la clé USB

La clé étant branchée sur le PC où installer IXIN OS :

### Méthode 1 — depuis Windows, sans touche à trouver (Windows 10 / 11)

1. **Paramètres** → **Système** → **Récupération** (Windows 10 : **Mise à jour et sécurité** → **Récupération**).
2. À côté de **Démarrage avancé**, cliquez sur **Redémarrer maintenant**.
3. Sur l'écran bleu : **Utiliser un périphérique** → choisissez votre clé USB (souvent « UEFI: USB… » ou le
   nom de la marque de la clé).

> Si « Utiliser un périphérique » n'apparaît pas, ou si le PC revient sur Windows, passez à la méthode 2.

### Méthode 2 — la touche du menu de démarrage

Éteignez complètement le PC. Rallumez-le et **appuyez plusieurs fois de suite** (une fois par seconde)
sur la touche du menu de démarrage, **dès l'allumage**, jusqu'à voir une liste de périphériques :

| Marque du PC | Touche du menu de démarrage |
|---|---|
| Acer, Dell, Lenovo, Toshiba / Dynabook, Gigabyte, Fujitsu, Huawei, Xiaomi | **F12** (Lenovo : parfois **Fn + F12**, ou le petit bouton « Novo ») |
| HP | **Échap**, puis **F9** |
| ASUS | **F8** (PC de bureau) ou **Échap** (portable) |
| MSI, ASRock | **F11** |
| Samsung | **Échap** ou **F2** |
| Medion | **F10** |
| Microsoft Surface | Maintenez **Volume −** en appuyant sur le bouton d'allumage |
| Mac (processeur Intel) | Maintenez **Option (⌥)** à l'allumage → **EFI Boot** |

Dans la liste, choisissez la clé USB avec les flèches du clavier, puis **Entrée**. Si la clé apparaît deux
fois, prenez celle qui commence par **UEFI**.

### Le PC refuse de démarrer sur la clé ? (« Secure Boot violation », retour sur Windows…)

IXIN OS ne démarre pas encore quand **Secure Boot** est activé (c'est prévu dans une prochaine version).
Il faut le désactiver une fois :

1. Entrez dans les **paramètres du PC** (« BIOS » / « UEFI ») : depuis Windows, **Démarrage avancé** →
   **Dépannage** → **Options avancées** → **Paramètres du microprogramme UEFI** → **Redémarrer** ; ou, à
   l'allumage, la touche **F2** ou **Suppr** (HP : **F10** ; Lenovo : **F1** ou **F2**).
2. Trouvez **Secure Boot** (onglet **Boot**, **Security** ou **Authentication**) et réglez-le sur **Disabled**.
   - Option grisée ? Définissez d'abord un mot de passe administrateur (« **Set Supervisor Password** »),
     désactivez Secure Boot, puis vous pourrez retirer ce mot de passe.
   - Sur certains PC, il faut choisir « **Other OS** » au lieu de « Windows UEFI mode ».
3. **Enregistrez et quittez** (souvent **F10**, puis **Yes**).

> ⚠️ Windows chiffré par **BitLocker** : désactiver Secure Boot peut faire demander la **clé de récupération
> BitLocker** au prochain démarrage de Windows. Notez-la avant (compte Microsoft →
> [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)), ou suspendez BitLocker dans Windows (Panneau de
> configuration → Chiffrement de lecteur BitLocker → **Interrompre la protection**).

---

## 5. Essayer IXIN OS (sans rien installer)

Un menu IXIN s'affiche pendant 10 secondes :

| Choix | Quand l'utiliser |
|---|---|
| **IXIN OS - Live / Installation** | Le choix normal (sélectionné tout seul). |
| **IXIN OS - Live chargé en RAM** | Plus rapide, et la clé peut être retirée ensuite (8 Go de RAM ou plus). |
| **IXIN OS - Mode compatibilité graphique** | Si l'écran reste **noir** ou se brouille avec le choix normal. |
| **Test de la mémoire** | Si le PC plante sans raison : vérifie la RAM. |

Après une minute environ, le **bureau IXIN** apparaît : c'est IXIN OS qui fonctionne depuis la clé.
**Rien n'est encore modifié sur le PC.** Profitez-en pour vérifier que tout marche : Wi-Fi (icône en bas à
droite), son, écran, clavier, pavé tactile. Tout ce que vous faites ici disparaît à l'extinction.

---

## 6. Installer IXIN OS

Double-cliquez sur **« Installer IXIN OS »** (sur le bureau). Branchez le chargeur si c'est un portable.

1. **Bienvenue** : la langue (Français). **Suivant**.
2. **Emplacement** : votre ville / fuseau horaire (Europe/Paris par défaut). **Suivant**.
3. **Clavier** : **AZERTY français** par défaut — tapez quelques lettres dans la zone de test. **Suivant**.
4. **Partitions** — l'étape importante. Choisissez **le disque** en haut (vérifiez sa taille), puis :
   - **Effacer le disque** : IXIN OS prend **tout le disque**. **Tout ce qu'il contient est supprimé**
     (Windows compris). Le plus simple sur un PC dédié à IXIN OS.
   - **Installer à côté** : garde Windows et réduit sa place. *Préférez IXIN OS Setup depuis Windows, qui
     prépare cela plus sûrement (BitLocker, démarrage rapide…).*
   - **Remplacer une partition** / **Partitionnement manuel** : pour les utilisateurs expérimentés.
   - Cochez **« Chiffrer le système »** et choisissez une **phrase de passe** : sans elle, personne ne peut lire
     vos fichiers si le PC est volé. **Notez-la** : sans elle, vos données sont perdues. Elle sera demandée à
     chaque démarrage.

   Avec « Remplacer une partition », les partitions de Windows (NTFS, BitLocker) ne peuvent pas être choisies
   par erreur. « Effacer le disque », lui, efface **tout**, Windows compris.
5. **Utilisateurs** : votre nom, le nom d'utilisateur, le nom du PC et un **mot de passe**. **Suivant**.
6. **Résumé** : relisez — surtout le disque. **Installer**, puis confirmez.
7. L'installation dure **10 à 30 minutes**. À la fin : cochez **Redémarrer maintenant**, **Terminé**, puis
   **retirez la clé USB** quand l'écran devient noir.

---

## 7. Premier démarrage

1. Si vous avez chiffré le disque : l'écran IXIN demande la **phrase de passe** du chiffrement.
2. Le menu de démarrage propose **IXIN OS** (et **Windows**, si vous l'avez gardé).
3. Connectez-vous avec votre mot de passe : **IXIN Center** s'ouvre et présente le système.
4. Connectez-vous au Wi-Fi (icône en bas à droite). IXIN OS se **met à jour tout seul** (comme Windows Update) :
   correctifs de sécurité dès leur publication, point de restauration avant chaque mise à jour.

Votre clé USB peut redevenir une clé ordinaire : dans IXIN OS, ouvrez **KDE Partition Manager** (ou sous
Windows **Gestion des disques**), supprimez ses partitions et créez-en une nouvelle en **exFAT**.

---

## 8. Dans une machine virtuelle (VirtualBox, VMware, Hyper-V…)

- Type **Linux / Arch Linux (64-bit)**, **4 Go** de RAM au moins (8 conseillés), **2 processeurs** ou plus,
  disque de **50 Go**.
- **Activez l'EFI** (VirtualBox : Système → Carte mère → « Activer l'EFI » ; Hyper-V : génération 2 avec
  Secure Boot **désactivé**). Le mode BIOS fonctionne aussi.
- Mémoire vidéo au maximum ; accélération 3D facultative.
- Démarrez sur le fichier `.iso` directement (pas besoin de clé USB) et suivez l'étape 6.

---

## 9. Problèmes fréquents

| Ce qui se passe | Que faire |
|---|---|
| Le PC démarre sur Windows au lieu de la clé | Méthode 1 de l'étape 4, ou la touche du menu de démarrage (appuyez **dès l'allumage**, plusieurs fois). Essayez un autre port USB (de préférence directement sur le PC, pas sur un hub). Sur certains PC, désactivez « **Fast Boot** » dans les paramètres. |
| « Secure Boot violation », « Invalid signature » | Désactivez Secure Boot (étape 4). |
| La clé n'apparaît pas dans le menu de démarrage | Refaites la clé avec Rufus en **mode Image DD**, ou avec balenaEtcher. Vieux PC : schéma **MBR** dans Rufus, et activez « **CSM** » / « **Legacy** » dans les paramètres. |
| Écran noir après le menu IXIN | Redémarrez et choisissez **Mode compatibilité graphique**. Les cartes NVIDIA anciennes (GeForce GTX 10xx et plus vieilles) reçoivent automatiquement le pilote libre adapté. |
| Pas de Wi-Fi | Branchez un câble Ethernet ou partagez la connexion du téléphone en USB, puis installez les mises à jour. Certaines puces Broadcom anciennes ne sont pas reconnues. |
| L'installation ne trouve **aucun disque** | Le disque est en mode **RAID / Intel RST / VMD** (fréquent sur les portables récents). Dans les paramètres du PC, le réglage « SATA Mode » / « VMD Controller » permet de passer en **AHCI** — **mais Windows ne démarre plus** après ce changement s'il n'a pas été préparé : si vous gardez Windows, cherchez « passer en AHCI sans réinstaller Windows » (mode sans échec) avant, ou demandez de l'aide. |
| Windows demande la clé de récupération BitLocker | Saisissez-la (compte Microsoft → [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)). Elle n'est demandée qu'une fois. |
| L'heure de Windows est décalée d'une heure ou deux | Dans Windows, ouvrez un **Terminal (administrateur)** et tapez : `reg add "HKLM\System\CurrentControlSet\Control\TimeZoneInformation" /v RealTimeIsUniversal /d 1 /t REG_DWORD /f` |
| Le PC est très lent depuis la clé | Normal avec une clé USB 2.0 : l'installation sera rapide une fois sur le disque. Ou choisissez « Live chargé en RAM ». |

Toujours bloqué ? Ouvrez une demande sur
[github.com/hashcat-max/IXIN-GROUP/issues](https://github.com/hashcat-max/IXIN-GROUP/issues) en indiquant la marque
et le modèle du PC, et l'étape où cela bloque (une photo de l'écran aide beaucoup).

---

## 10. Compatibilité

| | |
|---|---|
| ✅ PC de bureau et portables **Intel / AMD 64 bits**, démarrage **UEFI** ou **BIOS** | Cartes graphiques Intel, AMD et NVIDIA (pilote NVIDIA pour les GeForce GTX 16xx / RTX et plus récentes, pilote libre pour les plus anciennes) |
| ✅ Machines virtuelles | VirtualBox, VMware, QEMU / KVM, Hyper-V (outils invités inclus) |
| ✅ Imprimantes et scanners | La plupart sont reconnus automatiquement (USB ou réseau), pilotes HP et Gutenprint inclus |
| ⚠️ Mac à processeur Intel, Chromebooks Intel / AMD (firmware MrChromebox) | Possibles, non testés |
| ⚠️ Secure Boot | À désactiver pour l'instant |
| ❌ Mac Apple Silicon (M1 à M4), PC à processeur ARM (Snapdragon), tablettes et téléphones | Non pris en charge |

IXIN OS est en **version bêta** : il a été testé sur machines virtuelles et machines de test, pas encore sur un
grand nombre de PC réels. Vos retours sont précieux.
