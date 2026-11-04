# FluxTV Player — soumission au Microsoft Store (Xbox)

Produit réservé dans l'Espace partenaires : **FluxTV Player** (ID 9P4NX39QHPW1).
Éditeur : Kevin22 — identité du paquet `Kevin22.FluxTVPlayer`.

Fichiers fournis :

| Fichier | Rôle |
|---|---|
| `FluxTV-Player-Store.msixupload` | le paquet à déposer (version publique, **sans** ton compte) |
| `Captures/*.png` | captures d'écran 1920 × 1080 (mode démonstration) ; `*-en.png` : les mêmes en anglais |
| `Images/*.png` | affiche, boîte, icône et visuels Xbox ; `*-en.png` : versions anglaises (slogan traduit) |
| `Notes-certification.txt` | texte (en anglais) pour l'équipe de certification |
| `confidentialite.html` | politique de confidentialité à mettre en ligne |
| `rapport-wack.txt` | résultat du kit de certification Windows (même contrôle que le Store) |

Ordre conseillé dans l'Espace partenaires (partner.microsoft.com › Apps et jeux › FluxTV Player ›
« Démarrer votre soumission »). Chaque section passe en « Terminé » une fois remplie.

---

## 1. Tarification et disponibilité

- **Marchés** : tous, ou seulement France / Belgique / Suisse / Canada si tu préfères commencer petit.
- **Visibilité** : « Public » (ou « Privé » pour un premier essai : seuls les comptes que tu indiques la voient).
- **Prix** : « Gratuit », ou un prix fixe (ex. 4,99 €). Pour vendre, remplis d'abord le profil de
  paiement et le formulaire fiscal (Espace partenaires › Paramètres du compte › Paiement et taxes).
  Microsoft prélève une commission sur chaque vente ; le taux appliqué s'affiche dans l'Espace partenaires.
- **Publication** : « Dès que possible après la certification », ou manuelle pour choisir le jour.

## 2. Propriétés

- **Catégorie** : Divertissement (sous-catégorie : aucune).
- **Confidentialité** : « Oui », l'app accède à des informations personnelles (les identifiants de
  l'abonnement, transmis uniquement au serveur choisi) → **URL de la politique de confidentialité** :
  `https://mkzrt22.github.io/FluxTV-iOS/confidentialite/` (page du site, valable pour toutes les
  versions de FluxTV ; un visiteur anglophone arrive sur la version anglaise, `…/en/privacy/`).
- **Site web** : ton site (facultatif).
- **Coordonnées du support** : une adresse e-mail ou une page de contact à toi (elle sera publique).
- **Configuration requise** : cocher « Manette Xbox » en *minimum*. Rien d'autre.
- **Déclarations du produit** : laisser les valeurs par défaut (installation sur disque externe autorisée,
  sauvegarde OneDrive autorisée). Ne coche pas « testé pour l'accessibilité ».

## 3. Classifications par âge (questionnaire IARC)

- Type : **application** (pas un jeu), catégorie « Divertissement / diffusion de contenus ».
- Réponds honnêtement. Points propres à FluxTV Player :
  - pas d'échanges entre utilisateurs, pas de partage de position, pas d'achats dans l'app ;
  - **accès à des contenus Internet non filtrés : oui** (l'app lit les sources de l'utilisateur) ;
  - contenus fournis avec l'app : uniquement la démonstration (courts métrages libres de la Blender
    Foundation : Big Buck Bunny, animation burlesque ; Tears of Steel, science-fiction avec des scènes
    de combat contre des robots).
- La classification obtenue (souvent 12+ ou 16+ pour ce type de lecteur) s'applique automatiquement.

## 4. Packages

- Glisse `FluxTV-Player-Store.msixupload` dans la zone de dépôt.
- **Familles d'appareils** : coche **Xbox**. Tu peux aussi cocher **Windows 10/11 Desktop** : l'app
  fonctionne sur PC (affichage adapté, souris et clavier, Échap = retour). Décoche HoloLens et Team.
- Le Store signe lui-même le paquet : ne pas utiliser le certificat de test pour cette version.

## 5. Fiche du Store — Français (France)

Depuis la version 1.1, le paquet déclare **l'anglais et le français** : après le dépôt du paquet,
l'Espace partenaires affiche deux fiches, « Français (France) » et « Anglais (États-Unis) ». Remplis
les deux (la fiche anglaise est au point 5 bis). L'app, elle, demande la langue au premier lancement.

### Description (10 000 caractères max)

```
FluxTV Player est un lecteur élégant, pensé pour la télévision, pour regarder VOS propres sources : chaînes en direct, guide des programmes, films et séries, sur votre Xbox.

AJOUTEZ VOTRE SOURCE
• Compte Xtream (serveur, utilisateur, mot de passe) ou lien de playlist M3U
• Guide des programmes XMLTV pris en charge
• Mode démonstration pour découvrir l'app sans abonnement

TV EN DIRECT
• Catégories claires, chaînes favorites et récentes
• Programme en cours et suivant, avec barre d'avancement
• Zapping à la manette et liste des chaînes pendant la lecture

FILMS ET SÉRIES
• Grandes affiches, fiches détaillées, saisons et épisodes
• Reprise automatique là où vous vous étiez arrêté
• Épisode suivant enchaîné automatiquement

UN LECTEUR FAIT POUR LA TÉLÉ
• Tout se pilote à la manette, menus lisibles depuis le canapé
• Choix de la piste audio et des sous-titres
• Fréquence de la télé adaptée à la vidéo (24, 50 ou 60 Hz) pour une image fluide

VOS DONNÉES RESTENT CHEZ VOUS
• Aucune collecte de données, aucun pistage, aucune publicité
• Vos identifiants restent chiffrés sur votre console

SOUTENIR FLUXTV (FACULTATIF)
• Toutes les fonctions sont gratuites
• Achat intégré facultatif : le Pack Soutien (1,99 €, payé une fois) aide au développement et débloque en remerciement le thème Or et le badge Soutien

IMPORTANT : FluxTV Player est un lecteur multimédia. Il ne fournit, n'héberge et ne vend aucun contenu, aucune chaîne ni aucun abonnement. Vous devez disposer de vos propres sources et des droits nécessaires pour les regarder.
```

### Nouveautés de cette version

Version 1.3 (si la 1.2 est déjà publiée) :

```
Nouveau : un bouton « Noter FluxTV » dans Réglages › Soutenir FluxTV. Après quelques jours d'utilisation, l'app propose aussi, une seule fois, de la noter sur le Microsoft Store.
```

Version 1.3 (si la 1.2 n'a pas encore été publiée : la 1.3 la remplace) :

```
Nouveau : le Pack Soutien, facultatif, dans Réglages › Soutenir FluxTV. En remerciement, il débloque le thème Or et le badge Soutien. Toutes les fonctions restent gratuites. Et un bouton « Noter FluxTV » pour donner ton avis sur le Microsoft Store.
```

Version 1.2 :

```
Nouveau : le Pack Soutien, facultatif, dans Réglages › Soutenir FluxTV. En remerciement, il débloque le thème Or et le badge Soutien. Toutes les fonctions restent gratuites.
```

Version 1.1 (si la 1.0 a déjà été publiée) :

```
FluxTV Player parle maintenant français et anglais : la langue est demandée au premier lancement et se change à tout moment dans les réglages.
```

Si c'est ta toute première soumission (version 1.1 publiée directement) :

```
Première version de FluxTV Player sur Xbox, en français et en anglais.
```

### Fonctionnalités du produit (une par ligne, 20 max)

```
TV en direct avec guide des programmes
Films et séries avec reprise automatique
Comptes Xtream et playlists M3U
Pilotage complet à la manette
Pistes audio et sous-titres au choix
Fréquence de la télé adaptée à la vidéo
Favoris et chaînes récentes
Recherche dans les chaînes, films et séries
Contenus pour adultes masqués par défaut
Mode démonstration sans abonnement
```

### Captures d'écran (Xbox, PNG 1920 × 1080, 1 à 10)

Dépose les fichiers du dossier `Captures` dans cet ordre, avec ces légendes :

| Fichier | Légende |
|---|---|
| `home.png` | Toutes vos chaînes, films et séries au même endroit |
| `live.png` | La TV en direct avec le guide des programmes |
| `play-channels.png` | Zappez sans quitter la lecture |
| `movies.png` | Vos films en grandes affiches |
| `movie.png` | Fiches détaillées et reprise automatique |
| `seriesdetail.png` | Saisons et épisodes, enchaînés automatiquement |
| `play-movie.png` | Un lecteur pensé pour la télé |
| `search.png` | Une recherche instantanée |
| `intro.png` | FluxTV Player |

### Logos du Store et visuels Xbox (dossier `Images`)

| Emplacement dans l'Espace partenaires | Fichier |
|---|---|
| Affiche 9:16 (720 × 1080 ou 1440 × 2160) | `Affiche-1440x2160.png` |
| Boîte 1:1 (1080 × 1080 ou 2160 × 2160) | `Boite-2160x2160.png` |
| Icône de l'app 1:1 (300 × 300) | `Icone-300x300.png` |
| Xbox — illustration principale avec titre 16:9 | `Xbox-Hero-1920x1080.png` (ou `Xbox-Hero-3840x2160.png`) |
| Xbox — illustration de la marque (584 × 800) | `Xbox-Key-art-584x800.png` |
| Xbox — illustration promotionnelle carrée (1080 × 1080) | `Xbox-Promo-1080x1080.png` |

### Informations supplémentaires

- **Titre abrégé** : FluxTV Player
- **Titre vocal** : Flux T V Player
- **Termes de recherche** (7 max) : `lecteur iptv`, `m3u`, `xtream`, `tv en direct`, `guide tv`, `films`, `séries`
- **Copyright** : © 2026 Kevin22
- **Développé par** : Kevin22
- **Configuration matérielle supplémentaire** : « Abonnement ou playlist IPTV personnel requis (non fourni). »

## 5 bis. Fiche du Store — Anglais (English, United States)

Même structure que la fiche française, avec ces textes.

### Description

```
FluxTV Player is an elegant player designed for the TV, to watch YOUR own sources: live channels, TV guide, movies and series, on your Xbox.

ADD YOUR SOURCE
• Xtream account (server, username, password) or M3U playlist link
• XMLTV program guide supported
• Demo mode to explore the app without a subscription

LIVE TV
• Clear categories, favorite and recent channels
• Current and next program, with a progress bar
• Channel surfing with the controller, and the channel list during playback

MOVIES AND SERIES
• Big posters, detailed pages, seasons and episodes
• Automatically resumes where you left off
• Next episode plays automatically

A PLAYER BUILT FOR THE TV
• Everything works with the controller, menus readable from the couch
• Choice of audio track and subtitles
• TV refresh rate matched to the video (24, 50 or 60 Hz) for smooth motion

IN ENGLISH AND FRENCH
• Language asked at first launch, changeable anytime in Settings

YOUR DATA STAYS WITH YOU
• No data collection, no tracking, no ads
• Your credentials stay encrypted on your console

SUPPORT FLUXTV (OPTIONAL)
• Every feature is free
• Optional in-app purchase: the Supporter Pack (one-time purchase) supports development and unlocks the Gold theme and the Supporter badge as a thank-you

IMPORTANT: FluxTV Player is a media player. It does not provide, host or sell any content, channels or subscriptions. You must have your own sources and the rights needed to watch them.
```

### What's new in this version

Version 1.3 (if 1.2 is already published):

```
New: a “Rate FluxTV” button in Settings › Support FluxTV. After a few days of use, the app also offers, just once, to rate it on the Microsoft Store.
```

Version 1.3 (if 1.2 isn't published yet: 1.3 replaces it):

```
New: the optional Supporter Pack, in Settings › Support FluxTV. As a thank-you, it unlocks the Gold theme and the Supporter badge. Every feature stays free. Plus a “Rate FluxTV” button to share your opinion on the Microsoft Store.
```

Version 1.2:

```
New: the optional Supporter Pack, in Settings › Support FluxTV. As a thank-you, it unlocks the Gold theme and the Supporter badge. Every feature stays free.
```

Version 1.1 (if 1.0 was already published):

```
FluxTV Player now speaks English and French: the language is asked at first launch and can be changed anytime in Settings.
```

First submission:

```
First release of FluxTV Player on Xbox, in English and French.
```

### Product features

```
Live TV with program guide
Movies and series with auto-resume
Xtream accounts and M3U playlists
Full controller navigation
Choice of audio tracks and subtitles
TV refresh rate matched to the video
Favorites and recent channels
Search across channels, movies and series
Adult content hidden by default
Demo mode, no subscription needed
In English and French
```

### Screenshots (Xbox, PNG 1920 × 1080) — files `Captures/*-en.png`

| File | Caption |
|---|---|
| `home-en.png` | All your channels, movies and series in one place |
| `live-en.png` | Live TV with the program guide |
| `play-channels-en.png` | Switch channels without leaving playback |
| `movies-en.png` | Your movies as big posters |
| `movie-en.png` | Detailed pages and auto-resume |
| `seriesdetail-en.png` | Seasons and episodes, played back to back |
| `play-movie-en.png` | A player designed for the TV |
| `search-en.png` | Instant search |
| `intro-en.png` | FluxTV Player |

### Store logos and Xbox images

Same files as the French listing, except the two that carry the tagline:
`Affiche-1440x2160-en.png` (poster art) and `Xbox-Hero-1920x1080-en.png` (or `Xbox-Hero-3840x2160-en.png`,
titled hero art).

### Additional information

- **Short title**: FluxTV Player
- **Voice title**: Flux T V Player
- **Search terms** (7 max): `iptv player`, `m3u`, `xtream`, `live tv`, `tv guide`, `movies`, `series`
- **Copyright**: © 2026 Kevin22
- **Developed by**: Kevin22
- **Additional system requirements**: "Personal IPTV subscription or playlist required (not provided)."

## 6. Options de soumission

- **Notes pour la certification** : colle le texte de `Notes-certification.txt` (en anglais).
- **Options de publication** : comme choisi à l'étape 1.

## 7. Envoyer

« Soumettre au Store ». La certification prend en général 1 à 3 jours ouvrés. En cas de refus, le
rapport indique la règle concernée et l'écran fautif : envoie-le-moi, je corrige et je refais le paquet.

---

## Mises à jour de l'app

Chaque nouvelle version doit avoir un numéro plus grand (1.0.1.0, 1.1.0.0…) : il est dans
`Package.appxmanifest`. Le serveur de compilation produit à chaque fois un nouveau
`FluxTV-Player-Store.msixupload` ; dans l'Espace partenaires, « Mettre à jour » la soumission puis
remplacer le paquet.

## Extension « Pack Soutien » (version 1.2)

Achat intégré facultatif, **payé une fois** : il débloque en remerciement le thème Or et le badge
Soutien ; toutes les fonctions restent gratuites. La règle 10.8.2 du Store n'autorise pas les dons
purs (qui ne donnent rien en échange) par une extension : c'est pourquoi le pack débloque ce bonus.

1. Dans FluxTV Player › **Extensions** (Add-ons) › **Créer une extension** :
   type **Durable**, identifiant du produit **`packsoutien`** (exactement, c'est celui de l'app).
2. **Propriétés** : durée de vie « Pour toujours » ; type de contenu « Téléchargement de logiciel
   électronique » (Electronic software download).
3. **Tarification et disponibilité** : 1,99 € ; disponible.
4. **Classification par âge** : même questionnaire que l'app.
5. **Fiches du Store** : « Ajouter/supprimer des langues » › Français (France) et Anglais (États-Unis).
   - Français — titre : `Pack Soutien` ; description :
     `Soutiens le développement de FluxTV et débloque en remerciement le thème Or et le badge Soutien. Toutes les fonctions de l'app restent gratuites.`
   - Anglais — titre : `Supporter Pack` ; description :
     `Support FluxTV's development and unlock the Gold theme and the Supporter badge as a thank-you. Every feature of the app stays free.`
   - Icône (les deux langues) : `Images/PackSoutien-300x300.png`.
6. **Soumettre au Store** (certification en général en une heure environ).

Ensuite seulement, la mise à jour de l'app : nouvelle soumission de FluxTV Player avec le paquet 1.2,
description et nouveautés ci-dessus. Si le questionnaire de classification demande si l'app propose
des achats, réponds **oui**. L'ancienne extension « soutien1 » (consommable) reste non publiée :
supprime simplement son brouillon de soumission.

## Invitation à noter l'app (version 1.3)

Les règles du Microsoft Store interdisent d'influencer les notes : l'app ne demande donc jamais
« 5 étoiles », ne filtre pas les avis et n'offre rien en échange. Elle propose simplement, **une seule
fois**, de la noter, à qui s'en sert vraiment :

- au moins **3 jours différents** de visionnage et **2 heures** regardées en tout (la démonstration ne
  compte pas, ni ta copie perso) ;
- à la fermeture d'une lecture d'au moins **10 minutes** qui s'est bien passée, jamais pendant une vidéo ;
- carte « FluxTV te plaît ? » : « Noter FluxTV » ouvre la fenêtre de note du Store (la personne choisit
  sa note, de 1 à 5 étoiles), « Non merci » ou Ⓑ referme, et la question ne revient plus ;
- le bouton « Noter FluxTV » reste toujours dans Réglages › Soutenir FluxTV.

Rien à configurer dans l'Espace partenaires : les notes arrivent dans **Avis** (Reviews) de FluxTV Player.
Envoie la 1.3 une fois la 1.2 publiée (ou, si la 1.2 est refusée, directement la 1.3 à sa place).

## Ta copie personnelle

La version du Store ne contient pas ton compte : tu le saisis une fois sur la console (il reste
chiffré). Ta copie perso (installée par le Device Portal) ne fonctionne qu'en mode développeur ;
la version du Store s'utilise en mode normal (Retail), comme n'importe quelle app.
