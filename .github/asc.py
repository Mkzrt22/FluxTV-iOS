#!/usr/bin/env python3
"""App Store Connect : contrôle de la clé d'API et suivi des builds envoyés. Ne modifie RIEN dans
App Store Connect : uniquement des lectures.

  asc.py verifier [build]    clé acceptée ? app trouvée ? versions en préparation, builds déjà
                             envoyés ; écrit le numéro de build à utiliser dans GITHUB_OUTPUT
  asc.py suivi <build>       attend la fin du traitement par Apple du build envoyé (45 min au plus)
  asc.py cle <fichier>       écrit la clé .p8 remise en forme (pour xcodebuild)

Clé lue dans ASC_KEY_ID, ASC_ISSUER_ID et ASC_KEY_P8 (secrets du dépôt). Les journaux de ce dépôt
sont publics : rien d'identifiant n'est affiché (ni la clé, ni ses numéros, ni le nom du titulaire).
"""
import base64
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

BUNDLE = 'bzh.fluxtv.player'
API = 'https://api.appstoreconnect.apple.com'
RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLATEFORMES = {'IOS': 'iPhone et iPad', 'TV_OS': 'Apple TV'}
ETATS = {
    'PREPARE_FOR_SUBMISSION': 'en préparation',
    'READY_FOR_REVIEW': 'prête pour la vérification',
    'WAITING_FOR_REVIEW': 'en attente de vérification',
    'IN_REVIEW': 'en cours de vérification',
    'PENDING_DEVELOPER_RELEASE': 'acceptée, à publier',
    'READY_FOR_SALE': 'en vente',
    'READY_FOR_DISTRIBUTION': 'publiée',
    'REJECTED': 'refusée',
    'METADATA_REJECTED': 'refusée (fiche)',
    'DEVELOPER_REJECTED': 'retirée de la vérification',
    'INVALID_BINARY': 'build non valide',
}
MODIFIABLES = {'PREPARE_FOR_SUBMISSION', 'REJECTED', 'METADATA_REJECTED', 'DEVELOPER_REJECTED', 'INVALID_BINARY'}


def dire(texte=''):
    print(texte, flush=True)


def arreter(texte):
    dire(texte)
    sys.exit(1)


# ───────────────────────────── Clé et jeton ─────────────────────────────

def cle_pem():
    """Contenu du fichier .p8, remis en forme (collé depuis le Bloc-notes : fins de ligne Windows,
    espaces en trop, ou encodé en base64)."""
    brut = os.environ.get('ASC_KEY_P8', '').strip()
    if not brut:
        arreter('✘ Secret ASC_KEY_P8 absent : voir AppStore/Envoi-App-Store.md, étape 2.')
    if 'BEGIN PRIVATE KEY' not in brut:
        try:
            brut = base64.b64decode(''.join(brut.split())).decode('utf-8')
        except Exception:
            brut = ''
    m = re.search(r'-----BEGIN PRIVATE KEY-----(.*?)-----END PRIVATE KEY-----', brut, re.S)
    if not m:
        arreter('✘ ASC_KEY_P8 ne contient pas une clé .p8 complète : il faut tout le fichier, '
                'lignes « -----BEGIN PRIVATE KEY----- » et « -----END PRIVATE KEY----- » comprises.')
    corps = ''.join(m.group(1).split())
    lignes = '\n'.join(corps[i:i + 64] for i in range(0, len(corps), 64))
    return '-----BEGIN PRIVATE KEY-----\n' + lignes + '\n-----END PRIVATE KEY-----\n'


def b64url(octets):
    return base64.urlsafe_b64encode(octets).rstrip(b'=').decode('ascii')


def entier_der(octets, i):
    if octets[i] != 0x02:
        raise ValueError('signature inattendue')
    n = octets[i + 1]
    return int.from_bytes(octets[i + 2:i + 2 + n], 'big'), i + 2 + n


def signer_es256(message, pem):
    """Signature ES256 (r ‖ s, 64 octets) avec openssl : aucune bibliothèque à installer."""
    with tempfile.TemporaryDirectory() as d:
        chemin = os.path.join(d, 'cle.pem')
        with open(chemin, 'w', encoding='ascii') as f:
            f.write(pem)
        os.chmod(chemin, 0o600)
        r = subprocess.run(['openssl', 'dgst', '-sha256', '-sign', chemin], input=message, capture_output=True)
    if r.returncode != 0 or not r.stdout:
        arreter('✘ ASC_KEY_P8 : la clé est illisible (fichier .p8 abîmé ou incomplet ?).')
    der = r.stdout
    if der[0] != 0x30:
        arreter('✘ Signature inattendue.')
    i = 2 if der[1] < 0x80 else 2 + (der[1] & 0x7F)
    rr, i = entier_der(der, i)
    ss, i = entier_der(der, i)
    return rr.to_bytes(32, 'big') + ss.to_bytes(32, 'big')


def jeton():
    kid = os.environ.get('ASC_KEY_ID', '').strip()
    iss = os.environ.get('ASC_ISSUER_ID', '').strip()
    if not kid:
        arreter('✘ Secret ASC_KEY_ID absent : voir AppStore/Envoi-App-Store.md, étape 2.')
    if not iss:
        arreter('✘ Secret ASC_ISSUER_ID absent : voir AppStore/Envoi-App-Store.md, étape 2.')
    if not re.fullmatch(r'[A-Z0-9]{10}', kid):
        arreter('✘ ASC_KEY_ID ne ressemble pas à un ID de clé (10 lettres majuscules et chiffres) : '
                'as-tu inversé avec l\'ID de l\'émetteur ?')
    if not re.fullmatch(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', iss):
        arreter('✘ ASC_ISSUER_ID ne ressemble pas à un ID d\'émetteur (format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).')
    maintenant = int(time.time())
    entete = b64url(json.dumps({'alg': 'ES256', 'kid': kid, 'typ': 'JWT'}, separators=(',', ':')).encode())
    charge = b64url(json.dumps({'iss': iss, 'iat': maintenant - 30, 'exp': maintenant + 15 * 60,
                                'aud': 'appstoreconnect-v1'}, separators=(',', ':')).encode())
    signature = signer_es256(f'{entete}.{charge}'.encode('ascii'), cle_pem())
    return f'{entete}.{charge}.{b64url(signature)}'


# ───────────────────────────── API ─────────────────────────────

class Api:
    def __init__(self):
        self.jeton = jeton()
        self.depuis = time.time()

    def lire(self, chemin, parametres=None):
        if time.time() - self.depuis > 10 * 60:
            self.jeton, self.depuis = jeton(), time.time()
        url = API + chemin
        if parametres:
            url += '?' + urllib.parse.urlencode(parametres, safe='[],-')
        req = urllib.request.Request(url, headers={'Authorization': 'Bearer ' + self.jeton, 'Accept': 'application/json'})
        for essai in range(4):
            try:
                with urllib.request.urlopen(req, timeout=60) as r:
                    return json.load(r)
            except urllib.error.HTTPError as e:
                detail = ''
                try:
                    erreurs = json.loads(e.read().decode('utf-8', 'replace')).get('errors') or []
                    detail = ' · '.join(filter(None, (x.get('detail') or x.get('title') for x in erreurs)))[:300]
                except Exception:
                    pass
                if e.code == 401:
                    arreter('✘ Clé refusée par Apple (401) : un des trois secrets est faux. Vérifie l\'ID de la '
                            'clé (ASC_KEY_ID), l\'ID de l\'émetteur (ASC_ISSUER_ID), et que ASC_KEY_P8 contient '
                            'tout le fichier .p8 de CETTE clé. Une clé révoquée est refusée aussi.')
                if e.code == 403:
                    arreter('✘ Accès refusé par Apple (403) : la clé doit avoir l\'accès « Admin ». '
                            + (f'Apple : {detail}' if detail else ''))
                if e.code in (429, 500, 502, 503, 504) and essai < 3:
                    time.sleep(10 * (essai + 1))
                    continue
                arreter(f'✘ App Store Connect a répondu {e.code}' + (f' : {detail}' if detail else '') + '.')
            except (urllib.error.URLError, TimeoutError) as e:
                if essai < 3:
                    time.sleep(10 * (essai + 1))
                    continue
                arreter(f'✘ App Store Connect injoignable : {e}')

    def tout(self, chemin, parametres=None):
        """Toutes les pages d'une liste (avec les objets « included »)."""
        donnees, inclus = [], []
        page = self.lire(chemin, parametres)
        while True:
            donnees += page.get('data') or []
            inclus += page.get('included') or []
            suivant = (page.get('links') or {}).get('next')
            if not suivant:
                return donnees, inclus
            page = self.lire(suivant.replace(API, '').split('?')[0],
                             dict(urllib.parse.parse_qsl(urllib.parse.urlsplit(suivant).query)))


def version_du_projet():
    with open(os.path.join(RACINE, 'project.yml'), encoding='utf-8') as f:
        m = re.search(r'MARKETING_VERSION:\s*"([^"]+)"', f.read())
    return m.group(1) if m else '?'


def trouver_app(api):
    data, _ = api.tout('/v1/apps', {'filter[bundleId]': BUNDLE, 'limit': '10'})
    app = next((a for a in data if (a.get('attributes') or {}).get('bundleId') == BUNDLE), None)
    if not app:
        arreter(f'✘ Clé acceptée, mais aucune app « {BUNDLE} » dans App Store Connect. Il faut d\'abord la '
                'créer (Apps › « + » › Nouvelle app, identifiant de lot bzh.fluxtv.player).')
    return app


def builds(api, app_id, numero=None):
    """[(numéro de build, version, plateforme, état du traitement, date d'envoi)], plus récents d'abord."""
    p = {'filter[app]': app_id, 'include': 'preReleaseVersion', 'limit': '50', 'sort': '-uploadedDate'}
    if numero:
        p['filter[version]'] = str(numero)
    data, inclus = api.tout('/v1/builds', p)
    versions = {i['id']: i.get('attributes') or {} for i in inclus if i.get('type') == 'preReleaseVersions'}
    sortie = []
    for b in data:
        a = b.get('attributes') or {}
        pre = ((b.get('relationships') or {}).get('preReleaseVersion') or {}).get('data') or {}
        v = versions.get(pre.get('id'), {})
        sortie.append((a.get('version'), v.get('version'), v.get('platform'), a.get('processingState'),
                       (a.get('uploadedDate') or '')[:16].replace('T', ' ')))
    return sortie


# ───────────────────────────── Commandes ─────────────────────────────

def verifier(numero_prevu):
    attendue = version_du_projet()
    api = Api()
    app = trouver_app(api)
    dire(f'✔ Clé acceptée par Apple · app trouvée : {(app.get("attributes") or {}).get("name")} ({BUNDLE})')

    versions, _ = api.tout(f'/v1/apps/{app["id"]}/appStoreVersions', {'limit': '50'})
    vues = set()
    dire('── Versions dans App Store Connect')
    for v in versions:
        a = v.get('attributes') or {}
        plateforme = a.get('platform')
        etat = a.get('appVersionState') or a.get('appStoreState') or '?'
        dire(f'  {PLATEFORMES.get(plateforme, plateforme)} : version {a.get("versionString")}, {ETATS.get(etat, etat)}')
        if etat in MODIFIABLES:
            vues.add(plateforme)
            if a.get('versionString') != attendue:
                dire(f'  ⚠ Cette version s\'appelle « {a.get("versionString")} » alors que l\'app envoyée est la '
                     f'{attendue} : sur sa page, remplace le numéro par {attendue} (champ « Version »), puis Enregistrer.')
    for p in ('IOS', 'TV_OS'):
        if p not in vues:
            dire(f'  ⚠ Aucune version {PLATEFORMES[p]} en préparation : le build sera dans TestFlight, mais pour le '
                 'soumettre il faudra une version « en préparation » pour cette plateforme.')

    deja = builds(api, app['id'])
    dire('── Builds déjà envoyés')
    if not deja:
        dire('  aucun')
    for numero, version, plateforme, etat, date in deja[:10]:
        dire(f'  {PLATEFORMES.get(plateforme, plateforme)} {version} ({numero}) : {etat}, envoyé le {date} (UTC)')

    # Numéro de build : jamais déjà pris (Apple refuse un numéro déjà envoyé pour la même version)
    numeros = [int(n) for n, *_ in deja if str(n).isdigit()]
    numero = max([numero_prevu] + [n + 1 for n in numeros])
    if numero != numero_prevu:
        dire(f'  Numéro de build {numero_prevu} déjà utilisé : ce sera {numero}.')
    dire(f'✔ Prêt pour l\'envoi : FluxTV {attendue}, build {numero}')
    sortie = os.environ.get('GITHUB_OUTPUT')
    if sortie:
        with open(sortie, 'a', encoding='utf-8') as f:
            f.write(f'build={numero}\n')


def suivi(numero, plateformes):
    attendues = {'les-deux': ['IOS', 'TV_OS'], 'iphone-ipad': ['IOS'], 'apple-tv': ['TV_OS']}[plateformes]
    api = Api()
    app = trouver_app(api)
    dire(f'Traitement du build {numero} par Apple (en général 10 à 30 minutes)…')
    dernier = None
    for minute in range(46):
        etats = {p: e for n, v, p, e, d in builds(api, app['id'], numero)}
        resume = ', '.join(f'{PLATEFORMES[p]} : {etats.get(p, "pas encore visible")}' for p in attendues)
        if resume != dernier:
            dire(f'  {minute:2d} min · {resume}')
            dernier = resume
        if any(etats.get(p) in ('FAILED', 'INVALID') for p in attendues):
            arreter('✘ Apple a refusé le build pendant le traitement : le motif arrive par e-mail '
                    '(adresse du compte développeur).')
        if all(etats.get(p) == 'VALID' for p in attendues):
            dire(f'✔ Build {numero} prêt : il apparaît dans TestFlight et peut être ajouté aux versions à soumettre.')
            return
        time.sleep(60)
    dire('⚠ Traitement pas encore terminé après 45 minutes : c\'est parfois plus long. Apple envoie un e-mail '
         'quand c\'est prêt.')


def main():
    if len(sys.argv) < 2:
        arreter(__doc__)
    commande = sys.argv[1]
    if commande == 'verifier':
        prevu = int(sys.argv[2]) if len(sys.argv) > 2 else 1
        verifier(prevu)
    elif commande == 'suivi':
        suivi(int(sys.argv[2]), sys.argv[3] if len(sys.argv) > 3 else 'les-deux')
    elif commande == 'cle':
        with open(sys.argv[2], 'w', encoding='ascii') as f:
            f.write(cle_pem())
        os.chmod(sys.argv[2], 0o600)
    else:
        arreter(__doc__)


if __name__ == '__main__':
    main()
