# FluxTV Player — soumission au Microsoft Store (Xbox)

Produit réservé dans l'Espace partenaires : **FluxTV Player** (ID 9P4NX39QHPW1).
Éditeur : Kevin22 — identité du paquet `Kevin22.FluxTVPlayer`.

Fichiers fournis :

| Fichier | Rôle |
|---|---|
| `FluxTV-Player-Store.msixupload` | le paquet à déposer (version publique, **sans** ton compte) |
| `Captures/*.png` | captures d'écran 1920 × 1080 (mode démonstration) |
| `Images/*.png` | affiche, boîte, icône et visuels Xbox |
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
  mets `confidentialite.html` en ligne sur ton VPS (dossier du site), par exemple
  `http://IP_DU_VPS:8080/confidentialite.html`, puis colle l'adresse.
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

IMPORTANT : FluxTV Player est un lecteur multimédia. Il ne fournit, n'héberge et ne vend aucun contenu, aucune chaîne ni aucun abonnement. Vous devez disposer de vos propres sources et des droits nécessaires pour les regarder.
```

### Nouveautés de cette version

```
Première version de FluxTV Player sur Xbox.
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

## Ta copie personnelle

La version du Store ne contient pas ton compte : tu le saisis une fois sur la console (il reste
chiffré). Ta copie perso (installée par le Device Portal) ne fonctionne qu'en mode développeur ;
la version du Store s'utilise en mode normal (Retail), comme n'importe quelle app.
