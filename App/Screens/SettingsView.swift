import SwiftUI

/// Réglages : source, lecture, contenus, données, à propos.
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
                        row("Fin de l'abonnement", Format.day.string(from: exp).capitalizedFirst)
                    } else {
                        row("Fin de l'abonnement", "Illimité")
                    }
                    if let max = info.maxConnections {
                        row("Connexions simultanées", "\(info.activeConnections ?? 0) / \(max)")
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
                        Label("Actualiser les listes", systemImage: "arrow.clockwise")
                        Spacer()
                        if refreshing { ProgressView() }
                    }
                }
                .disabled(refreshing)
                Button(role: .destructive) { confirmSignOut = true } label: {
                    Label("Changer de source", systemImage: "rectangle.portrait.and.arrow.right")
                }
            } header: { Text("Source") }

            Section {
                Toggle(isOn: autoplay) { Label("Enchaîner les épisodes", systemImage: "forward.end") }
                Picker(selection: liveFormat) {
                    Text("Automatique").tag("auto")
                    Text("MPEG-TS (le plus compatible)").tag("ts")
                    Text("HLS").tag("m3u8")
                } label: { Label("Format du direct", systemImage: "antenna.radiowaves.left.and.right") }
                .tvPickerStyle()
                Picker(selection: caching) {
                    Text("Courte (1 s)").tag(1000)
                    Text("Normale (1,5 s)").tag(1500)
                    Text("Longue (3 s)").tag(3000)
                    Text("Très longue (6 s)").tag(6000)
                } label: { Label("Mémoire tampon", systemImage: "timer") }
                .tvPickerStyle()
            } header: { Text("Lecture") } footer: {
                Text("Si le direct coupe souvent, choisis une mémoire tampon plus longue.")
            }

            Section {
                Toggle(isOn: hideAdult) { Label("Masquer les contenus pour adultes", systemImage: "eye.slash") }
            } header: { Text("Contenus") }

            Section {
                Button(role: .destructive) { confirmClear = true } label: {
                    Label("Effacer l'historique, les reprises et les favoris", systemImage: "trash")
                }
            } header: { Text("Mes données") } footer: {
                Text("Favoris, reprises et historique restent uniquement sur cet appareil.")
            }

            Section {
                row("Version", version)
                NavigationLink {
                    LegalView()
                } label: { Label("Licences et confidentialité", systemImage: "doc.text") }
            } header: { Text("À propos") } footer: {
                Text("FluxTV est un lecteur : il ne fournit aucune chaîne ni aucun contenu. Utilise uniquement des sources auxquelles tu as légalement accès.")
            }
        }
        #if os(iOS)
        .listStyle(.insetGrouped)
        .scrollContentBackground(.hidden)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .fluxScreen()
        .navigationTitle("Réglages")
        .confirmationDialog("Changer de source ?", isPresented: $confirmSignOut, titleVisibility: .visible) {
            Button("Changer de source", role: .destructive) { app.signOut() }
            Button("Annuler", role: .cancel) {}
        } message: {
            Text("Ton compte sera retiré de cet appareil. Tes favoris et tes reprises sont conservés.")
        }
        .confirmationDialog("Tout effacer ?", isPresented: $confirmClear, titleVisibility: .visible) {
            Button("Effacer", role: .destructive) { app.library.clearAll() }
            Button("Annuler", role: .cancel) {}
        }
    }

    @ViewBuilder private var sourceRow: some View {
        switch app.source {
        case .xtream(let a)?:
            row("Compte Xtream", a.username)
            row("Serveur", URLComponents(string: a.server)?.host ?? a.server)
        case .playlist(let url, _)?:
            row("Playlist", URLComponents(string: url)?.host ?? url)
        case .demo?:
            row("Source", "Démonstration")
        case nil:
            row("Source", "Aucune")
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
                block("Confidentialité", """
                FluxTV ne collecte aucune donnée. Pas de compte FluxTV, pas de publicité, pas de mesure d'audience.
                Ton abonnement (adresse du serveur, identifiants) est enregistré uniquement sur cet appareil, chiffré (trousseau sécurisé d'Apple ou, à défaut, fichier chiffré par le système), et n'est envoyé qu'au serveur de ton fournisseur.
                Favoris, reprises et historique restent sur cet appareil.
                """)
                block("Contenus", """
                FluxTV est un lecteur multimédia. Il ne fournit, n'héberge et ne vend aucune chaîne, aucun film ni aucune série. Tu es responsable des sources que tu ajoutes : utilise uniquement des contenus auxquels tu as légalement accès.
                La démonstration utilise des flux de test publics et des films libres de droits de la Blender Foundation (Creative Commons BY).
                """)
                block("Lecture vidéo : VLCKit", """
                Lecture assurée par VLCKit (MobileVLCKit / TVVLCKit), © VideoLAN et les auteurs de VLC, distribué sous licence GNU LGPL 2.1. Code source : https://code.videolan.org/videolan/VLCKit
                """)
                block("Polices Sora et Manrope", """
                Sora © The Sora Project Authors, Manrope © The Manrope Project Authors. Licence SIL Open Font License 1.1.
                """)
            }
            .padding(Theme.pagePadding)
            .frame(maxWidth: Theme.s(820), alignment: .leading)
        }
        .fluxScreen()
        .navigationTitle("Licences et confidentialité")
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
    /// « jeudi 2 octobre » → « Jeudi 2 octobre ».
    var capitalizedFirst: String { prefix(1).uppercased() + dropFirst() }
}
