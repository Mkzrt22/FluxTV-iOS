# Envoi de FluxTV à Apple (TestFlight, puis validation)

L'envoi se fait depuis GitHub, sur un Mac de GitHub : pas besoin de Mac ni de certificat. Apple
signe l'app lui-même au moment de l'envoi. Il faut seulement une **clé d'API App Store
Connect**, rangée dans les secrets du dépôt GitHub.

Le workflow « App Store - envoi » ne part **jamais tout seul** : il faut la clé dans les secrets,
le workflow sur la branche `main`, et un lancement à la main avec le mot `ENVOYER`.

## 1. Créer la clé d'API (une seule fois)

1. https://appstoreconnect.apple.com › **Utilisateurs et accès** › onglet **Intégrations** ›
   **API App Store Connect** › **Clés d'équipe** (Team Keys).
2. La première fois : **Demander l'accès** (Request Access), accepter les conditions.
3. **Générer une clé API** (bouton « + ») : nom `GitHub FluxTV`, accès **Admin**
   (indispensable pour qu'Apple signe l'app).
4. **Télécharger** le fichier `AuthKey_XXXXXXXXXX.p8`. Apple ne le propose **qu'une seule
   fois** : le ranger à l'abri (avec tes sauvegardes).
5. Noter les deux identifiants affichés sur la page :
   - **ID de la clé** (Key ID, 10 caractères, dans le tableau) ;
   - **ID de l'émetteur** (Issuer ID, en haut de la page, au format
     `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`).

Ne jamais envoyer la clé ni ces identifiants à quelqu'un, **Claude compris** : tu les colles
toi-même dans GitHub.

## 2. Les ranger dans GitHub

https://github.com/Mkzrt22/FluxTV-iOS › **Settings** › **Secrets and variables** › **Actions**
› **New repository secret**, trois fois :

| Name | Secret |
|---|---|
| `ASC_KEY_ID` | l'ID de la clé |
| `ASC_ISSUER_ID` | l'ID de l'émetteur |
| `ASC_KEY_P8` | tout le contenu du fichier `.p8` : ouvre-le avec le Bloc-notes et copie tout, y compris les lignes `-----BEGIN PRIVATE KEY-----` et `-----END PRIVATE KEY-----` |

GitHub ne réaffiche jamais un secret, et il n'apparaît pas dans les journaux.

## 3. Lancer l'envoi (avec l'accord donné à Claude)

1. Claude met le workflow sur `main` (après ton accord).
2. https://github.com/Mkzrt22/FluxTV-iOS/actions › **App Store - envoi** › **Run workflow** ›
   écrire `ENVOYER` › plateformes `les-deux` › **Run workflow**.
3. Environ 20 à 30 minutes : fabrication, contrôles (version, icônes, aucun compte intégré),
   signature par Apple et envoi. Si un contrôle échoue, rien n'est envoyé.

## 4. Ensuite, dans App Store Connect

1. **TestFlight** : les builds iPhone/iPad et Apple TV apparaissent après le traitement par
   Apple (10 à 30 minutes, un e-mail arrive). La question sur le chiffrement est déjà réglée
   dans l'app.
2. Conseillé : installer l'app **TestFlight** sur ton iPhone et ton Apple TV, et essayer FluxTV
   avec ton abonnement avant de la soumettre (tu es testeur interne d'office).
3. Page de la version **1.2** (iOS, puis tvOS) › section **Build** › « + » › choisir le build ›
   **Terminé** › **Enregistrer**.
4. Tout est rempli (voir `Fiche-App-Store.md`) : **Ajouter pour vérification** › **Soumettre**.
   Réponse d'Apple : en général 1 à 3 jours.

## En cas de refus
Envoyer à Claude le message d'Apple (Centre de résolution), sans tes coordonnées.
