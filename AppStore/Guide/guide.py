"""
Guide complet de mise en ligne de FluxTV sur l'App Store : une seule source pour deux sorties.

    python3 guide.py <page.html> <note.md>

- page.html : page interactive (cases à cocher, boutons « Copier », textes en français ou en anglais) ;
- note.md : la même chose pour le coffre Obsidian.
Les textes de la fiche sont lus dans ../Fiche-App-Store.md et ../Notes-validation.md (jamais recopiés à la main).
"""
import html
import json
import pathlib
import re
import sys

ICI = pathlib.Path(__file__).resolve().parent
FICHE = (ICI / ".." / "Fiche-App-Store.md").read_text(encoding="utf-8")
NOTES = (ICI / ".." / "Notes-validation.md").read_text(encoding="utf-8")


def entre(texte, debut, fin):
    i = texte.index(debut) + len(debut)
    return texte[i:texte.index(fin, i)]


def ligne_code(section, prefixe):
    m = re.search(re.escape(prefixe) + r"[^`]*`([^`]+)`", section)
    return m.group(1)


promo = entre(FICHE, "### Texte promotionnel", "### Mots-clés")
mots = entre(FICHE, "### Mots-clés", "### URL")
desc_fr = entre(FICHE, "### Description (français)\n```\n", "\n```")
desc_en = entre(FICHE, "### Description (English)\n```\n", "\n```")
notes = NOTES.split("\n---\n", 1)[1].strip()

SITE = "https://mkzrt22.github.io/FluxTV-iOS/"
# Champs à coller : libellé, limite d'Apple (caractères), texte français, texte anglais (None : le même)
CHAMPS = {
    "version": ("Version", None, "1.2", None),
    "nom": ("Nom", 30, "FluxTV", None),
    "sous-titre": ("Sous-titre", 30, "Lecteur IPTV pour vos listes", "IPTV player for your playlists"),
    "confidentialite": ("URL de la politique de confidentialité", None, SITE + "confidentialite/", SITE + "en/privacy/"),
    "promo": ("Texte promotionnel", 170, ligne_code(promo, "FR ("), ligne_code(promo, "EN (")),
    "description": ("Description", 4000, desc_fr, desc_en),
    "mots-cles": ("Mots-clés", 100, ligne_code(mots, "FR ("), ligne_code(mots, "EN (")),
    "assistance": ("URL de l'assistance", None, SITE + "assistance/", SITE + "en/help/"),
    "marketing": ("URL marketing", None, SITE, SITE + "en/"),
    "copyright": ("Copyright", None, "2026 FluxTV", None),
    "notes": ("Remarques (en anglais, pour les deux langues)", 4000, notes, None),
    "cle-nom": ("Nom de la clé", None, "GitHub FluxTV", None),
    "secret-id": ("Name du secret 1", None, "ASC_KEY_ID", None),
    "secret-emetteur": ("Name du secret 2", None, "ASC_ISSUER_ID", None),
    "secret-p8": ("Name du secret 3", None, "ASC_KEY_P8", None),
    "envoyer": ("Confirmation", None, "ENVOYER", None),
}

ASC = '<a href="https://appstoreconnect.apple.com/apps">App Store Connect</a>'
GH = "https://github.com/Mkzrt22/FluxTV-iOS"

# Étapes : (identifiant, titre, texte, champs à coller)
PHASES = [
    ("avant", "Avant de commencer", "Dix minutes pour tout avoir sous la main.", [
        ("zip", "Ouvre le dossier des captures",
         "Décompresse <code>FluxTV-AppStore-captures.zip</code> (dans ton coffre : <code>Sauvegardes/App Store</code>). "
         "Dans <code>francais</code> et dans <code>english</code>, un dossier par emplacement d'App Store Connect : "
         "<code>1-iPhone-ecran-moyen</code> (obligatoire), <code>2-iPhone-grand-ecran</code>, <code>3-iPad-13-pouces</code> et <code>4-AppleTV</code>. "
         "Les fichiers sont numérotés dans l'ordre où les déposer.", []),
        ("connexion", "Ouvre App Store Connect sur un ordinateur",
         f"{ASC} › <b>FluxTV</b>. Garde ce guide ouvert à côté, ou sur ton téléphone.", []),
        ("langue-guide", "Choisis la langue des textes en haut de ce guide",
         "<b>Français</b> pour la fiche française, puis <b>English</b> quand tu passes à la fiche anglaise. "
         "Chaque bouton « Copier » copie le texte de la langue choisie.", []),
    ]),
    ("infos", "Informations communes", "Valables pour l'iPhone, l'iPad et l'Apple TV.", [
        ("version", "Passe la version à 1.2",
         "Page <b>App iOS Version 1.0</b> : le champ « Version » est plus bas, sous « URL de l'assistance ». Remplace 1.0 par 1.2, puis "
         "<b>« Enregistrer »</b> en haut à droite : le titre devient « App iOS Version 1.2 ». Même chose sur la page <b>tvOS</b>. "
         "L'app envoyée porte le numéro 1.2 : sans ce changement, Apple ne la proposerait pas.", ["version"]),
        ("anglais", "Ajoute l'anglais",
         "Menu des langues en haut à droite de la page (il affiche « Français ») › <b>English (U.S.)</b>. "
         "Ce menu sert ensuite à passer d'une langue à l'autre pour chaque texte.", []),
        ("nom", "Nom et sous-titre",
         "Colonne de gauche : <b>Informations sur l'app</b>. À remplir en français, puis en anglais.", ["nom", "sous-titre"]),
        ("categories", "Catégories",
         "Même page. Catégorie principale : <b>Divertissement</b>. Catégorie secondaire : <b>Photo et vidéo</b>.", []),
        ("droits", "Droits sur le contenu",
         "Même page, « Droits sur le contenu » › « Configurer » › <b>Oui</b>, l'app contient, affiche ou donne accès à du contenu tiers › "
         "coche <b>J'ai les droits nécessaires</b>. La démo n'utilise que des films libres de la Blender Foundation ; le reste vient de l'utilisateur.", []),
        ("age", "Classification par âge",
         "Même page, « Classification par âge » › « Modifier ». Réponds pour l'app elle-même : <b>Non</b> ou <b>Aucun</b> partout "
         "(violence, contenu sexuel, langage, drogues, jeux d'argent, horreur, publicité, discussion, contenu des utilisateurs, accès web illimité, réseaux sociaux, médical), "
         "et <b>Oui</b> pour le contrôle parental (« Masquer les contenus pour adultes », activé par défaut). "
         "À la fin, si Apple propose une classification plus élevée, choisis <b>18+</b> : c'est ce qu'Apple attend d'un lecteur IPTV.", []),
        ("prix", "Prix et pays",
         "Colonne de gauche : <b>Tarifs et disponibilité</b>. Prix : <b>0 € (gratuit)</b>. Disponibilité : tous les pays, "
         "<b>sauf la Chine continentale</b> (Apple y exige une licence que FluxTV n'a pas). "
         "Si la page le propose, décoche aussi les Mac avec puce Apple et l'Apple Vision Pro : FluxTV n'y a pas été testé.", []),
        ("confidentialite", "Confidentialité de l'app",
         "Colonne de gauche : <b>Confidentialité de l'app</b>. Colle l'adresse de la politique (en français, puis en anglais). "
         "Puis « Pratiques de confidentialité » › « Commencer » › <b>Non, nous ne collectons pas de données</b> › « Enregistrer » › <b>« Publier »</b>. "
         "La fiche affichera « Données non collectées ».", ["confidentialite"]),
    ]),
    ("ios", "Page de la version iPhone et iPad", "Page « App iOS Version 1.2 », rubrique « Informations de la page produit ». En français d'abord, puis en anglais.", [
        ("ignorer-ios", "Ce que tu laisses vide",
         "<b>En-tête et résultats de recherche</b> (nouveauté 2026, facultatif), <b>iPhone Duo</b> (facultatif jusqu'en avril 2027), "
         "<b>Fichier de couverture géographique</b>, <b>Extrait d'app</b>, <b>App iMessage</b>, <b>Game Center</b> et <b>Pièce jointe</b> : rien à faire, ils ne concernent pas FluxTV.", []),
        ("captures-iphone", "Captures iPhone",
         "Trouve la rubrique des captures de l'app (« Aperçus et captures d'écran ») : si la page n'affiche que « En-tête et résultats de recherche », "
         "clique sur ce titre pour changer de rubrique. Onglet <b>iPhone avec Dynamic Island (écran moyen)</b> : glisse les 6 fichiers du dossier "
         "<code>1-iPhone-ecran-moyen</code>, dans l'ordre (obligatoire). Puis, si l'onglet existe, <b>iPhone avec Dynamic Island (grand écran)</b> : "
         "dossier <code>2-iPhone-grand-ecran</code> (conseillé : sinon Apple réclame une taille de plus). "
         "Introuvable ? Envoie une capture de la page à Claude.", []),
        ("captures-ipad", "Captures iPad",
         "Onglet <b>iPad, écran de 13 pouces</b> : les 6 fichiers du dossier <code>3-iPad-13-pouces</code>.", []),
        ("textes-ios", "Texte promotionnel, description et mots-clés",
         "Le texte promotionnel peut changer à tout moment, sans nouvelle version.", ["promo", "description", "mots-cles"]),
        ("url-ios", "Adresses et copyright", "", ["assistance", "marketing", "copyright"]),
        ("verification-ios", "Informations utiles à la vérification de l'app",
         "<b>Connexion requise</b> est cochée d'office : <b>décoche-la</b> (l'équipe d'Apple utilise le mode démo, nom d'utilisateur et mot de passe restent vides). "
         "Coordonnées : ton prénom, ton nom, ton téléphone et ton e-mail (Apple ne les publie pas ; ne les envoie à personne). "
         "<b>Remarques</b> : colle le texte ci-dessous, en anglais (le même dans les deux langues).", ["notes"]),
        ("publication-ios", "Publication de la version dans l'App Store",
         "Coche <b>Publier cette version manuellement</b> : c'est « automatiquement » qui est coché d'office. Tu choisiras ainsi le jour de la sortie.", []),
        ("enregistrer-ios", "Enregistre, puis passe à l'anglais",
         "« Enregistrer » en haut à droite. Menu des langues › English (U.S.), choisis <b>English</b> en haut de ce guide, "
         "et refais les captures (dossier <code>english</code>) et les textes.", []),
    ]),
    ("tvos", "Page de la version Apple TV", "Menu à côté du titre « App iOS Version 1.2 » (ou menu « Distribution ») › tvOS. Même présentation que la page iPhone, mêmes textes.", [
        ("captures-tv", "Captures Apple TV",
         "Rubrique des captures de l'app › Apple TV : les 5 fichiers du dossier <code>4-AppleTV</code>, dans l'ordre. "
         "« En-tête et résultats de recherche » : facultatif, à laisser vide.", []),
        ("textes-tv", "Textes, adresses et copyright",
         "Recolle le texte promotionnel, la description, les mots-clés, l'URL d'assistance, l'URL marketing et le copyright "
         "(boutons « Copier » de la page iPhone, juste au-dessus).", []),
        ("verification-tv", "Informations utiles à la vérification de l'app",
         "Comme pour l'iPhone : <b>Connexion requise</b> décochée, tes coordonnées, les mêmes remarques.", []),
        ("publication-tv", "Publication manuelle, puis l'anglais",
         "<b>Publier cette version manuellement</b> › « Enregistrer ». Puis la même chose en anglais.", []),
    ]),
    ("cle", "La clé d'accès Apple", "Elle permet à GitHub d'envoyer FluxTV à Apple. Une seule fois.", [
        ("acces-api", "Demande l'accès à l'API",
         f"{ASC} › <b>Utilisateurs et accès</b> › onglet <b>Intégrations</b> › <b>API App Store Connect</b> › « Demander l'accès » › accepte les conditions. "
         "Apple peut mettre un peu de temps à l'accorder.", []),
        ("generer", "Crée la clé",
         "« Clés d'équipe » › <b>Générer une clé API</b> (ou « + »). Accès : <b>Admin</b> (indispensable pour qu'Apple signe l'app) › « Générer ».", ["cle-nom"]),
        ("telecharger", "Télécharge le fichier .p8",
         "Bouton « Télécharger » à côté de la clé : <code>AuthKey_XXXXXXXXXX.p8</code>. Apple ne le propose <b>qu'une seule fois</b> : "
         "range-le dans <code>Sauvegardes</code> de ton coffre, qui est privé.", []),
        ("identifiants", "Repère les deux identifiants",
         "<b>ID de la clé</b> : 10 caractères, dans le tableau. <b>ID de l'émetteur</b> : en haut de la page, avec un bouton pour le copier. "
         "Ne les envoie à personne, Claude compris : tu les colles toi-même dans GitHub.", []),
    ]),
    ("github", "Les secrets GitHub", "GitHub les garde chiffrés et ne les réaffiche jamais.", [
        ("ouvrir-secrets", "Ouvre les secrets du dépôt",
         f'<a href="{GH}/settings/secrets/actions">Settings › Secrets and variables › Actions</a> du dépôt FluxTV-iOS.', []),
        ("secret-1", "Secret 1 : l'ID de la clé",
         "<b>New repository secret</b> › Name ci-dessous › Secret : l'ID de la clé › <b>Add secret</b>.", ["secret-id"]),
        ("secret-2", "Secret 2 : l'ID de l'émetteur",
         "<b>New repository secret</b> › Name ci-dessous › Secret : l'ID de l'émetteur › <b>Add secret</b>.", ["secret-emetteur"]),
        ("secret-3", "Secret 3 : le fichier .p8",
         "<b>New repository secret</b> › Name ci-dessous › Secret : ouvre le fichier .p8 avec le Bloc-notes, Ctrl+A puis Ctrl+C, "
         "et colle tout, y compris les lignes <code>-----BEGIN PRIVATE KEY-----</code> et <code>-----END PRIVATE KEY-----</code>.", ["secret-p8"]),
        ("verifier-secrets", "Vérifie la liste",
         "La page affiche <code>ASC_ISSUER_ID</code>, <code>ASC_KEY_ID</code> et <code>ASC_KEY_P8</code>. Le contenu n'est plus jamais visible, c'est normal.", []),
    ]),
    ("envoi", "L'envoi à Apple", "Depuis GitHub : pas besoin de Mac, Apple signe l'app lui-même.", [
        ("lancer", "Lance l'envoi",
         f'<a href="{GH}/actions/workflows/appstore-envoi.yml">Actions › App Store - envoi</a> › <b>Run workflow</b> › Branch : <b>main</b> › '
         "écris le mot ci-dessous dans la case de confirmation › Plateformes : <b>les-deux</b> › <b>Run workflow</b>.", ["envoyer"]),
        ("attendre", "Attends la fin, environ 30 minutes",
         "Coche verte : c'est envoyé. Croix rouge : rien n'est parti ; ouvre l'exécution, puis « envoi », copie les lignes en rouge "
         "et envoie-les à Claude (elles ne contiennent aucun secret).", []),
        ("courriel", "Attends l'e-mail d'Apple",
         "10 à 30 minutes plus tard, Apple écrit que le build a été traité. Les builds iPhone/iPad et Apple TV apparaissent dans l'onglet <b>TestFlight</b>.", []),
    ]),
    ("testflight", "L'essai sur tes appareils", "Avant de soumettre, vérifie que tout marche avec ton abonnement.", [
        ("app-testflight", "Installe TestFlight",
         "L'app <b>TestFlight</b> (gratuite, sur l'App Store) sur ton iPhone et sur ton Apple TV.", []),
        ("testeur", "Ajoute-toi comme testeur",
         f"{ASC} › FluxTV › onglet <b>TestFlight</b> › « Tests internes » › « + » › un groupe « Moi » › ajoute-toi, puis ajoute les builds iOS et tvOS au groupe.", []),
        ("installer", "Installe FluxTV",
         "Ouvre l'invitation reçue par e-mail, ou directement l'app TestFlight › <b>Installer</b>.", []),
        ("essayer", "Essaie tout",
         "Avec ton abonnement : direct, guide, films, séries, reprise, langue. Puis « Essayer la démo », comme le fera Apple. "
         "Un souci : décris-le à Claude avant de soumettre.", []),
    ]),
    ("soumission", "La soumission", "La dernière ligne droite.", [
        ("build-ios", "Choisis le build iPhone et iPad",
         "<b>iOS 1.2</b> › rubrique « Build » › « + » › choisis le build › « Terminé » › « Enregistrer ».", []),
        ("build-tv", "Choisis le build Apple TV",
         "Même chose sur <b>tvOS 1.2</b>.", []),
        ("ajouter", "Ajoute pour vérification",
         "Bouton <b>Ajouter pour vérification</b> sur chaque page (iOS, puis tvOS). S'il manque quelque chose, Apple le liste : complète, puis recommence.", []),
        ("dsa", "Statut de commerçant (Union européenne)",
         "Si Apple le demande : <b>Non commerçant</b> si FluxTV est un projet personnel sans revenus. "
         "« Commerçant » affiche ton adresse et ton téléphone sur la fiche, après une vérification parfois longue. C'est ta déclaration : choisis selon ta situation.", []),
        ("soumettre", "Soumets",
         "<b>Soumettre pour vérification</b>. Statut « En attente de vérification », puis « En cours de vérification ». Réponse en général sous 1 à 3 jours, par e-mail.", []),
    ]),
    ("apres", "Après la réponse d'Apple", "", [
        ("accepte", "Si c'est accepté",
         "Statut « En attente de publication par le développeur » › <b>Publier cette version</b>. FluxTV arrive sur l'App Store en quelques heures. "
         "Envoie le lien de la fiche à Claude : il met à jour les boutons du site, avec ton accord.", []),
        ("refuse", "Si c'est refusé",
         "Ouvre le message d'Apple (« Centre de résolution » de l'app), copie-le sans tes coordonnées et envoie-le à Claude. On corrige, puis on renvoie.", []),
    ]),
]

REGLES = [
    "Ne donne jamais le fichier .p8, l'ID de la clé ni l'ID de l'émetteur, à personne : Claude non plus n'en a pas besoin.",
    "Tes coordonnées (nom, téléphone, adresse) restent dans App Store Connect : ne les envoie pas dans la conversation.",
    "FluxTV ne fournit aucune chaîne : ne promets jamais de chaînes, ni à Apple ni aux utilisateurs.",
    "Un doute, une erreur, un écran différent de ce guide : envoie une capture à Claude, sans tes informations personnelles.",
]


def total_etapes():
    return sum(len(p[3]) for p in PHASES)


# ─────────────────────────────── Note Obsidian ───────────────────────────────

def en_markdown(t):
    t = re.sub(r'<a href="([^"]+)">(.*?)</a>', r"[\2](\1)", t)
    t = re.sub(r"</?b>", "**", t)
    t = re.sub(r"</?code>", "`", t)
    t = re.sub(r"<[^>]+>", "", t)
    return html.unescape(t)


def note():
    out = ["---", "tags: [fluxtv, ios, app-store]", "mis_a_jour: 2026-11-04", "---",
           "# App Store : guide complet", "",
           "Toutes les étapes pour mettre FluxTV sur l'App Store, dans l'ordre. Version interactive (cases à cocher, boutons « Copier ») : "
           "[Guide App Store FluxTV](https://claude.ai/artifact/QnBmnsXPZaJBjRhp2U5RVa) (page privée, visible seulement par toi). "
           "Résumé : [[App Store - pas à pas]].", ""]
    n = 0
    for i, (pid, titre, intro, etapes) in enumerate(PHASES, 1):
        out += [f"## {i}. {titre}", ""]
        if intro:
            out += [intro, ""]
        for eid, et, texte, champs in etapes:
            n += 1
            ligne = f"- [ ] **{et}**"
            if texte:
                t = en_markdown(texte)
                if len(t) > 1 and t[0].isupper() and t[1].islower():
                    t = t[0].lower() + t[1:]
                ligne += f" : {t}"
            out.append(ligne)
            for c in champs:
                label, limite, fr, en = CHAMPS[c]
                lim = f" ({limite} max)" if limite else ""
                if "\n" in fr or (en and "\n" in en) or len(fr) > 120:
                    out.append(f"    - {label}{lim}, français :")
                    out += ["      ```", *["      " + l for l in fr.split("\n")], "      ```"]
                    if en:
                        out.append(f"    - {label}{lim}, anglais :")
                        out += ["      ```", *["      " + l for l in en.split("\n")], "      ```"]
                elif en:
                    out.append(f"    - {label}{lim} : français `{fr}` · anglais `{en}`")
                else:
                    out.append(f"    - {label}{lim} : `{fr}`")
        out.append("")
    out += ["## Règles de sécurité", ""] + [f"- {r}" for r in REGLES] + [""]
    return "\n".join(out), n


# ─────────────────────────────── Page interactive ───────────────────────────────

def compte(t):
    return len(t)


def bloc_champ(c):
    label, limite, fr, en = CHAMPS[c]
    long_ = "\n" in fr or len(fr) > 140
    lim = f"<span class=\"slip-count\" data-count>{compte(fr)} / {limite}</span>" if limite else ""
    meme = "" if en else '<span class="slip-same">même texte en anglais</span>'
    return (f'<div class="slip{" slip-long" if long_ else ""}" data-champ="{c}">'
            f'<div class="slip-head"><span class="slip-label">{html.escape(label)}</span>{meme}{lim}'
            f'<button type="button" class="copy" aria-label="Copier : {html.escape(label)}">Copier</button></div>'
            f'<pre class="slip-value" tabindex="0">{html.escape(fr)}</pre></div>')


def typo(doc):
    """Espaces insécables de la typographie française dans les textes affichés (pas dans les
    scripts, les styles ni les textes à coller)."""
    morceaux = re.split(r"(<script\b.*?</script>|<style\b.*?</style>|<pre\b.*?</pre>)", doc, flags=re.S)
    def texte(m):
        t = m.group(1)
        for avant, apres in ((" :", "\u00a0:"), (" ;", "\u00a0;"), (" ?", "\u00a0?"), (" !", "\u00a0!"),
                             ("« ", "«\u00a0"), (" »", "\u00a0»")):
            t = t.replace(avant, apres)
        return ">" + t + "<"
    return "".join(m if re.match(r"<(script|style|pre)\b", m) else re.sub(r">([^<]+)<", texte, m) for m in morceaux)


def page():
    phases_html = []
    nav = []
    for i, (pid, titre, intro, etapes) in enumerate(PHASES, 1):
        nav.append(f'<li><a href="#{pid}"><span class="nav-num">{i:02d}</span><span class="nav-title">{html.escape(titre)}</span>'
                   f'<span class="nav-count" data-phase-count="{pid}">0/{len(etapes)}</span></a></li>')
        items = []
        for eid, et, texte, champs in etapes:
            sid = f"{pid}-{eid}"
            corps = f'<p class="step-text">{texte}</p>' if texte else ""
            slips = "".join(bloc_champ(c) for c in champs)
            slips = f'<div class="slips">{slips}</div>' if slips else ""
            items.append(f'<li class="step" data-step="{sid}"><label class="step-check" for="c-{sid}">'
                         f'<input type="checkbox" id="c-{sid}" data-check="{sid}"><span class="box" aria-hidden="true"></span>'
                         f'<span class="step-title">{html.escape(et)}</span></label>'
                         f'<div class="step-body">{corps}{slips}</div></li>')
        phases_html.append(
            f'<section class="phase" id="{pid}" data-phase="{pid}"><div class="phase-head"><span class="phase-num">{i:02d}</span>'
            f'<div class="phase-titles"><h2>{html.escape(titre)}</h2>'
            + (f'<p class="phase-intro">{html.escape(intro)}</p>' if intro else "")
            + f'</div><span class="phase-count" data-phase-count="{pid}">0/{len(etapes)}</span></div>'
            f'<ol class="steps">{"".join(items)}</ol></section>')
    champs_js = {c: {"fr": v[2], "en": v[3] if v[3] else v[2], "limite": v[1], "meme": v[3] is None} for c, v in CHAMPS.items()}
    regles = "".join(f"<li>{html.escape(r)}</li>" for r in REGLES)
    return typo(MODELE).replace("%%NAV%%", typo("".join(nav))).replace("%%PHASES%%", typo("".join(phases_html))) \
        .replace("%%REGLES%%", typo(regles)).replace("%%TOTAL%%", str(total_etapes())) \
        .replace("%%CHAMPS%%", json.dumps(champs_js, ensure_ascii=False).replace("</", "<\\/"))


MODELE = r"""<title>Guide App Store FluxTV</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Manrope:wght@400;500;600;700&family=Sora:wght@600;700&display=swap">
<style>
/* Mise en page : un carnet de route — en-tête collant avec la progression, dix chapitres dans l'ordre réel,
   chaque étape à cocher, et les textes à coller sur des fiches avec compteur de caractères (les limites d'Apple). */
:root {
  --bg: #090918; --surface: #11112b; --surface-2: #181838; --line: #262650;
  --fg: #f1f2ff; --fg-dim: #a6abd2; --accent: #8e97ff; --accent-ink: #ffffff; --accent-fill: #5b5cf0;
  --ok: #34d399; --warn: #fbbf24; --code: #0d0d24;
  --display: "Sora", "Manrope", system-ui, sans-serif;
  --body: "Manrope", system-ui, -apple-system, "Segoe UI", sans-serif;
  --mono: ui-monospace, "SF Mono", Menlo, Consolas, monospace;
  color-scheme: dark;
}
@media (prefers-color-scheme: light) {
  :root:not([data-theme="dark"]) {
    --bg: #f5f5fc; --surface: #ffffff; --surface-2: #eeeffb; --line: #d9dbf1;
    --fg: #15152c; --fg-dim: #575b7e; --accent: #4647d6; --accent-ink: #ffffff; --accent-fill: #5b5cf0;
    --ok: #0e8f63; --warn: #a86b12; --code: #f2f3fb; color-scheme: light;
  }
}
:root[data-theme="light"] {
  --bg: #f5f5fc; --surface: #ffffff; --surface-2: #eeeffb; --line: #d9dbf1;
  --fg: #15152c; --fg-dim: #575b7e; --accent: #4647d6; --accent-ink: #ffffff; --accent-fill: #5b5cf0;
  --ok: #0e8f63; --warn: #a86b12; --code: #f2f3fb; color-scheme: light;
}
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--fg); font: 16px/1.55 var(--body); }
body::before { content: ""; position: fixed; inset: 0 0 auto; height: env(safe-area-inset-top, 0px); z-index: 10; background: var(--surface); }
a { color: var(--accent); text-underline-offset: 2px; }
a:focus-visible, button:focus-visible, input:focus-visible + .box, .slip-value:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
code { font-family: var(--mono); font-size: .88em; background: var(--surface-2); border: 1px solid var(--line); border-radius: 5px; padding: 0 .3em; overflow-wrap: anywhere; }
.wrap { max-width: 880px; margin: 0 auto; padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px)); }

/* En-tête collant */
.bar { position: sticky; top: env(safe-area-inset-top, 0px); z-index: 5; background: var(--surface); border-bottom: 1px solid var(--line); }
.bar .wrap { display: flex; align-items: center; gap: 14px; padding-block: 10px; flex-wrap: wrap; }
.brand { display: flex; align-items: center; gap: 10px; font: 700 15px var(--display); letter-spacing: -.01em; }
.brand svg { width: 26px; height: 26px; flex: none; }
.progress { flex: 1 1 200px; min-width: 0; display: flex; align-items: center; gap: 10px; }
.track { flex: 1; height: 6px; border-radius: 99px; background: var(--surface-2); overflow: hidden; }
.fill { height: 100%; width: 0; background: var(--accent-fill); border-radius: inherit; transition: width .3s; }
.progress-text { font: 600 13px var(--body); color: var(--fg-dim); font-variant-numeric: tabular-nums; white-space: nowrap; }
.lang { display: inline-flex; border: 1px solid var(--line); border-radius: 99px; padding: 3px; background: var(--bg); }
.lang button { border: 0; background: none; color: var(--fg-dim); font: 600 13px var(--body); padding: 5px 12px; border-radius: 99px; cursor: pointer; }
.lang button[aria-pressed="true"] { background: var(--accent-fill); color: var(--accent-ink); }

/* Ouverture */
.intro { padding-block: 40px 8px; display: grid; gap: 18px; }
.eyebrow { font: 700 12px var(--body); letter-spacing: .14em; text-transform: uppercase; color: var(--accent); margin: 0; }
h1 { font: 700 clamp(30px, 6vw, 44px)/1.08 var(--display); letter-spacing: -.02em; margin: 0; text-wrap: balance; }
.lead { margin: 0; color: var(--fg-dim); max-width: 62ch; font-size: 17px; }
.ready { display: flex; flex-wrap: wrap; gap: 8px; margin: 0; padding: 0; list-style: none; }
.ready li { display: inline-flex; align-items: center; gap: 7px; font: 600 13px var(--body); padding: 6px 12px; border-radius: 99px; background: var(--surface); border: 1px solid var(--line); }
.ready li::before { content: ""; width: 8px; height: 8px; border-radius: 50%; background: var(--ok); }
.ready-label { font: 600 13px var(--body); color: var(--fg-dim); margin: 0 0 -8px; }

/* Sommaire */
.toc { padding-block: 18px 6px; }
.toc ol { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 8px; }
.toc a { display: flex; align-items: center; gap: 10px; padding: 10px 12px; border-radius: 10px; background: var(--surface); border: 1px solid var(--line); text-decoration: none; color: var(--fg); min-width: 0; }
.toc a:hover { border-color: var(--accent); }
.nav-num { font: 700 12px var(--mono); color: var(--accent); }
.nav-title { flex: 1; min-width: 0; font-weight: 600; font-size: 14px; }
.nav-count, .phase-count { font: 600 12px var(--body); color: var(--fg-dim); font-variant-numeric: tabular-nums; }
.done .nav-count, .phase.done .phase-count { color: var(--ok); }

/* Chapitres */
.phase { padding-block: 34px 6px; scroll-margin-top: 70px; }
.phase-head { display: flex; align-items: flex-start; gap: 14px; margin-bottom: 14px; }
.phase-num { font: 700 13px var(--mono); color: var(--accent-ink); background: var(--accent-fill); border-radius: 8px; padding: 5px 8px; margin-top: 3px; }
.phase-titles { flex: 1; min-width: 0; }
h2 { font: 700 22px/1.25 var(--display); letter-spacing: -.01em; margin: 0; text-wrap: balance; }
.phase-intro { margin: 3px 0 0; color: var(--fg-dim); font-size: 15px; }
.phase-count { margin-top: 6px; white-space: nowrap; }
.steps { list-style: none; margin: 0; padding: 0; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); overflow: hidden; }
.step { padding: 14px 16px 16px; border-top: 1px solid var(--line); display: grid; gap: 8px; }
.step:first-child { border-top: 0; }
.step-check { display: flex; align-items: flex-start; gap: 12px; cursor: pointer; }
.step-check input { position: absolute; opacity: 0; width: 1px; height: 1px; }
.box { flex: none; width: 22px; height: 22px; border-radius: 6px; border: 2px solid var(--line); background: var(--bg); margin-top: 1px; display: grid; place-items: center; transition: background .15s, border-color .15s; }
.step-check:hover .box { border-color: var(--accent); }
input:checked + .box { background: var(--ok); border-color: var(--ok); }
input:checked + .box::after { content: ""; width: 6px; height: 11px; border: solid var(--bg); border-width: 0 2.5px 2.5px 0; transform: translateY(-1px) rotate(45deg); }
.step-title { font: 600 16px/1.4 var(--body); }
.step.is-done .step-title { color: var(--fg-dim); text-decoration: line-through; text-decoration-color: var(--line); }
.step-body { padding-left: 34px; min-width: 0; display: grid; gap: 10px; }
.step-text { margin: 0; color: var(--fg-dim); max-width: 68ch; }
.step-text b { color: var(--fg); font-weight: 600; }

/* Fiches à coller */
.slips { display: grid; gap: 8px; }
.slip { border: 1px dashed var(--line); border-radius: 10px; background: var(--code); min-width: 0; }
.slip-head { display: flex; align-items: center; gap: 10px; padding: 7px 8px 7px 12px; flex-wrap: wrap; }
.slip-label { font: 700 11px var(--body); letter-spacing: .1em; text-transform: uppercase; color: var(--fg-dim); flex: 1; min-width: 0; }
.slip-same { font-size: 12px; color: var(--fg-dim); }
.slip-count { font: 600 12px var(--mono); color: var(--ok); font-variant-numeric: tabular-nums; }
.slip-count.over { color: var(--warn); }
.copy { border: 1px solid var(--line); background: var(--surface); color: var(--fg); font: 600 13px var(--body); border-radius: 8px; padding: 5px 12px; cursor: pointer; min-width: 78px; }
.copy:hover { border-color: var(--accent); color: var(--accent); }
.copy.ok { background: var(--ok); border-color: var(--ok); color: var(--bg); }
.slip-value { margin: 0; padding: 4px 12px 11px; font: 14px/1.5 var(--mono); white-space: pre-wrap; overflow-wrap: anywhere; color: var(--fg); }
.slip-long .slip-value { max-height: 210px; overflow: auto; }

/* Règles et fin */
.rules { margin-block: 40px 16px; border: 1px solid var(--warn); border-radius: 14px; padding: 18px 20px; background: var(--surface); }
.rules h2 { font-size: 18px; color: var(--warn); }
.rules ul { margin: 10px 0 0; padding-left: 20px; display: grid; gap: 6px; color: var(--fg-dim); }
.end { padding-block: 8px 48px; display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; color: var(--fg-dim); font-size: 13px; }
.reset { border: 1px solid var(--line); background: none; color: var(--fg-dim); font: 600 13px var(--body); border-radius: 8px; padding: 7px 12px; cursor: pointer; }
.reset.confirm { border-color: var(--warn); color: var(--warn); }
@media (max-width: 560px) {
  .phase { scroll-margin-top: 150px; }
  .step-body { padding-left: 0; }
  .phase-count { display: none; }
}
@media (prefers-reduced-motion: reduce) { * { transition: none !important; scroll-behavior: auto !important; } }
</style>

<header class="bar">
  <div class="wrap">
    <span class="brand"><svg viewBox="0 0 32 32" aria-hidden="true"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#5b5cf0"/><stop offset="1" stop-color="#9b4deb"/></linearGradient></defs><rect width="32" height="32" rx="8" fill="url(#g)"/><path d="M11 9.5v13l10-6.5z" fill="#fff"/><path d="M23.5 11.5a7 7 0 0 1 0 9" stroke="#fff" stroke-width="2" fill="none" stroke-linecap="round"/></svg>FluxTV · App Store</span>
    <div class="progress"><div class="track" role="progressbar" aria-label="Étapes faites" aria-valuemin="0" aria-valuemax="%%TOTAL%%" aria-valuenow="0"><div class="fill"></div></div><span class="progress-text" id="progress-text">0 / %%TOTAL%% étapes</span></div>
    <div class="lang" role="group" aria-label="Langue des textes à coller"><button type="button" id="lang-fr" aria-pressed="true">Français</button><button type="button" id="lang-en" aria-pressed="false">English</button></div>
  </div>
</header>

<main>
  <div class="wrap intro">
    <p class="eyebrow">Guide complet · iPhone, iPad et Apple TV</p>
    <h1>Mettre FluxTV sur l'App Store</h1>
    <p class="lead">Toutes les étapes, dans l'ordre : la fiche dans App Store Connect, la clé d'accès, l'envoi depuis GitHub, l'essai sur TestFlight et la soumission. Coche chaque étape faite : ta progression reste dans ce navigateur. Les boutons « Copier » collent les textes dans la langue choisie en haut.</p>
    <p class="ready-label">Déjà prêt</p>
    <ul class="ready">
      <li>46 captures aux tailles d'Apple</li>
      <li>Textes de la fiche en français et en anglais</li>
      <li>Page Assistance en ligne</li>
      <li>Envoi automatique prêt sur GitHub</li>
      <li>Archives vérifiées</li>
    </ul>
  </div>
  <nav class="wrap toc" aria-label="Chapitres"><ol>%%NAV%%</ol></nav>
  <div class="wrap">%%PHASES%%
    <aside class="rules"><h2>Règles de sécurité</h2><ul>%%REGLES%%</ul></aside>
    <div class="end"><span>Guide préparé par Claude le 4 novembre 2026, pour FluxTV 1.2.</span><button type="button" class="reset" id="reset">Tout décocher</button></div>
  </div>
</main>

<script>
(function () {
  var CHAMPS = %%CHAMPS%%;
  var CLE = "fluxtv-guide-app-store-v1";
  var etat = { coches: {}, langue: "fr" };
  try { var lu = JSON.parse(localStorage.getItem(CLE) || "null"); if (lu && typeof lu === "object") { etat.coches = lu.coches || {}; etat.langue = lu.langue === "en" ? "en" : "fr"; } } catch (e) {}
  function garder() { try { localStorage.setItem(CLE, JSON.stringify(etat)); } catch (e) {} }

  var cases = Array.prototype.slice.call(document.querySelectorAll("[data-check]"));
  function compter() {
    var total = cases.length, faites = 0, parPhase = {};
    cases.forEach(function (c) {
      var pid = c.closest("[data-phase]").getAttribute("data-phase");
      parPhase[pid] = parPhase[pid] || [0, 0];
      parPhase[pid][1]++;
      if (c.checked) { faites++; parPhase[pid][0]++; }
      c.closest(".step").classList.toggle("is-done", c.checked);
    });
    document.querySelector(".fill").style.width = (100 * faites / total) + "%";
    document.querySelector(".track").setAttribute("aria-valuenow", faites);
    document.getElementById("progress-text").textContent = faites + " / " + total + " étapes";
    Object.keys(parPhase).forEach(function (pid) {
      var v = parPhase[pid];
      document.querySelectorAll('[data-phase-count="' + pid + '"]').forEach(function (el) {
        el.textContent = v[0] + "/" + v[1];
        var fini = v[0] === v[1];
        (el.closest("li") || el.closest(".phase")).classList.toggle("done", fini);
        document.getElementById(pid).classList.toggle("done", fini);
      });
    });
  }
  cases.forEach(function (c) {
    c.checked = !!etat.coches[c.getAttribute("data-check")];
    c.addEventListener("change", function () {
      if (c.checked) etat.coches[c.getAttribute("data-check")] = true; else delete etat.coches[c.getAttribute("data-check")];
      garder(); compter();
    });
  });

  function langue(l) {
    etat.langue = l;
    document.getElementById("lang-fr").setAttribute("aria-pressed", l === "fr");
    document.getElementById("lang-en").setAttribute("aria-pressed", l === "en");
    document.querySelectorAll("[data-champ]").forEach(function (s) {
      var c = CHAMPS[s.getAttribute("data-champ")], t = c[l];
      s.querySelector(".slip-value").textContent = t;
      var n = s.querySelector("[data-count]");
      if (n) { n.textContent = t.length + " / " + c.limite; n.classList.toggle("over", t.length > c.limite); }
    });
  }
  document.getElementById("lang-fr").addEventListener("click", function () { langue("fr"); garder(); });
  document.getElementById("lang-en").addEventListener("click", function () { langue("en"); garder(); });

  document.querySelectorAll(".copy").forEach(function (b) {
    b.addEventListener("click", function () {
      var s = b.closest("[data-champ]"), pre = s.querySelector(".slip-value");
      var texte = CHAMPS[s.getAttribute("data-champ")][etat.langue];
      function fait(msg) { b.textContent = msg; b.classList.add("ok"); setTimeout(function () { b.textContent = "Copier"; b.classList.remove("ok"); }, 1600); }
      function selection() {
        var r = document.createRange(); r.selectNodeContents(pre);
        var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(r);
        b.textContent = "Ctrl+C"; setTimeout(function () { b.textContent = "Copier"; }, 2500);
      }
      try {
        navigator.clipboard.writeText(texte).then(function () { fait("Copié"); }, selection);
      } catch (e) { selection(); }
    });
  });

  var reset = document.getElementById("reset"), attente;
  reset.addEventListener("click", function () {
    if (!reset.classList.contains("confirm")) {
      reset.classList.add("confirm"); reset.textContent = "Confirmer : tout décocher ?";
      attente = setTimeout(function () { reset.classList.remove("confirm"); reset.textContent = "Tout décocher"; }, 4000);
      return;
    }
    clearTimeout(attente);
    etat.coches = {}; cases.forEach(function (c) { c.checked = false; });
    garder(); compter();
    reset.classList.remove("confirm"); reset.textContent = "Tout décocher";
  });

  langue(etat.langue);
  compter();
})();
</script>
"""

if __name__ == "__main__":
    sortie_html, sortie_md = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    sortie_html.write_text(page(), encoding="utf-8")
    md, n = note()
    sortie_md.write_text(md, encoding="utf-8")
    print(f"{n} étapes · {sortie_html} ({sortie_html.stat().st_size} octets) · {sortie_md}")
    for c, (label, limite, fr, en) in CHAMPS.items():
        for t in (fr, en or fr):
            assert not limite or len(t) <= limite, (c, len(t))
