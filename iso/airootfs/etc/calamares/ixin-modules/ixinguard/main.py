#!/usr/bin/env python3
# IXIN OS — garde-fou de l'installation lancée depuis Windows (IXIN OS Setup)
#
# Exécuté juste avant le partitionnement : si le système live a été démarré par
# IXIN OS Setup (paramètre noyau ixin.setup=windows), l'espace destiné à IXIN OS a déjà
# été libéré par Windows. Effacer le disque ou réduire de nouveau Windows (souvent chiffré
# par BitLocker) est alors refusé, AVANT toute modification du disque.
import libcalamares


def pretty_name():
    return "Vérification de l'installation à côté de Windows"


def from_windows():
    try:
        with open("/proc/cmdline", encoding="utf-8") as f:
            return "ixin.setup=windows" in f.read().split()
    except OSError:
        return False


def run():
    if not from_windows():
        return None
    choice = (libcalamares.globalstorage.value("partitionChoices") or {}).get("install", "")
    libcalamares.utils.debug(f"ixinguard: installation depuis Windows, choix « {choice} »")
    if choice in ("replace", "manual"):
        return None
    return ("Installation arrêtée : Windows est protégé",
            "IXIN OS Setup a déjà préparé un espace libre pour IXIN OS sur ce disque. "
            "Relancez l'installation et, à l'étape « Partitions », choisissez "
            "« Remplacer une partition », puis cliquez sur la zone « Espace libre ».<br/>"
            "Rien n'a été modifié sur le disque.")
