# Site os.ixin.online : mises à jour automatiques

Le site **os.ixin.online** (projet séparé, hébergé chez Cloudflare) affiche des informations qui changent
à chaque version d'IXIN OS : numéro de version, tailles et empreintes des fichiers, nouveautés, guides,
captures d'écran. Pour qu'il n'y ait **jamais à recopier ces informations à la main**, ce dépôt publie un
**flux de données** que le site lit tout seul.

## Le flux : `site/ixin-os.json`

Publié sur GitHub Pages par l'action « Publier les mises à jour IXIN » (`tools/prepare-site-feed.py`) :
- après **chaque version** publiée (l'action « Construire l'ISO » l'appelle à la fin) ;
- à **chaque modification** de `docs/` (guides, captures, nouveautés) ;
- chaque lundi (nouvelle signature de la politique des mises à jour).

| Adresse | Contenu |
|---|---|
| `https://hashcat-max.github.io/IXIN-GROUP/site/ixin-os.json` | le flux (format ci-dessous) |
| `https://hashcat-max.github.io/IXIN-GROUP/site/docs/<guide>.md` | les guides en Markdown |
| `https://hashcat-max.github.io/IXIN-GROUP/site/screenshots/<fichier>.png` | les captures d'écran |
| `https://hashcat-max.github.io/IXIN-GROUP/site/release.json` | la dernière version au format de l'API GitHub (utilisée par IXIN OS Setup en secours) |

GitHub Pages répond avec `Access-Control-Allow-Origin: *` et un cache de 10 minutes. La politique de
sécurité du site (`connect-src 'self'`) interdit au navigateur de lire une autre adresse : c'est le
**Worker du site, côté serveur**, qui lit le flux (avec son propre cache) et l'insère dans les pages.

### Format (`"format": 1`)

```jsonc
{
  "format": 1,
  "generated": "2026-10-17T16:20:23Z",          // date de publication du flux
  "status": "beta",
  "version": { "tag": "build-2026.10.17-5", "date": "2026-10-17",
               "url": "https://github.com/…/releases/tag/build-2026.10.17-5",
               "all_versions": "https://github.com/…/releases" },
  "downloads": {
    "setup": { "name": "IXIN-OS-Setup.exe", "size": 3774976, "sha256": "7dfc…",
               "url": "https://github.com/…/releases/latest/download/IXIN-OS-Setup.exe", "for": "…" },
    "iso":   { "name": "ixin-os-2026.10.17-x86_64.iso", "size": 5063438336, "sha256": "c46a…",
               "sha256_url": "…", "parts": [ { "name": "….iso.part00", "size": 1992294400, "url": "…" }, … ] }
  },
  "requirements": { "cpu": "…", "ram_gb": 4, "ram_recommended_gb": 8, "disk_gb": 40, … },
  "news": [ { "tag": "build-2026.10.17-5", "date": "2026-10-17", "title": "…", "items": [ "…", … ] }, … ],
  "docs": [ { "title": "…", "file": "INSTALLATION-WINDOWS.md", "url": "…", "github": "…" }, … ],
  "screenshots": { "base_url": "https://…/site/screenshots/", "files": [ "bureau.png", … ] },
  "update_policy_url": "https://hashcat-max.github.io/IXIN-GROUP/repo/policy.json",
  "signing_key_fingerprint": "74DF FA0C …",
  "source": "https://github.com/hashcat-max/IXIN-GROUP"
}
```

Règles pour le site : un champ peut être `null` (par exemple `downloads.iso` pendant la publication d'une
version) — garder alors l'affichage précédent ; un champ inconnu est ignoré ; `format` ne change que si la
structure change de façon incompatible. Les nouveautés (`news`, la plus récente en premier) viennent de
`docs/site/nouveautes.json`, mis à jour avec chaque version.

## Prévenir le site tout de suite (Cloudflare, facultatif)

Sans rien configurer, le site voit les changements dès que son cache expire. Pour qu'il les affiche
**immédiatement**, l'action peut vider le cache Cloudflare et/ou relancer le déploiement du site. Il suffit
d'ajouter des **secrets** au dépôt GitHub (**Settings → Secrets and variables → Actions → New repository
secret**) — **ne jamais** coller une clé dans une conversation, un fichier ou un ticket :

| Secret | Ce que c'est | Où le trouver |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | jeton API **limité** | Cloudflare → My Profile → API Tokens → **Create Token** → modèle « Custom » : permission **Zone → Cache Purge → Purge**, ressource **Zone → ixin.online** uniquement, durée de validité conseillée |
| `CLOUDFLARE_ZONE_ID` | identifiant de la zone `ixin.online` | Cloudflare → ixin.online → Overview → colonne de droite « Zone ID » |
| `CLOUDFLARE_DEPLOY_HOOK` | (si le site est un projet Cloudflare Pages relié à Git) adresse de déclenchement | Workers & Pages → le projet → Settings → Builds → **Deploy hooks** |

Le jeton n'a que le droit de vider le cache d'`ixin.online` : il ne peut ni modifier le site, ni lire
d'autres données du compte. L'action « Publier les mises à jour IXIN » (job « site ») indique dans son journal
ce qu'elle a fait.
