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
                    Text("Bienvenue").font(Theme.display(30)).foregroundStyle(Theme.text)
                    Text("Ajoute ton abonnement : FluxTV affiche tes chaînes, ton guide TV, tes films et tes séries.")
                        .font(Theme.bodyRegular(15)).foregroundStyle(Theme.textDim)
                        .fixedSize(horizontal: false, vertical: true)
                }

                Picker("Type de source", selection: $mode) {
                    Text("Compte Xtream").tag(Mode.xtream)
                    Text("Lien M3U").tag(Mode.playlist)
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

                HStack(spacing: Theme.s(12)) {
                    PillButton(title: busy ? "Connexion…" : "Se connecter", systemImage: busy ? nil : "arrow.right") { connect() }
                        .disabled(busy)
                    PillButton(title: "Essayer la démo", systemImage: "sparkles", primary: false) { tryDemo() }
                        .disabled(busy)
                    if busy { ProgressView().tint(Theme.accentBright) }
                }
                .padding(.top, Theme.s(4))

                Text("FluxTV est un lecteur : il ne fournit aucune chaîne ni aucun contenu. Utilise uniquement des sources auxquelles tu as légalement accès. Tes identifiants restent sur cet appareil, dans le trousseau sécurisé d'Apple.")
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

    private var xtreamFields: some View {
        VStack(alignment: .leading, spacing: Theme.s(14)) {
            #if os(iOS)
            FluxField(label: "Serveur", text: $server, placeholder: "http://mon-serveur.com:8080", keyboard: .URL)
            #else
            FluxField(label: "Serveur", text: $server, placeholder: "http://mon-serveur.com:8080")
            #endif
            FluxField(label: "Nom d'utilisateur", text: $username, placeholder: "Nom d'utilisateur")
            FluxField(label: "Mot de passe", text: $password, placeholder: "Mot de passe", secure: true)
        }
    }

    private var playlistFields: some View {
        VStack(alignment: .leading, spacing: Theme.s(14)) {
            #if os(iOS)
            FluxField(label: "Adresse de la playlist", text: $playlistURL, placeholder: "http://…/playlist.m3u", keyboard: .URL)
            FluxField(label: "Guide TV (facultatif)", text: $epgURL, placeholder: "http://…/guide.xml", keyboard: .URL)
            #else
            FluxField(label: "Adresse de la playlist", text: $playlistURL, placeholder: "http://…/playlist.m3u")
            FluxField(label: "Guide TV (facultatif)", text: $epgURL, placeholder: "http://…/guide.xml")
            #endif
            Text("Un lien « get.php?username=…&password=… » est reconnu automatiquement comme un compte Xtream (affiches, résumés et guide TV en plus).")
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
                    error = "Remplis le serveur, le nom d'utilisateur et le mot de passe."
                    return
                }
                source = .xtream(a)
            }
        case .playlist:
            let url = playlistURL.trimmingCharacters(in: .whitespacesAndNewlines)
            guard url.lowercased().hasPrefix("http") else {
                error = "Colle l'adresse complète de ta playlist (elle commence par http)."
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
