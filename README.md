# FluxTV pour iPhone, iPad et Apple TV — version 1.0

*Made by Kevin G*

Une seule app pour iPhone, iPad et Apple TV, écrite en Swift (SwiftUI), avec le même design
que FluxTV sur Android : nuit profonde, indigo et violet, polices Sora et Manrope.

> FluxTV est un lecteur : il ne fournit aucune chaîne ni aucun contenu. Utilise uniquement
> des sources auxquelles tu as légalement accès.

## Ce que fait l'app

- **Sources** :
  - un compte Xtream (serveur, utilisateur, mot de passe) ;
  - un lien de playlist M3U, avec guide TV XMLTV facultatif ;
  - le mode **Démonstration**, sans abonnement : flux de test publics et films libres de la
    Blender Foundation. C'est aussi ce qui permet à Apple de tester l'app.

  Un lien « get.php?username=…&password=… » est reconnu tout seul comme un compte Xtream.
- **Accueil** :
  - « Continuer à regarder » en grand ;
  - tes chaînes favorites, tes dernières chaînes ;
  - les nouveautés films et séries, tes films et séries favoris.
- **TV en direct** :
  - catégories (barre latérale sur iPad et Apple TV, pastilles sur iPhone) ;
  - programme en cours avec sa barre d'avancement, programme suivant ;
  - favoris par appui long.
- **Films et séries** :
  - grilles d'affiches, tri (récents, titre, note) ;
  - fiches détaillées ;
  - séries : saisons, résumé de chaque épisode, reprise de l'épisode en cours, « épisode
    suivant » automatique avec compte à rebours.
- **Recherche** dans les chaînes, les films et les séries, sans tenir compte des accents ni
  des majuscules.
- **Lecteur** : moteur **VLC** (VLCKit, VideoLAN). Il lit tout ce que les fournisseurs
  envoient : direct MPEG-TS et HLS, films MKV / AVI / MP4, son AC3, E-AC3 et DTS.
  - Choix de l'audio et des sous-titres, format d'image (adapté, remplir, étiré).
  - Zapping, liste des chaînes pendant la lecture, reprise là où tu t'étais arrêté.
  - Sur Apple TV, tout se pilote à la télécommande :

    | Touche | Direct | Film / épisode |
    |---|---|---|
    | ⬆︎ ⬇︎ | chaîne précédente / suivante | ⬇︎ : audio et sous-titres |
    | ◀︎ ▶︎ | infos | −10 s / +10 s |
    | OK | liste des chaînes | pause / lecture |
    | ⏯ | audio et sous-titres | pause / lecture |
    | Menu | fermer | fermer |

- **Noms nettoyés** : « FR| TF1 FHD » s'affiche « TF1 » avec un badge FHD ;
  « FR - Dune (2021) [MULTI] » devient « Dune », 2021, badge MULTI ;
  « FR | SPORTS » devient « Sports ».
- **Confidentialité** :
  - ton compte est enregistré chiffré sur l'appareil, dans le **trousseau sécurisé d'Apple**
    (à défaut, dans un fichier chiffré par iOS, exclu des sauvegardes) ;
  - favoris, reprises et historique restent sur l'appareil ;
  - aucune donnée collectée, aucun pistage, aucune publicité.

Différences avec Android :
- pas de compte intégré à l'app (Apple reçoit une copie de chaque version publiée) ;
- pas de FluxTV Home (Apple ne permet pas de remplacer l'écran d'accueil) ;
- les mises à jour passent par l'App Store.

## Installer sur ton iPhone, ton iPad et ton Apple TV

Il faut :
- un Mac avec **macOS 15.6 ou plus récent** ;
- **Xcode 26**, gratuit sur l'App Store du Mac ;
- ton identifiant Apple.

Sans compte développeur payant, l'app fonctionne **7 jours** sur tes appareils. Il suffit
ensuite de recliquer sur ▶︎.

1. Décompresse `FluxTV-iOS-v1_0.zip` sur le Mac.
2. Ouvre Xcode une première fois, puis **Xcode › Réglages › Comptes › +** et ajoute ton
   identifiant Apple.
3. Dans le dossier `ios`, **clic droit sur `Preparer.command` › Ouvrir**. Si macOS refuse
   (« développeur non identifié ») : **Réglages Système › Confidentialité et sécurité ›
   Ouvrir quand même**. Autre méthode : ouvre le Terminal, tape `bash ` (avec une espace),
   glisse `Preparer.command` dans la fenêtre, puis appuie sur Entrée. Le script :
   - télécharge le moteur VLC officiel (environ 360 Mo) et vérifie son empreinte ;
   - renseigne ton équipe Apple ;
   - ouvre le projet dans Xcode.
4. **iPhone / iPad** :
   - branche-le au Mac en USB et accepte « Faire confiance » ;
   - active **Réglages › Confidentialité et sécurité › Mode développeur** sur l'iPhone ;
   - dans Xcode, en haut : cible **FluxTV**, ton iPhone, puis **▶︎**.
5. **Apple TV** (même Wi-Fi que le Mac) :
   - sur l'Apple TV : **Réglages › Télécommandes et appareils › App et appareils
     distants** ;
   - dans Xcode : **Window › Devices and Simulators** › associer l'Apple TV ;
   - si l'Apple TV le demande : **Réglages › Confidentialité et sécurité › Mode
     développeur** ;
   - en haut : cible **FluxTV TV**, ton Apple TV, puis **▶︎**.
6. Si Xcode demande une équipe : clique le projet **FluxTV** à gauche, puis pour chaque cible
   (**FluxTV** et **FluxTV TV**) : onglet **Signing & Capabilities** › **Team** › ton nom.

⚠️ Je n'ai pas pu compiler les écrans moi-même : il faut un Mac. Le « cerveau » de l'app
(comptes Xtream, playlists, guide, favoris, reprises) est testé : `cd Core && swift test`,
28 tests. Si Xcode affiche une erreur rouge, envoie-moi une capture et je corrige.

## Installer le .ipa sans Mac (Sideloadly)

`FluxTV-iPhone.ipa` est fabriqué par un Mac de GitHub (voir « Compilation automatique »
plus bas) ou sur un Mac avec `Fabriquer-IPA.command` (double-clic, résultat dans le dossier
`ipa`). Le fichier n'est pas signé : Sideloadly le signe avec ton identifiant Apple au moment
de l'installation.

Il faut :
- un PC Windows ou un Mac, et le câble de l'iPhone ;
- **Sideloadly**, gratuit : https://sideloadly.io ;
- sur Windows, **iTunes** et **iCloud** téléchargés sur le site d'Apple (pas les versions du
  Microsoft Store) ;
- ton identifiant Apple (un compte gratuit suffit) ;
- un iPhone ou un iPad sous **iOS 17** ou plus récent.

1. Branche l'iPhone, déverrouille-le et accepte « Faire confiance à cet ordinateur ».
2. Ouvre Sideloadly et glisse `FluxTV-iPhone.ipa` dans la fenêtre.
3. Tape ton identifiant Apple puis clique sur **Start**. Le mot de passe et le code de
   validation à deux facteurs sont demandés : Sideloadly les envoie uniquement à Apple. Si tu
   préfères, crée un identifiant Apple secondaire, gratuit, juste pour ça.
4. Sur l'iPhone : **Réglages › Confidentialité et sécurité › Mode développeur** › active-le,
   laisse l'iPhone redémarrer, puis confirme.
5. **Réglages › Général › VPN et gestion de l'appareil** › ton identifiant Apple › **Faire
   confiance**.
6. Ouvre FluxTV.

Avec un identifiant Apple gratuit, l'app fonctionne **7 jours**. Pour la prolonger, refais
l'étape 3 : favoris et reprises sont conservés. Avec l'Apple Developer Program, c'est 1 an.

## Compilation automatique (GitHub)

Le dossier `.github` fait compiler l'app par un Mac de GitHub à chaque envoi sur la branche
`main` d'un dépôt **privé** :
- page **Releases** du dépôt › « Version de test » : `FluxTV-iPhone.ipa` et
  `FluxTV-AppleTV.ipa` ;
- branche `ci` : résumé de la compilation, erreurs et journaux.

Un compte GitHub gratuit a un quota mensuel de minutes de compilation pour les dépôts privés.
En 2025, c'était 2 000 minutes, et une minute sur Mac en comptait 10. Une compilation dure
environ 10 minutes : cela fait une vingtaine de compilations par mois. Le détail du quota est
dans **Settings › Billing** sur GitHub.

## Publier sur l'App Store

Il faut l'**Apple Developer Program** (99 €/an), créé avec ta pièce d'identité. La
validation prend 24 à 48 h. Tout le nécessaire est dans le dossier `AppStore/` :
- `Fiche-App-Store.md` : nom, sous-titre, description, mots-clés, catégorie, prix, âge ;
- `Notes-validation.md` : texte à coller pour l'équipe de validation d'Apple ;
- `confidentialite.html` : politique de confidentialité, à mettre en ligne. Une adresse
  publique est obligatoire, ton VPS convient.

Étapes :
1. Sur **appstoreconnect.apple.com** : **Mes apps › + › Nouvelle app**.
   - Plateformes : iOS et tvOS.
   - Nom : FluxTV.
   - Langue : français.
   - Identifiant de lot : `bzh.fluxtv.player`.
   - SKU : `fluxtv`.
2. Dans Xcode : cible **FluxTV**, appareil « Any iOS Device », puis **Product › Archive** ›
   **Distribute App › App Store Connect › Upload**. Recommence avec **FluxTV TV**
   (« Any tvOS Device »).
3. Dans App Store Connect, remplis la fiche à partir des textes fournis :
   - captures d'écran ;
   - confidentialité : « Données non collectées » ;
   - prix ;
   - notes pour la validation.

   Puis **Soumettre pour vérification**.

Version suivante : augmente `MARKETING_VERSION` (1.1…) et `CURRENT_PROJECT_VERSION` (2, 3…)
dans `project.yml` ou dans Xcode, puis archive à nouveau.

## Structure

```
ios/
  Preparer.command     préparation sur le Mac (moteur VLC, équipe Apple)
  Fabriquer-IPA.command  fabrique les .ipa non signés (Sideloadly)
  FluxTV.xcodeproj     projet Xcode (cibles « FluxTV » iPhone/iPad et « FluxTV TV »)
  project.yml          description du projet (XcodeGen) — sert à régénérer le .xcodeproj
  Config/              Info.plist iOS et tvOS, signature (Signing.xcconfig)
  Core/                cœur sans interface (Swift Package testable partout : swift test)
    Sources/FluxCore   comptes Xtream, M3U, XMLTV, nettoyage des noms, bibliothèque, démo
    Tests/             28 tests
  App/
    FluxTVApp.swift    démarrage, onglets
    Model/             état de l'app, trousseau, favoris / reprises, réglages
    Player/            lecteur VLC et ses commandes (tactile et télécommande)
    Screens/           accueil, TV, films, séries, fiches, recherche, réglages, configuration
    UI/                thème, composants, chargement des images
    Resources/         icônes (iPhone et Apple TV), polices, visuels de la démo, confidentialité
  AppStore/            fiche, notes de validation, politique de confidentialité
  .github/             compilation automatique sur un Mac de GitHub (.ipa, journaux)
```

## Licences

- VLCKit © VideoLAN, GNU LGPL 2.1. Téléchargé depuis download.videolan.org, non modifié à
  part la suppression des architectures 32 bits.
- Polices Sora et Manrope : SIL Open Font License 1.1.
- Films de la démonstration : © Blender Foundation, Creative Commons BY.
