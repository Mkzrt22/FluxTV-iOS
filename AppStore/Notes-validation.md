# Notes pour l'équipe de validation d'Apple

À coller dans App Store Connect › version 1.2 › Informations pour la vérification de l'app ›
Notes (même texte pour iOS et tvOS). En anglais : l'équipe de validation travaille en anglais.
« Connexion requise » : décochée.

---

FluxTV is a media player for the user's OWN playlists (M3U links or Xtream Codes accounts provided by their TV service). The app does not provide, host, sell or link to any channel or content. No account is needed to review it.

HOW TO TEST (DEMO MODE, NO LOGIN)
1. At first launch, choose "English".
2. On the "Welcome" screen, tap "Try the demo".
The demo only uses public test streams and open movies:
- 3 live channels: a live test pattern (Unified Streaming demo stream) and two open films streamed as channels (Mux test streams);
- 2 movies and 1 demo series made of the Blender Foundation's open films (Creative Commons Attribution): Big Buck Bunny and Tears of Steel.

WHERE TO LOOK
- "Live TV" tab: tap a channel. The up/down buttons (iPhone, iPad) or up/down on the Siri Remote (Apple TV) switch channels.
- "Movies" and "Series" tabs: open a title, then "Play".
- Resume: stop a movie, it appears under "Continue watching" on the "Home" tab.
- Settings: gear button on the Home screen (iPhone, iPad) or "Settings" tab (Apple TV). The app language can be changed there.

PRIVACY
No data collection, no analytics, no advertising, no tracking. Login details entered by the user are stored in the Keychain on the device and are only sent to the server chosen by the user. Adult categories are hidden by default.

NETWORK
Many TV services use plain HTTP servers, often on the user's local network: NSAllowsArbitraryLoads is enabled so that users can play their own streams.

THIRD-PARTY CODE
Video playback uses VLCKit (VideoLAN, LGPL 2.1), as a dynamically linked framework.
