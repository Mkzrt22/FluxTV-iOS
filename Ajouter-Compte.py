#!/usr/bin/env python3
"""
FluxTV — ajoute ton compte Xtream dans un .ipa : l'app s'y connecte toute seule.

    python3 Ajouter-Compte.py FluxTV-iPhone.ipa

Le serveur, le nom d'utilisateur et le mot de passe sont demandés (le mot de passe ne
s'affiche pas), ou lus dans FLUXTV_SERVEUR, FLUXTV_UTILISATEUR et FLUXTV_MOTDEPASSE.
Résultat : FluxTV-iPhone-perso.ipa, à installer avec Sideloadly.

Copie PERSONNELLE : quiconque a ce fichier peut y lire le compte. Ne la partage pas, ne
l'envoie pas sur GitHub.
"""
import base64
import getpass
import json
import os
import sys
import zipfile

FICHIER = "compte.flux"


def demander(variable, question, secret=False):
    valeur = os.environ.get(variable, "").strip()
    if valeur:
        return valeur
    return (getpass.getpass if secret else input)(question).strip()


def ajouter_compte(source, compte):
    """Copie `source` en « …-perso.ipa » avec le compte dans l'app ; renvoie le chemin créé."""
    if not all(compte.get(k) for k in ("serveur", "utilisateur", "motdepasse")):
        raise ValueError("compte incomplet")
    contenu = base64.b64encode(json.dumps(compte).encode()).decode()
    sortie = source[:-4] + "-perso.ipa"
    with zipfile.ZipFile(source) as zin:
        apps = {n.split("/")[1] for n in zin.namelist()
                if n.startswith("Payload/") and n.count("/") >= 2 and n.split("/")[1].endswith(".app")}
        if len(apps) != 1:
            raise ValueError("ce fichier n'est pas un .ipa de FluxTV")
        cible = f"Payload/{apps.pop()}/{FICHIER}"
        with zipfile.ZipFile(sortie, "w", zipfile.ZIP_DEFLATED) as zout:
            for info in zin.infolist():
                if info.filename != cible:
                    # Les droits des fichiers (exécutable de l'app) sont conservés
                    zout.writestr(info, zin.read(info.filename))
            ajout = zipfile.ZipInfo(cible, date_time=(2026, 1, 1, 0, 0, 0))
            ajout.external_attr = 0o644 << 16
            ajout.compress_type = zipfile.ZIP_DEFLATED
            zout.writestr(ajout, contenu)
    return sortie


def main():
    if len(sys.argv) != 2 or not sys.argv[1].endswith(".ipa"):
        sys.exit("Utilisation : python3 Ajouter-Compte.py FluxTV-iPhone.ipa")
    compte = {
        "serveur": demander("FLUXTV_SERVEUR", "Serveur (http://…:port) : "),
        "utilisateur": demander("FLUXTV_UTILISATEUR", "Nom d'utilisateur : "),
        "motdepasse": demander("FLUXTV_MOTDEPASSE", "Mot de passe : ", secret=True),
    }
    try:
        sortie = ajouter_compte(sys.argv[1], compte)
    except (OSError, ValueError, zipfile.BadZipFile) as e:
        sys.exit(f"✘ Rien n'a été modifié : {e}")
    print(f"✔ {sortie} : FluxTV se connectera tout seul à ce compte. Ne partage pas ce fichier.")


if __name__ == "__main__":
    main()
