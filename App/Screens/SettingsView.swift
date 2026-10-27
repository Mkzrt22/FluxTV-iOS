import SwiftUI

/// Réglages : source, langue, lecture, contenus, données, à propos.
struct SettingsView: View {
    @EnvironmentObject private var app: AppModel
    @State private var confirmSignOut = false
    @State private var confirmClear = false
    @State private var refreshing = false

    // Réglages : liaisons explicites (l'objet des réglages est partagé par toute l'app)
    private var autoplay: Binding<Bool> { Binding(get: { app.settings.autoplayNext }, set: { app.settings.autoplayNext = $0 }) }
    private var liveFormat: Binding<String> { Binding(get: { app.settings.liveFormat }, set: { app.settings.liveFormat = $0 }) }
    private var caching: Binding<Int> { Binding(get: { app.settings.networkCaching }, set: { app.settings.networkCaching = $0 }) }
    private var hideAdult: Binding<Bool> { Binding(get: { app.settings.hideAdult }, set: { app.settings.hideAdult = $0 }) }
    private var language: Binding<String> { Binding(get: { app.language }, set: { app.changeLanguage($0) }) }

    private var version: String {
        let v = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0"
        let b = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "1"
        return "\(v) (\(b))"
    }

    var body: some View {
        List {
            Section {
                sourceRow
                if let info = app.account {
                    if let exp = info.expiry {
                        row(T("Fin de l'abonnement", "Subscription ends"), Format.day.string(from: exp).capitalizedFirst)
                    } else {
                        row(T("Fin de l'abonnement", "Subscription ends"), T("Illimité", "Never"))
                    }
                    if let max = info.maxConnections {
                        row(T("Connexions simultanées", "Simultaneous connections"), "\(info.activeConnections ?? 0) / \(max)")
                    }
                }
                Button {
                    refreshing = true
                    Task {
                        await app.refreshAll()
                        await app.refreshAccount()
                        refreshing = false
                    }
                } label: {
                    HStack {
                        Label(T("Actualiser les listes", "Refresh lists"), systemImage: "arrow.clockwise")
                        Spacer()
                        if refreshing { ProgressView() }
                    }
                }
                .disabled(refreshing)
                Button(role: .destructive) { confirmSignOut = true } label: {
                    Label(T("Changer de source", "Change source"), systemImage: "rectangle.portrait.and.arrow.right")
                }
            } header: { Text(verbatim: "Source") }

            // Titre dans les deux langues, pour s'y retrouver même dans l'autre
            Section {
                Picker(selection: language) {
                    Text(verbatim: "Français").tag(Lang.fr)
                    Text(verbatim: "English").tag(Lang.en)
                } label: { Label(T("Langue de l'app", "App language"), systemImage: "globe") }
                .tvPickerStyle()
            } header: { Text(verbatim: "Langue · Language") }

            Section {
                Toggle(isOn: autoplay) { Label(T("Enchaîner les épisodes", "Autoplay next episode"), systemImage: "forward.end") }
                Picker(selection: liveFormat) {
                    Text(T("Automatique", "Automatic")).tag("auto")
                    Text(T("MPEG-TS (le plus compatible)", "MPEG-TS (most compatible)")).tag("ts")
                    Text(verbatim: "HLS").tag("m3u8")
                } label: { Label(T("Format du direct", "Live format"), systemImage: "antenna.radiowaves.left.and.right") }
                .tvPickerStyle()
                Picker(selection: caching) {
                    Text(T("Courte (1 s)", "Short (1 s)")).tag(1000)
                    Text(T("Normale (1,5 s)", "Normal (1.5 s)")).tag(1500)
                    Text(T("Longue (3 s)", "Long (3 s)")).tag(3000)
                    Text(T("Très longue (6 s)", "Very long (6 s)")).tag(6000)
                } label: { Label(T("Mémoire tampon", "Buffer"), systemImage: "timer") }
                .tvPickerStyle()
            } header: { Text(T("Lecture", "Playback")) } footer: {
                Text(T("Si le direct coupe souvent, choisis une mémoire tampon plus longue.", "If live TV often cuts out, choose a longer buffer."))
            }

            Section {
                Toggle(isOn: hideAdult) { Label(T("Masquer les contenus pour adultes", "Hide adult content"), systemImage: "eye.slash") }
            } header: { Text(T("Contenus", "Content")) }

            Section {
                Button(role: .destructive) { confirmClear = true } label: {
                    Label(T("Effacer l'historique, les reprises et les favoris", "Clear history, resume points and favorites"), systemImage: "trash")
                }
            } header: { Text(T("Mes données", "My data")) } footer: {
                Text(T("Favoris, reprises et historique restent uniquement sur cet appareil.", "Favorites, resume points and history stay on this device only."))
            }

            Section {
                row("Version", version)
                NavigationLink {
                    LegalView()
                } label: { Label(T("Licences et confidentialité", "Licenses and privacy"), systemImage: "doc.text") }
            } header: { Text(T("À propos", "About")) } footer: {
                Text(T("FluxTV est un lecteur : il ne fournit aucune chaîne ni aucun contenu. Utilise uniquement des sources auxquelles tu as légalement accès.",
                       "FluxTV is a player: it provides no channels or content. Only use sources you have legal access to."))
            }
        }
        #if os(iOS)
        .listStyle(.insetGrouped)
        .scrollContentBackground(.hidden)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .fluxScreen()
        .navigationTitle(T("Réglages", "Settings"))
        .confirmationDialog(T("Changer de source ?", "Change source?"), isPresented: $confirmSignOut, titleVisibility: .visible) {
            Button(T("Changer de source", "Change source"), role: .destructive) { app.signOut() }
            Button(T("Annuler", "Cancel"), role: .cancel) {}
        } message: {
            Text(T("Ton compte sera retiré de cet appareil. Tes favoris et tes reprises sont conservés.",
                   "Your account will be removed from this device. Your favorites and resume points are kept."))
        }
        .confirmationDialog(T("Tout effacer ?", "Clear everything?"), isPresented: $confirmClear, titleVisibility: .visible) {
            Button(T("Effacer", "Clear"), role: .destructive) { app.library.clearAll() }
            Button(T("Annuler", "Cancel"), role: .cancel) {}
        }
    }

    @ViewBuilder private var sourceRow: some View {
        switch app.source {
        case .xtream(let a)?:
            row(T("Compte Xtream", "Xtream account"), a.username)
            row(T("Serveur", "Server"), URLComponents(string: a.server)?.host ?? a.server)
        case .playlist(let url, _)?:
            row("Playlist", URLComponents(string: url)?.host ?? url)
        case .demo?:
            row("Source", T("Démonstration", "Demo"))
        case nil:
            row("Source", T("Aucune", "None"))
        }
    }

    private func row(_ title: String, _ value: String) -> some View {
        HStack {
            Text(title).foregroundStyle(Theme.text)
            Spacer()
            Text(value).foregroundStyle(Theme.textDim).lineLimit(1)
        }
    }
}

/// Licences des composants et politique de confidentialité.
struct LegalView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(18)) {
                block(T("Confidentialité", "Privacy"), T("""
                FluxTV ne collecte aucune donnée. Pas de compte FluxTV, pas de publicité, pas de mesure d'audience.
                Ton abonnement (adresse du serveur, identifiants) est enregistré uniquement sur cet appareil, chiffré (trousseau sécurisé d'Apple ou, à défaut, fichier chiffré par le système), et n'est envoyé qu'au serveur de ton fournisseur.
                Favoris, reprises et historique restent sur cet appareil.
                """, """
                FluxTV collects no data. No FluxTV account, no ads, no analytics.
                Your subscription (server address, login details) is stored only on this device, encrypted (Apple's secure keychain or, failing that, a file encrypted by the system), and is only sent to your provider's server.
                Favorites, resume points and history stay on this device.
                """))
                block(T("Contenus", "Content"), T("""
                FluxTV est un lecteur multimédia. Il ne fournit, n'héberge et ne vend aucune chaîne, aucun film ni aucune série. Tu es responsable des sources que tu ajoutes : utilise uniquement des contenus auxquels tu as légalement accès.
                La démonstration utilise une mire de test publique (Unified Streaming), des flux de test hébergés par Mux et des films libres de droits de la Blender Foundation : Big Buck Bunny (2008) et Tears of Steel (2012), © Blender Foundation, blender.org, licence Creative Commons Attribution 3.0.
                """, """
                FluxTV is a media player. It does not provide, host or sell any channels, movies or series. You are responsible for the sources you add: only use content you have legal access to.
                The demo uses a public test pattern (Unified Streaming), test streams hosted by Mux and free-licensed films from the Blender Foundation: Big Buck Bunny (2008) and Tears of Steel (2012), © Blender Foundation, blender.org, Creative Commons Attribution 3.0 license.
                """))
                block(T("Lecture vidéo : VLCKit", "Video playback: VLCKit"), T("""
                Lecture assurée par VLCKit (MobileVLCKit / TVVLCKit), © VideoLAN et les auteurs de VLC, distribué sous licence GNU LGPL 2.1. Code source : https://code.videolan.org/videolan/VLCKit
                """, """
                Playback by VLCKit (MobileVLCKit / TVVLCKit), © VideoLAN and the VLC authors, distributed under the GNU LGPL 2.1 license. Source code: https://code.videolan.org/videolan/VLCKit
                """))
                block(T("Polices Sora et Manrope", "Sora and Manrope fonts"), T("""
                Sora © The Sora Project Authors, Manrope © The Manrope Project Authors. Licence SIL Open Font License 1.1.
                """, """
                Sora © The Sora Project Authors, Manrope © The Manrope Project Authors. SIL Open Font License 1.1.
                """))
            }
            .padding(Theme.pagePadding)
            .frame(maxWidth: Theme.s(820), alignment: .leading)
        }
        .fluxScreen()
        .navigationTitle(T("Licences et confidentialité", "Licenses and privacy"))
    }

    private func block(_ title: String, _ text: String) -> some View {
        VStack(alignment: .leading, spacing: Theme.s(6)) {
            Text(title).font(Theme.title(17)).foregroundStyle(Theme.text)
            Text(text).font(Theme.bodyRegular(14)).foregroundStyle(Theme.textDim).fixedSize(horizontal: false, vertical: true)
        }
    }
}

extension View {
    /// Apple TV : un choix s'ouvre dans sa propre liste (sinon ses options s'affichent sans titre).
    @ViewBuilder func tvPickerStyle() -> some View {
        #if os(tvOS)
        self.pickerStyle(.navigationLink)
        #else
        self
        #endif
    }
}

extension String {
    /// « jeudi 2 octobre » → « Jeudi 2 octobre » (l'anglais a déjà sa majuscule).
    var capitalizedFirst: String { prefix(1).uppercased() + dropFirst() }
}
