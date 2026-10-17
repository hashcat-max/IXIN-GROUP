#!/usr/bin/env python3
"""IXIN OS — flux de données du site os.ixin.online (publié sur GitHub Pages).

    tools/prepare-site-feed.py <site>

Écrit dans <site>/site/ :
  ixin-os.json   tout ce que le site affiche et qui change avec les versions : version actuelle,
                 téléchargements (taille, SHA-256, liens), nouveautés, guides, captures d'écran
  release.json   copie de la dernière version publiée au format de l'API de GitHub : IXIN OS Setup
                 l'utilise si l'API refuse la demande (limite de 60 demandes par heure et par adresse)
  docs/          les guides (Markdown) et screenshots/ les captures d'écran

Le site lit ces fichiers depuis https://hashcat-max.github.io/IXIN-GROUP/site/ : chaque version
publiée et chaque modification de la documentation y apparaissent sans autre intervention.
"""
import datetime
import json
import os
import shutil
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPOSITORY = os.environ.get("GITHUB_REPOSITORY", "hashcat-max/IXIN-GROUP")
PAGES = "https://{}.github.io/{}".format(*REPOSITORY.split("/", 1))
RAW = f"https://raw.githubusercontent.com/{REPOSITORY}/HEAD"
GUIDES = [
    ("INSTALLATION-WINDOWS.md", "Installer IXIN OS depuis Windows (sans clé USB)"),
    ("INSTALLATION-CLE-USB.md", "Installer IXIN OS avec une clé USB (guide pour débutants)"),
    ("MISES-A-JOUR.md", "Mises à jour d'IXIN OS (IXIN Update)"),
    ("CONTEXTE-IXIN-OS.md", "Présentation complète d'IXIN OS (fiche de référence)"),
]


def get(url, accept=None):
    req = urllib.request.Request(url, headers={"User-Agent": "ixin-site-feed"})
    if accept:
        req.add_header("Accept", accept)
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token and url.startswith("https://api.github.com/"):
        req.add_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read()


def main(site):
    out = os.path.join(site, "site")
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(os.path.join(out, "docs"))

    release = json.loads(get(f"https://api.github.com/repos/{REPOSITORY}/releases/latest",
                             "application/vnd.github+json"))
    assets = [{
        "name": a["name"],
        "size": a["size"],
        "browser_download_url": a["browser_download_url"],
        "digest": a.get("digest"),
    } for a in release.get("assets", [])]
    trimmed = {
        "tag_name": release["tag_name"],
        "name": release.get("name"),
        "published_at": release.get("published_at"),
        "html_url": release.get("html_url"),
        "assets": assets,
    }
    with open(os.path.join(out, "release.json"), "w", encoding="utf-8") as f:
        json.dump(trimmed, f, ensure_ascii=False, indent=1)

    def sha(a):
        d = a.get("digest") or ""
        return d.split(":", 1)[1] if d.startswith("sha256:") else None

    by_name = {a["name"]: a for a in assets}
    setup = by_name.get("IXIN-OS-Setup.exe")
    parts = sorted((a for a in assets if ".iso.part" in a["name"]), key=lambda a: a["name"])
    sums = [a for a in assets if a["name"].endswith(".iso.sha256")]
    iso = None
    if parts and sums:
        text = get(sums[0]["browser_download_url"]).decode()
        iso_sha = text.split()[0] if text.strip() else None
        iso = {
            "name": sums[0]["name"][: -len(".sha256")],
            "size": sum(a["size"] for a in parts),
            "sha256": iso_sha,
            "sha256_url": sums[0]["browser_download_url"],
            "parts": [{"name": a["name"], "size": a["size"], "url": a["browser_download_url"]} for a in parts],
        }

    with open(os.path.join(ROOT, "docs", "site", "nouveautes.json"), encoding="utf-8") as f:
        news = json.load(f)

    docs = []
    for name, title in GUIDES:
        src = os.path.join(ROOT, "docs", name)
        if not os.path.exists(src):
            continue
        shutil.copy(src, os.path.join(out, "docs", name))
        docs.append({"title": title, "file": name, "url": f"{PAGES}/site/docs/{name}",
                     "github": f"https://github.com/{REPOSITORY}/blob/HEAD/docs/{name}"})
    shots = os.path.join(ROOT, "docs", "screenshots")
    shutil.copytree(shots, os.path.join(out, "screenshots"))

    feed = {
        "format": 1,
        "generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "status": "beta",
        "version": {
            "tag": release["tag_name"],
            "date": (release.get("published_at") or "")[:10],
            "url": release.get("html_url"),
            "all_versions": f"https://github.com/{REPOSITORY}/releases",
        },
        "downloads": {
            "setup": setup and {
                "name": setup["name"],
                "size": setup["size"],
                "sha256": sha(setup),
                "url": f"https://github.com/{REPOSITORY}/releases/latest/download/IXIN-OS-Setup.exe",
                "for": "Windows 10 / 11 (64 bits) : installation à côté de Windows, sans clé USB",
            },
            "iso": iso,
        },
        "requirements": {
            "cpu": "PC 64 bits Intel ou AMD (x86_64)",
            "ram_gb": 4, "ram_recommended_gb": 8,
            "disk_gb": 40, "disk_recommended_gb": 80,
            "boot": "UEFI (recommandé) ou BIOS",
            "secure_boot": "à désactiver",
        },
        "news": news,
        "docs": docs,
        "screenshots": {
            "base_url": f"{PAGES}/site/screenshots/",
            "files": sorted(os.listdir(shots)),
        },
        "update_policy_url": f"{PAGES}/repo/policy.json",
        "signing_key_fingerprint": "74DF FA0C 6B29 1E07 27EA  8A59 891C 90DD 6094 ABDF",
        "source": f"https://github.com/{REPOSITORY}",
    }
    with open(os.path.join(out, "ixin-os.json"), "w", encoding="utf-8") as f:
        json.dump(feed, f, ensure_ascii=False, indent=1)
    print(f"flux du site : {release['tag_name']}, {len(assets)} fichiers, {len(news)} nouveautés, "
          f"{len(docs)} guides, {len(feed['screenshots']['files'])} captures")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage : prepare-site-feed.py <site>")
    main(sys.argv[1])
