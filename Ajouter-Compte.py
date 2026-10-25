#!/usr/bin/env python3
"""
Copie personnelle de FluxTV pour Xbox : ajoute un compte Xtream au paquet .msix (fichier « compte.flux »),
puis signe le paquet avec le certificat de test (osslsigncode). L'app démarre alors directement sur ce compte.

    python3 Ajouter-Compte.py FluxTV.msix FluxTV-perso.msix

Identifiants : variables FLUXTV_SERVEUR, FLUXTV_UTILISATEUR, FLUXTV_MOTDEPASSE, sinon demandés au clavier.
Le paquet obtenu contient tes identifiants : ne le partage pas.

Le paquet d'origine n'est pas recompressé : ses fichiers sont recopiés tels quels, seuls le plan des blocs
(AppxBlockMap.xml) et la liste des types ([Content_Types].xml) sont refaits pour inclure le nouveau fichier.
"""
import base64
import getpass
import hashlib
import json
import os
import struct
import subprocess
import sys
import tempfile
import zlib

COMPTE = b"compte.flux"
BLOC = 65536


def _entrees(data):
    """Entrées du zip, dans l'ordre du répertoire central, avec leurs octets bruts."""
    z64 = data.rfind(b"PK\x06\x06")
    if z64 >= 0:
        _, _, _, _, _, _, _, n, _, cdoff = struct.unpack("<IQHHIIQQQQ", data[z64:z64 + 56])
    else:
        e = data.rfind(b"PK\x05\x06")
        _, _, _, _, n, _, cdoff, _ = struct.unpack("<IHHHHIIH", data[e:e + 22])
    entrees, p = [], cdoff
    for _ in range(n):
        champs = struct.unpack("<IHHHHHHIIIHHHHHII", data[p:p + 46])
        if champs[0] != 0x02014B50:
            raise ValueError("répertoire central illisible")
        nl, el, cl, off = champs[10], champs[11], champs[12], champs[16]
        if el:
            raise ValueError("paquet non pris en charge (champ supplémentaire dans le répertoire central)")
        nom = data[p + 46:p + 46 + nl]
        entrees.append({"nom": nom, "central": bytearray(data[p:p + 46 + nl + el + cl]), "off": off,
                        "heure": champs[5], "date": champs[6]})
        p += 46 + nl + el + cl
    ordre = sorted(entrees, key=lambda e: e["off"])
    for a, b in zip(ordre, ordre[1:] + [None]):
        a["brut"] = data[a["off"]:(b["off"] if b else cdoff)]
    return entrees


def _enregistrement(nom, contenu, compresser, heure, date):
    """En-tête local + données (sans descripteur), et entrée du répertoire central (décalage à compléter)."""
    crc = zlib.crc32(contenu) & 0xFFFFFFFF
    if compresser:
        c = zlib.compressobj(9, zlib.DEFLATED, -15)
        donnees, methode = c.compress(contenu) + c.flush(), 8
    else:
        donnees, methode = contenu, 0
    local = struct.pack("<IHHHHHIIIHH", 0x04034B50, 20, 0, methode, heure, date, crc, len(donnees), len(contenu), len(nom), 0)
    central = bytearray(struct.pack("<IHHHHHHIIIHHHHHII", 0x02014B50, 0x2D, 20, 0, methode, heure, date, crc,
                                    len(donnees), len(contenu), len(nom), 0, 0, 0, 0, 0, 0) + nom)
    return local + nom + donnees, central


def _plan_du_compte(contenu):
    """Élément <File> du plan des blocs pour compte.flux (fichier non compressé)."""
    blocs = "".join('<Block Hash="%s"/>' % base64.b64encode(hashlib.sha256(contenu[i:i + BLOC]).digest()).decode()
                    for i in range(0, max(len(contenu), 1), BLOC))
    total = base64.b64encode(hashlib.sha256(contenu).digest()).decode()
    return '<File Name="%s" Size="%d" LfhSize="%d">%s<b4:FileHash Hash="%s"/></File>' % (
        COMPTE.decode(), len(contenu), 30 + len(COMPTE), blocs, total)


def assembler(msix, compte):
    """Nouveau paquet (non signé) avec compte.flux ; renvoie ses octets."""
    entrees = _entrees(msix)
    noms = [e["nom"] for e in entrees]
    if COMPTE in noms:
        raise ValueError("ce paquet contient déjà un compte")
    if b"AppxManifest.xml" not in noms or b"AppxBlockMap.xml" not in noms:
        raise ValueError("ce fichier n'est pas un paquet d'app Windows")

    import zipfile
    import io
    z = zipfile.ZipFile(io.BytesIO(msix))
    plan = z.read("AppxBlockMap.xml").decode("utf-8")
    types = z.read("[Content_Types].xml").decode("utf-8")
    if 'xmlns:b4=' not in plan:
        plan = plan.replace('<BlockMap ', '<BlockMap xmlns:b4="http://schemas.microsoft.com/appx/2021/blockmap" ', 1)
        if "IgnorableNamespaces=" not in plan:
            plan = plan.replace('<BlockMap ', '<BlockMap IgnorableNamespaces="b4" ', 1)
    repere = '<File Name="AppxManifest.xml"'
    if plan.count(repere) != 1:
        raise ValueError("plan des blocs inattendu")
    plan = plan.replace(repere, _plan_du_compte(compte) + repere, 1)
    if 'Extension="flux"' not in types:
        debut = types.index(">", types.index("<Types")) + 1
        types = types[:debut] + '<Default Extension="flux" ContentType="application/octet-stream"/>' + types[debut:]

    modele = next(e for e in entrees if e["nom"] == b"AppxManifest.xml")
    heure, date = modele["heure"], modele["date"]
    sortie, centraux = bytearray(), []

    def ajouter(brut, central):
        struct.pack_into("<I", central, 42, len(sortie))
        sortie.extend(brut)
        centraux.append(bytes(central))

    for e in entrees:
        nom = e["nom"]
        if nom == b"AppxSignature.p7x":
            continue
        if nom == b"AppxManifest.xml":
            ajouter(*_enregistrement(COMPTE, compte, False, heure, date))
        if nom == b"AppxBlockMap.xml":
            ajouter(*_enregistrement(nom, plan.encode("utf-8"), True, e["heure"], e["date"]))
        elif nom == b"[Content_Types].xml":
            ajouter(*_enregistrement(nom, types.encode("utf-8"), True, e["heure"], e["date"]))
        else:
            ajouter(e["brut"], e["central"])

    debut_central = len(sortie)
    for c in centraux:
        sortie.extend(c)
    taille_central = len(sortie) - debut_central
    debut_z64 = len(sortie)
    n = len(centraux)
    sortie.extend(struct.pack("<IQHHIIQQQQ", 0x06064B50, 44, 45, 45, 0, 0, n, n, taille_central, debut_central))
    sortie.extend(struct.pack("<IIQI", 0x07064B50, 0, debut_z64, 1))
    sortie.extend(struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, 0xFFFF, 0xFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0))
    return bytes(sortie)


def contenu_compte(serveur, utilisateur, motdepasse):
    serveur = serveur.strip()
    if not serveur.lower().startswith(("http://", "https://")):
        serveur = "http://" + serveur
    donnees = json.dumps({"serveur": serveur, "utilisateur": utilisateur.strip(), "motdepasse": motdepasse.strip()},
                         ensure_ascii=False).encode("utf-8")
    return base64.b64encode(donnees) + b"\n"


def ajouter_compte(entree, sortie, serveur, utilisateur, motdepasse, pfx, mot_de_passe_pfx="fluxtv"):
    with open(entree, "rb") as f:
        msix = f.read()
    paquet = assembler(msix, contenu_compte(serveur, utilisateur, motdepasse))
    with tempfile.TemporaryDirectory() as tmp:
        brut = os.path.join(tmp, "non-signe.msix")
        with open(brut, "wb") as f:
            f.write(paquet)
        if os.path.exists(sortie):
            os.remove(sortie)
        r = subprocess.run(["osslsigncode", "sign", "-pkcs12", pfx, "-pass", mot_de_passe_pfx, "-h", "sha256",
                            "-in", brut, "-out", sortie], capture_output=True, text=True)
        if r.returncode != 0:
            raise RuntimeError("signature impossible : " + (r.stderr or r.stdout).strip())


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    ici = os.path.dirname(os.path.abspath(__file__))
    serveur = os.environ.get("FLUXTV_SERVEUR") or input("Serveur (http://hote:port) : ")
    utilisateur = os.environ.get("FLUXTV_UTILISATEUR") or input("Identifiant : ")
    motdepasse = os.environ.get("FLUXTV_MOTDEPASSE") or getpass.getpass("Mot de passe : ")
    ajouter_compte(sys.argv[1], sys.argv[2], serveur, utilisateur, motdepasse, os.path.join(ici, "Signature", "FluxTV-test.pfx"))
    print("Paquet personnel prêt : " + sys.argv[2] + " (ne le partage pas : il contient tes identifiants)")


if __name__ == "__main__":
    main()
