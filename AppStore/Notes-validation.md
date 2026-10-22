# Notes pour l'équipe de validation d'Apple

À coller dans App Store Connect › Version › Informations pour la vérification de l'app › Notes.
(En anglais : l'équipe de validation travaille en anglais.)

---

FluxTV is a media player for the user's OWN playlists (M3U links or Xtream Codes accounts provided by their TV service). The app does not provide, host, sell or link to any channel or content.

HOW TO TEST WITHOUT AN ACCOUNT
On the first screen, tap "Essayer la démo" (Try the demo). The demo mode loads:
- 3 public test live streams (Akamai and Apple HLS test streams),
- 2 free movies and 1 demo series from the Blender Foundation (Creative Commons BY): Big Buck Bunny, Tears of Steel.
No login is required.

- Live TV: "TV" tab > tap a channel. Up/down buttons (iPhone) or up/down on the Siri Remote (Apple TV) switch channels.
- Movies / Series: "Films" / "Séries" tabs > open a title > "Lecture".
- Resume: stop a movie, it appears in "Continuer à regarder" on the home screen.

PRIVACY
The app collects no data, has no analytics and no advertising. Credentials entered by the user are stored in the iOS Keychain on the device and are only sent to the server chosen by the user.

NETWORK
Many TV services use plain HTTP servers: NSAllowsArbitraryLoads is enabled so that users can play their own streams.

THIRD-PARTY CODE
Video playback uses VLCKit (VideoLAN, LGPL 2.1).
