"""
Captures App Store de FluxTV : titre au-dessus de chaque capture (iPhone, iPad), aux tailles
exactes demandées par Apple, sans transparence. Les captures Apple TV restent brutes.

    python3 montage.py <dossier des captures brutes> <dossier de sortie>

Captures brutes : branche « ci-appstore », dossier « captures » (workflow « App Store -
préparation »). Sortie : « francais/ » et « english/ », fichiers numérotés dans l'ordre de la fiche.
Polices de l'app (Sora, Manrope), couleurs de l'app (Theme.swift).
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ICI = os.path.dirname(os.path.abspath(__file__))
POLICES = os.path.join(ICI, "..", "..", "App", "Resources", "Fonts")

ABYSS = (4, 4, 13)
BG = (8, 8, 24)
ACCENT = (91, 92, 240)
ACCENT_BRIGHT = (142, 151, 255)
VIOLET = (155, 77, 235)
TEXTE = (241, 242, 255)
TEXTE_DOUX = (166, 171, 210)

# iPhone : « écran moyen » (6,3 pouces, obligatoire depuis l'automne 2026) et « grand écran »
# (6,9 pouces, facultatif mais évite de devoir fournir la taille 6,5 pouces). Même mise en page.
TAILLES = {"iphone-moyen": (1206, 2622), "iphone": (1320, 2868), "ipad": (2064, 2752), "tv": (1920, 1080)}
# Arrondi des coins de l'écran, en part de sa largeur
RAYON = {"iphone-moyen": 0.11, "iphone": 0.11, "ipad": 0.045}
# Captures brutes utilisées pour chaque taille (l'écran moyen reprend celles de l'iPhone)
SOURCE = {"iphone-moyen": "iphone", "iphone": "iphone", "ipad": "ipad", "tv": "tv"}
# Dossiers du zip : un par emplacement d'App Store Connect, dans l'ordre où les remplir
EMPLACEMENTS = [
    ("iphone-moyen", "1-iPhone-ecran-moyen-1206x2622-obligatoire", "iPhone"),
    ("iphone", "2-iPhone-grand-ecran-1320x2868", "iPhone"),
    ("ipad", "3-iPad-13-pouces-2064x2752", "iPad"),
]
DOSSIER_TV = "4-AppleTV-1920x1080"

# Titre et sous-titre de chaque écran ; « puces » : formats affichés sous le lecteur
TEXTES = {
    "fr": {
        "accueil": ("Toute votre TV,\nau même endroit", "Chaînes, films et séries de votre abonnement"),
        "lecture-film": ("Un lecteur complet", "Formats, pistes audio et sous-titres"),
        "direct": ("La TV en direct,\navec le programme", "Guide XMLTV, favoris, zapping rapide"),
        "fiche-film": ("Reprenez là où\nvous en étiez", "Reprise automatique des films et des épisodes"),
        "fiche-serie": ("Saisons et épisodes\nenchaînés", "L'épisode suivant démarre tout seul"),
        "bienvenue": ("Ajoutez votre source\nen un instant", "Compte Xtream ou playlist M3U · aucune chaîne fournie"),
    },
    "en": {
        "accueil": ("All your TV,\nin one place", "Live channels, movies and series from your subscription"),
        "lecture-film": ("A complete player", "Formats, audio tracks and subtitles"),
        "direct": ("Live TV with\nwhat's on now", "XMLTV guide, favorites, fast zapping"),
        "fiche-film": ("Pick up where\nyou left off", "Movies and episodes resume automatically"),
        "fiche-serie": ("Seasons and episodes,\nback to back", "The next episode starts on its own"),
        "bienvenue": ("Add your source\nin seconds", "Xtream account or M3U playlist · no channels included"),
    },
}
# Ordre de la fiche (les premières comptent le plus). Sur iPad, le lecteur reste vertical : plus loin.
ORDRE = {
    "iphone-moyen": ["accueil", "lecture-film", "direct", "fiche-film", "fiche-serie", "bienvenue"],
    "iphone": ["accueil", "lecture-film", "direct", "fiche-film", "fiche-serie", "bienvenue"],
    "ipad": ["accueil", "direct", "fiche-film", "lecture-film", "fiche-serie", "bienvenue"],
}
PUCES = ["HLS", "MPEG-TS", "MP4", "MKV", "AVI", "AC3", "E-AC3", "DTS", "Sous-titres"]
PUCES_EN = ["HLS", "MPEG-TS", "MP4", "MKV", "AVI", "AC3", "E-AC3", "DTS", "Subtitles"]
# Apple TV : captures brutes, dans cet ordre
ECRANS_TV = ["accueil", "direct", "lecture-film", "fiche-film", "fiche-serie"]
# Plusieurs instants du film (captures -1, -2, -3) : celui retenu, par appareil et par langue
INSTANT = {"iphone": {"fr": 2, "en": 2}, "ipad": {"fr": 1, "en": 2}, "tv": {"fr": 3, "en": 3}}


def police(nom, taille):
    return ImageFont.truetype(os.path.join(POLICES, nom), taille)


def fond(w, h):
    """Dégradé sombre de l'app, avec deux halos (indigo et violet)."""
    img = Image.new("RGB", (w, h), BG)
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(BG[i] * (1 - t) + ABYSS[i] * t) for i in range(3))
        d.line([(0, y), (w, y)], fill=c)
    halo = Image.new("RGB", (w, h), (0, 0, 0))
    hd = ImageDraw.Draw(halo)
    r = int(max(w, h) * 0.42)
    hd.ellipse([int(w * 0.1) - r, int(h * 0.08) - r, int(w * 0.1) + r, int(h * 0.08) + r], fill=(46, 40, 140))
    hd.ellipse([int(w * 0.95) - r, int(h * 0.9) - r, int(w * 0.95) + r, int(h * 0.9) + r], fill=(70, 30, 110))
    halo = halo.filter(ImageFilter.GaussianBlur(int(r * 0.55)))
    return Image.blend(img, Image.composite(halo, img, Image.new("L", (w, h), 255)), 0.55)


def arrondi(img, rayon):
    masque = Image.new("L", img.size, 0)
    ImageDraw.Draw(masque).rounded_rectangle([0, 0, img.width - 1, img.height - 1], rayon, fill=255)
    return masque


def appareil(capture, largeur, rayon_rel):
    """Capture dans un cadre sombre arrondi, avec une ombre ; renvoie (image RGBA, marge d'ombre)."""
    k = largeur / capture.width
    ecran = capture.resize((largeur, round(capture.height * k)), Image.LANCZOS)
    bord = max(10, round(largeur * 0.016))
    rayon = round(largeur * rayon_rel)
    w, h = ecran.width + 2 * bord, ecran.height + 2 * bord
    ombre_marge = round(largeur * 0.08)
    out = Image.new("RGBA", (w + 2 * ombre_marge, h + 2 * ombre_marge), (0, 0, 0, 0))
    ombre = Image.new("L", out.size, 0)
    ImageDraw.Draw(ombre).rounded_rectangle([ombre_marge, ombre_marge + round(ombre_marge * 0.35),
                                             ombre_marge + w, ombre_marge + h + round(ombre_marge * 0.35)],
                                            rayon + bord, fill=150)
    ombre = ombre.filter(ImageFilter.GaussianBlur(ombre_marge * 0.45))
    out.paste((0, 0, 0, 255), (0, 0), ombre)
    cadre = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cd = ImageDraw.Draw(cadre)
    cd.rounded_rectangle([0, 0, w - 1, h - 1], rayon + bord, fill=(22, 22, 44, 255), outline=(70, 72, 120, 255),
                         width=max(2, bord // 5))
    cadre.paste(ecran, (bord, bord), arrondi(ecran, rayon))
    out.alpha_composite(cadre, (ombre_marge, ombre_marge))
    return out, ombre_marge


def texte_centre(d, w, y, texte, f, couleur, interligne=1.12):
    lignes = texte.split("\n")
    hauteur = f.size * interligne
    for i, l in enumerate(lignes):
        lw = d.textlength(l, font=f)
        d.text(((w - lw) / 2, y + i * hauteur), l, font=f, fill=couleur)
    return y + len(lignes) * hauteur


def ajuster(d, texte, nom, taille, largeur_max):
    """Plus grande police (jusqu'à « taille ») où chaque ligne tient dans la largeur."""
    while taille > 20:
        f = police(nom, taille)
        if all(d.textlength(l, font=f) <= largeur_max for l in texte.split("\n")):
            return f
        taille -= 2
    return police(nom, taille)


def puces(d, w, y, mots, f, marge):
    """Formats en pastilles centrées, sur une ou plusieurs lignes."""
    pad_x, pad_y, ecart = round(f.size * 0.8), round(f.size * 0.45), round(f.size * 0.45)
    lignes, ligne, lw = [], [], 0
    for m in mots:
        mw = d.textlength(m, font=f) + 2 * pad_x
        if ligne and lw + ecart + mw > w - 2 * marge:
            lignes.append((ligne, lw)); ligne, lw = [], 0
        ligne.append((m, mw)); lw += (ecart if len(ligne) > 1 else 0) + mw
    if ligne:
        lignes.append((ligne, lw))
    h = f.size + 2 * pad_y
    for ligne, lw in lignes:
        x = (w - lw) / 2
        for m, mw in ligne:
            d.rounded_rectangle([x, y, x + mw, y + h], h / 2, fill=(24, 24, 58), outline=(91, 92, 240), width=3)
            d.text((x + pad_x, y + pad_y - f.size * 0.08), m, font=f, fill=TEXTE)
            x += mw + ecart
        y += h + ecart
    return y


def composer(capture, appareil_nom, titre, sous_titre, puces_mots=None):
    rayon = RAYON[appareil_nom]
    W, H = TAILLES[appareil_nom]
    img = fond(W, H).convert("RGBA")
    d = ImageDraw.Draw(img)
    portrait = H > W
    marge = round(W * (0.075 if portrait else 0.06))
    y = round(H * (0.055 if portrait else 0.05))
    f_titre = ajuster(d, titre, "Sora-Bold.ttf", round(W * (0.083 if portrait else 0.04)), W - 2 * marge)
    y = round(texte_centre(d, W, y, titre, f_titre, TEXTE, 1.1))
    y += round(f_titre.size * 0.32)
    f_sous = ajuster(d, sous_titre, "Manrope-SemiBold.ttf", round(W * (0.037 if portrait else 0.019)), W - 2 * marge)
    y = round(texte_centre(d, W, y, sous_titre, f_sous, ACCENT_BRIGHT))
    y += round(H * (0.03 if portrait else 0.035))
    paysage_dans_portrait = portrait and capture.width > capture.height
    if paysage_dans_portrait:
        # Lecteur à l'horizontale sur une page verticale : l'écran, puis les formats lus
        cadre, om = appareil(capture, W - 2 * round(W * 0.04), 0.045)
        f_p = police("Manrope-Bold.ttf", round(W * 0.046))
        # Hauteur des pastilles (dessinées à blanc sur une image jetable), pour centrer l'ensemble
        essai = ImageDraw.Draw(Image.new("RGB", (W, H)))
        h_puces = puces(essai, W, 0, puces_mots or PUCES, f_p, marge)
        ecart = round(H * 0.06)
        bloc = cadre.height - 2 * om + ecart + h_puces
        y_cadre = y + max(0, (H - round(H * 0.04) - y - bloc) // 2)
        img.alpha_composite(cadre, ((W - cadre.width) // 2, y_cadre - om))
        puces(d, W, y_cadre + cadre.height - 2 * om + ecart, puces_mots or PUCES, f_p, marge)
    else:
        bas = round(H * (0.035 if portrait else 0.045))
        dispo_h = H - y - bas
        # Largeur qui fait tenir l'appareil (cadre compris) dans la hauteur restante
        largeur = round(dispo_h / (capture.height / capture.width) / 1.0)
        largeur = min(largeur, W - 2 * marge)
        cadre, om = appareil(capture, largeur, rayon)
        while cadre.height - 2 * om > dispo_h:
            largeur -= 8
            cadre, om = appareil(capture, largeur, rayon)
        img.alpha_composite(cadre, ((W - cadre.width) // 2, y - om))
    return img.convert("RGB")


def charger(chemin, paysage=False):
    """Capture sans transparence. « paysage » : le simulateur range une app tournée à
    l'horizontale dans une image verticale, couchée ; on la redresse."""
    img = Image.open(chemin)
    if paysage and img.height > img.width:
        img = img.rotate(90, expand=True)
    if img.mode != "RGB":
        fond_noir = Image.new("RGB", img.size, (0, 0, 0))
        fond_noir.paste(img, mask=img.getchannel("A") if "A" in img.getbands() else None)
        img = fond_noir
    return img


def trouver(dossier, appareil_nom, langue, ecran):
    appareil_nom = SOURCE.get(appareil_nom, appareil_nom)
    base = os.path.join(dossier, f"{appareil_nom}-{langue}-{ecran}")
    for nom in (f"{base}.png", f"{base}-{INSTANT[appareil_nom][langue]}.png", f"{base}-1.png"):
        if os.path.exists(nom):
            return nom
    return None


def main(src, dst):
    for langue, dossier_l in (("fr", "francais"), ("en", "english")):
        for appareil_nom, dossier_e, prefixe in EMPLACEMENTS:
            out = os.path.join(dst, dossier_l, dossier_e)
            os.makedirs(out, exist_ok=True)
            for n, ecran in enumerate(ORDRE[appareil_nom], 1):
                titre, sous = TEXTES[langue][ecran]
                f = trouver(src, appareil_nom, langue, ecran)
                if not f:
                    print("  manque", appareil_nom, langue, ecran)
                    continue
                # iPhone : le lecteur a été capturé à l'horizontale
                paysage = appareil_nom.startswith("iphone") and ecran.startswith("lecture")
                img = composer(charger(f, paysage), appareil_nom, titre, sous, PUCES if langue == "fr" else PUCES_EN)
                assert img.size == TAILLES[appareil_nom] and img.mode == "RGB"
                img.save(os.path.join(out, f"{prefixe}-{n}-{ecran}.png"), optimize=True)
        out = os.path.join(dst, dossier_l, DOSSIER_TV)
        os.makedirs(out, exist_ok=True)
        for n, ecran in enumerate(ECRANS_TV, 1):
            f = trouver(src, "tv", langue, ecran)
            if not f:
                print("  manque tv", langue, ecran)
                continue
            img = charger(f)
            if img.size != TAILLES["tv"]:
                img = img.resize(TAILLES["tv"], Image.LANCZOS)
            img.save(os.path.join(out, f"AppleTV-{n}-{ecran}.png"), optimize=True)
        print(langue, ":", sum(len(f) for _, _, f in os.walk(os.path.join(dst, dossier_l))), "fichiers")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
