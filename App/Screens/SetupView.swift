import SwiftUI

/// Premier lancement : compte Xtream, lien de playlist M3U, ou démonstration.
struct SetupView: View {
    @EnvironmentObject private var app: AppModel

    enum Mode: String, CaseIterable { case xtream, playlist }

    @State private var mode: Mode = .xtream
    @State private var server = ""
    @State private var username = ""
    @State private var password = ""
    @State private var playlistURL = ""
    @State private var epgURL = ""
    @State private var busy = false
    @State private var error: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(18)) {
                BrandLogo(size: 44)
                    .padding(.bottom, Theme.s(4))
                VStack(alignment: .leading, spacing: Theme.s(6)) {
                    Text(T("Bienvenue", "Welcome")).font(Theme.display(30)).foregroundStyle(Theme.text)
                    Text(T("Ajoute ton abonnement : FluxTV affiche tes chaînes, ton guide TV, tes films et tes séries.",
                           "Add your subscription: FluxTV shows your channels, TV guide, movies and series."))
                        .font(Theme.bodyRegular(15)).foregroundStyle(Theme.textDim)
                        .fixedSize(horizontal: false, vertical: true)
                }

                Picker(T("Type de source", "Source type"), selection: $mode) {
                    Text(T("Compte Xtream", "Xtream account")).tag(Mode.xtream)
                    Text(T("Lien M3U", "M3U link")).tag(Mode.playlist)
                }
                .pickerStyle(.segmented)

                if mode == .xtream {
                    xtreamFields
                } else {
                    playlistFields
                }

                if let error {
                    HStack(alignment: .top, spacing: Theme.s(8)) {
                        Image(systemName: "exclamationmark.circle.fill").foregroundStyle(Theme.error)
                        Text(error).font(Theme.body(14)).foregroundStyle(Theme.error).fixedSize(horizontal: false, vertical: true)
                    }
                    .transition(.opacity)
                }

                // Côte à côte s'il y a la place, sinon l'un sous l'autre (iPhone)
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: Theme.s(12)) { actionButtons }
                    VStack(alignment: .leading, spacing: Theme.s(10)) { actionButtons }
                }
                .padding(.top, Theme.s(4))

                Text(T("FluxTV est un lecteur : il ne fournit aucune chaîne ni aucun contenu. Utilise uniquement des sources auxquelles tu as légalement accès. Tes identifiants restent chiffrés sur cet appareil.",
                       "FluxTV is a player: it provides no channels or content. Only use sources you have legal access to. Your login details stay encrypted on this device."))
                    .font(Theme.bodyRegular(12))
                    .foregroundStyle(Theme.textFaint)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, Theme.s(8))
            }
            .padding(Theme.s(28))
            .frame(maxWidth: Theme.s(560))
            .background(RoundedRectangle(cornerRadius: Theme.s(28), style: .continuous).fill(Theme.surface.opacity(0.82)))
            .overlay(RoundedRectangle(cornerRadius: Theme.s(28), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
            .padding(.horizontal, Theme.pagePadding)
            .padding(.vertical, Theme.s(40))
            .frame(maxWidth: .infinity)
        }
        .scrollDismissesKeyboardIfAvailable()
        .animation(.easeInOut(duration: 0.2), value: mode)
        .animation(.easeInOut(duration: 0.2), value: error)
    }

    @ViewBuilder private var actionButtons: some View {
        if let embedded = EmbeddedAccount.load() {
            PillButton(title: T("Mon compte", "My account"), systemImage: "person.fill") { error = nil; run(embedded) }
                .disabled(busy)
        }
        PillButton(title: busy ? T("Connexion…", "Connecting…") : T("Se connecter", "Sign in"), systemImage: busy ? nil : "arrow.right") { connect() }
            .disabled(busy)
        PillButton(title: T("Essayer la démo", "Try the demo"), systemImage: "sparkles", primary: false) { tryDemo() }
            .disabled(busy)
        if busy { ProgressView().tint(Theme.accentBright) }
    }

    private var xtreamFields: some View {
        VStack(alignment: .leading, spacing: Theme.s(14)) {
            #if os(iOS)
            FluxField(label: T("Serveur", "Server"), text: $server, placeholder: T("http://mon-serveur.com:8080", "http://my-server.com:8080"), keyboard: .URL)
            #else
            FluxField(label: T("Serveur", "Server"), text: $server, placeholder: T("http://mon-serveur.com:8080", "http://my-server.com:8080"))
            #endif
            FluxField(label: T("Nom d'utilisateur", "Username"), text: $username, placeholder: T("Nom d'utilisateur", "Username"))
            FluxField(label: T("Mot de passe", "Password"), text: $password, placeholder: T("Mot de passe", "Password"), secure: true)
        }
    }

    private var playlistFields: some View {
        VStack(alignment: .leading, spacing: Theme.s(14)) {
            #if os(iOS)
            FluxField(label: T("Adresse de la playlist", "Playlist URL"), text: $playlistURL, placeholder: "http://…/playlist.m3u", keyboard: .URL)
            FluxField(label: T("Guide TV (facultatif)", "TV guide (optional)"), text: $epgURL, placeholder: "http://…/guide.xml", keyboard: .URL)
            #else
            FluxField(label: T("Adresse de la playlist", "Playlist URL"), text: $playlistURL, placeholder: "http://…/playlist.m3u")
            FluxField(label: T("Guide TV (facultatif)", "TV guide (optional)"), text: $epgURL, placeholder: "http://…/guide.xml")
            #endif
            Text(T("Un lien « get.php?username=…&password=… » est reconnu automatiquement comme un compte Xtream (affiches, résumés et guide TV en plus).",
                   "A “get.php?username=…&password=…” link is recognized automatically as an Xtream account (with posters, summaries and TV guide)."))
                .font(Theme.bodyRegular(12)).foregroundStyle(Theme.textFaint)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func connect() {
        error = nil
        let source: Source
        switch mode {
        case .xtream:
            // Lien de playlist Xtream collé dans le champ « Serveur »
            if let a = XtreamAccount.fromPlaylistURL(server) {
                source = .xtream(a)
            } else {
                let a = XtreamAccount(server: server, username: username, password: password)
                guard a.isComplete else {
                    error = T("Remplis le serveur, le nom d'utilisateur et le mot de passe.", "Fill in the server, username and password.")
                    return
                }
                source = .xtream(a)
            }
        case .playlist:
            let url = playlistURL.trimmingCharacters(in: .whitespacesAndNewlines)
            guard url.lowercased().hasPrefix("http") else {
                error = T("Colle l'adresse complète de ta playlist (elle commence par http).", "Paste the full address of your playlist (it starts with http).")
                return
            }
            if let a = XtreamAccount.fromPlaylistURL(url) {
                source = .xtream(a)
            } else {
                let epg = epgURL.trimmingCharacters(in: .whitespacesAndNewlines)
                source = .playlist(url: url, epg: epg.isEmpty ? nil : epg)
            }
        }
        run(source)
    }

    private func tryDemo() {
        error = nil
        run(.demo)
    }

    private func run(_ source: Source) {
        busy = true
        Task {
            do {
                try await app.connect(source)
            } catch {
                self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
            busy = false
        }
    }
}

extension View {
    /// iPhone : le clavier se ferme quand on fait défiler.
    @ViewBuilder func scrollDismissesKeyboardIfAvailable() -> some View {
        #if os(iOS)
        self.scrollDismissesKeyboard(.interactively)
        #else
        self
        #endif
    }
}
