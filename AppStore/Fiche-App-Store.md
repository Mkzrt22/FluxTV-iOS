# Fiche App Store — FluxTV (iPhone, iPad, Apple TV)

À recopier dans App Store Connect (https://appstoreconnect.apple.com › Apps › FluxTV), dans
l'ordre des écrans. Les textes entre `…` se copient tels quels. Toutes les limites d'Apple sont
vérifiées (nombre de caractères indiqué entre parenthèses).

Langue principale de la fiche : **français**. Ajouter ensuite **English (U.S.)** (menu des langues
en haut à droite de la fiche) et recopier la partie anglaise.

---

## 1. Version : 1.0 → 1.2

L'app envoyée porte le numéro **1.2**. Sur la page de la version (colonne de gauche,
« iOS 1.0 »), remplacer **1.0** par `1.2` dans le champ « Version », puis « Enregistrer ».
Même chose pour « tvOS 1.0 ».

## 2. Informations sur l'app (Général › Informations sur l'app)

| Champ | Français | English (U.S.) |
|---|---|---|
| Nom | `FluxTV` | `FluxTV` |
| Sous-titre (30 max) | `Lecteur IPTV pour vos listes` (28) | `IPTV player for your playlists` (30) |

- **Catégorie principale** : Divertissement · **secondaire** : Photo et vidéo
- **Droits sur le contenu** : « Oui, l'app contient, affiche ou donne accès à du contenu tiers » ›
  « J'ai les droits nécessaires » (la démonstration n'utilise que des films libres de la Blender
  Foundation, licence Creative Commons BY, et des flux de test publics ; le reste est apporté par
  l'utilisateur).
- **Classification par âge** : voir « À décider », point 3.
- **Statut de commerçant (Union européenne)** : voir « À décider », point 4.

## 3. Tarifs et disponibilité

- **Prix** : voir « À décider », point 2 (conseil : gratuit, comme sur Android et Xbox).
- **Disponibilité** : tous les pays **sauf la Chine continentale** (Apple y exige une licence
  ICP, que FluxTV n'a pas).

## 4. Confidentialité de l'app

- **URL de la politique de confidentialité** :
  `https://mkzrt22.github.io/FluxTV-iOS/confidentialite/`
  (en anglais : `https://mkzrt22.github.io/FluxTV-iOS/en/privacy/`)
- **Pratiques de confidentialité** › « Commencer » › « Collectez-vous des données à partir de
  cette app ? » › **Non, nous ne collectons pas de données** › Publier. La fiche affichera
  « Données non collectées ».

## 5. Page de la version 1.2 (iOS, puis tvOS)

### Captures d'écran
Fichiers fournis par Claude (`FluxTV-AppStore-captures.zip`), un dossier par emplacement
d'App Store Connect, dans `francais` (fiche française) et `english` (fiche anglaise). Fichiers
numérotés dans l'ordre (les premiers comptent le plus : ils s'affichent dans les recherches).
Où : page de la version › « Informations de la page produit » › onglet **Aperçus et captures
d'écran** (fenêtre étroite : cliquer sur le titre bleu « En-tête et résultats de recherche ⌄ » et
choisir « Aperçus et captures d'écran »), puis l'onglet de l'appareil.

| Dossier | Emplacement dans App Store Connect | |
|---|---|---|
| `1-iPhone-ecran-moyen-1206x2622-obligatoire` | iPhone avec Dynamic Island (écran moyen) | **obligatoire** (6 captures) |
| `2-iPhone-grand-ecran-1320x2868` | iPhone avec Dynamic Island (grand écran) | conseillé (sinon Apple réclame la taille 6,5 pouces) |
| `3-iPad-13-pouces-2064x2752` | iPad, écran de 13 pouces | obligatoire (6 captures) |
| `4-AppleTV-1920x1080` | Apple TV (page tvOS) | obligatoire (5 captures) |

Depuis l'automne 2026, la taille iPhone **obligatoire** est l'écran moyen (1206 × 2622) ; le grand
écran est facultatif. **iPhone Duo** : facultatif jusqu'en avril 2027 (obligatoire seulement pour
les apps compilées avec le SDK iOS 27.1). **En-tête et résultats de recherche** (nouveauté 2026,
images 3840 × 1646 ou 5244 × 2950) : facultatif, à laisser vide pour l'instant.

À laisser vide aussi sur la page de la version : « Fichier de couverture géographique »,
« Extrait d'app », « App iMessage », « Game Center », « Pièce jointe ».

### Texte promotionnel (170 max, modifiable à tout moment sans nouvelle version)
FR (149) :
`Regardez vos chaînes, films et séries sur iPhone, iPad et Apple TV avec votre propre abonnement. Sans publicité, sans pistage. Aucune chaîne fournie.`

EN (137) :
`Watch your live TV, movies and series on iPhone, iPad and Apple TV with your own subscription. No ads, no tracking. No channels included.`

### Mots-clés (100 max, séparés par des virgules, sans espaces)
Pas besoin de répéter les mots du nom et du sous-titre (Apple les prend déjà en compte).

FR (96) : `m3u,xtream,tv,direct,chaînes,guide,epg,xmltv,films,séries,playlist,player,stream,replay,live,vod`

EN (91) : `m3u,m3u8,xtream,live,tv,channels,guide,epg,xmltv,movies,series,stream,vod,catchup,lists,hls`

### URL
- **URL de l'assistance** (obligatoire) : `https://mkzrt22.github.io/FluxTV-iOS/assistance/`
  (anglais : `https://mkzrt22.github.io/FluxTV-iOS/en/help/`). En ligne depuis le 4 novembre
  2026 : questions fréquentes et contact `fluxtv.support@gmail.com`.
- **URL marketing** (facultative) : `https://mkzrt22.github.io/FluxTV-iOS/` (anglais :
  `https://mkzrt22.github.io/FluxTV-iOS/en/`)

### Description (français)
```
FluxTV est un lecteur élégant pour regarder VOS propres listes : chaînes en direct, films et séries, sur iPhone, iPad et Apple TV.

AJOUTEZ VOTRE SOURCE
• Compte Xtream (serveur, utilisateur, mot de passe) ou lien de playlist M3U
• Guide des programmes XMLTV facultatif
• Mode démonstration pour découvrir l'app sans abonnement

TV EN DIRECT
• Catégories claires, chaînes favorites et récentes
• Programme en cours et suivant, avec barre d'avancement
• Zapping rapide et liste des chaînes pendant la lecture

FILMS ET SÉRIES
• Grandes affiches, fiches détaillées, tri par nouveautés, titre ou note
• Reprise automatique là où vous vous étiez arrêté
• Saisons et épisodes, épisode suivant enchaîné automatiquement

LECTEUR COMPLET
• Lit les formats les plus courants : HLS, MPEG-TS, MP4, MKV, AVI
• Son AC3, E-AC3 et DTS, choix de la piste audio et des sous-titres
• Format d'image adapté, rempli ou étiré

PENSÉ POUR CHAQUE ÉCRAN
• Apple TV : tout se pilote à la télécommande Siri Remote
• iPad : catégories en barre latérale
• iPhone : navigation à une main

EN FRANÇAIS ET EN ANGLAIS
• Langue demandée au premier lancement, modifiable dans les réglages

VOS DONNÉES RESTENT CHEZ VOUS
• Aucune collecte de données, aucun pistage, aucune publicité
• Vos identifiants restent chiffrés sur votre appareil (trousseau sécurisé)

IMPORTANT : FluxTV est un lecteur multimédia. Il ne fournit, n'héberge et ne vend aucun contenu, aucune chaîne ni aucun abonnement. Vous devez disposer de vos propres sources et des droits nécessaires pour les regarder.
```

### Description (English)
```
FluxTV is an elegant player to watch YOUR own playlists: live channels, movies and series, on iPhone, iPad and Apple TV.

ADD YOUR SOURCE
• Xtream account (server, username, password) or M3U playlist link
• Optional XMLTV program guide
• Demo mode to explore the app without a subscription

LIVE TV
• Clear categories, favorite and recent channels
• Current and next program, with a progress bar
• Fast channel surfing and the channel list during playback

MOVIES AND SERIES
• Big posters, detailed pages, sort by newest, title or rating
• Automatically resumes where you left off
• Seasons and episodes, next episode plays automatically

A COMPLETE PLAYER
• Plays the most common formats: HLS, MPEG-TS, MP4, MKV, AVI
• AC3, E-AC3 and DTS audio, choice of audio track and subtitles
• Picture fit, fill or stretched

DESIGNED FOR EVERY SCREEN
• Apple TV: everything works with the Siri Remote
• iPad: categories in a sidebar
• iPhone: one-handed navigation

IN ENGLISH AND FRENCH
• Language asked at first launch, changeable anytime in Settings

YOUR DATA STAYS WITH YOU
• No data collection, no tracking, no ads
• Your login details stay encrypted on your device (secure keychain)

IMPORTANT: FluxTV is a media player. It does not provide, host or sell any content, channels or subscriptions. You must have your own sources and the rights needed to watch them.
```

### Nouveautés de cette version
Pas demandé pour la toute première version publiée sur l'App Store.

### Copyright
`2026 FluxTV`

### Informations utiles à la vérification de l'app
- **Connexion requise** : cochée d'office, **à décocher** : l'équipe d'Apple utilise le mode démo.
- **Coordonnées** : ton prénom, ton nom, ton téléphone et ton e-mail. Apple ne les affiche pas
  sur la fiche. Tu les remplis toi-même, ne les envoie à personne (Claude compris).
- **Remarques** : copier le texte de `Notes-validation.md` (en anglais).

### Publication de la version dans l'App Store
Cocher « Publier cette version manuellement » (« automatiquement » est coché d'office) pour
choisir le jour de la sortie.

---

## À décider (Claude te guide pour chaque point)

1. ✅ **Page « Assistance »** : en ligne le 4 novembre 2026, avec l'adresse
   `fluxtv.support@gmail.com` (réservée à FluxTV).
2. **Prix** : conseil, **gratuit** (comme sur Android et Xbox). Une app payante demande en plus
   le contrat « Apps payantes » d'Apple, avec coordonnées bancaires et fiscales.
3. **Classification par âge** : répondre honnêtement au questionnaire (aucune violence, aucun
   contenu sexuel, etc., dans l'app elle-même ; contrôle parental : oui, « Masquer les contenus
   pour adultes », activé par défaut). Apple se méfie des lecteurs IPTV, qui peuvent lire
   n'importe quelle source : choisir ensuite une classification plus élevée (**18+**) évite le
   plus souvent un refus. Contrepartie : l'app est masquée sur les appareils d'enfants.
4. **Statut de commerçant (DSA, Union européenne)** : obligatoire pour être visible dans l'UE.
   « Commerçant » affiche ton adresse, ton téléphone et ton e-mail sur la fiche. Pour une app
   gratuite faite à titre personnel, sans vente ni publicité, « Non commerçant » est en général
   la bonne réponse : c'est à toi de juger ta situation.

## Captures : comment elles sont faites
Sur des Mac de GitHub (workflow « App Store - préparation », branche `appstore`), avec la
démonstration : aucune chaîne protégée à l'écran, uniquement des films libres de la Blender
Foundation. Captures brutes : branche `ci-appstore`. Titre ajouté au-dessus des captures iPhone
et iPad par `AppStore/Captures/montage.py` (polices et couleurs de l'app).
