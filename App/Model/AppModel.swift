import Foundation
import SwiftUI
import Combine

/// Ce que le lecteur doit ouvrir, avec son contexte (zapping, épisode suivant).
struct PlayerRequest: Identifiable {
    let id = UUID()
    var item: PlayItem
    /// Direct : liste des chaînes pour zapper (⬆⬇).
    var channels: [LiveChannel] = []
    var channelIndex: Int?
    /// Série : épisodes dans l'ordre, pour enchaîner.
    var episodes: [Episode] = []
    var series: Series?
}

/// État de l'app : source, catalogue du direct, lecture en cours.
@MainActor
final class AppModel: ObservableObject {
    enum Phase: Equatable { case launching, needsSetup, ready }

    @Published private(set) var phase: Phase = .launching
    @Published private(set) var source: Source?
    @Published private(set) var account: AccountInfo?
    @Published private(set) var live: LiveCatalog?
    @Published private(set) var liveLoading = false
    @Published private(set) var liveError: String?
    @Published var player: PlayerRequest?
    @Published var toast: String?
    /// Langue de l'interface : quand elle change, toute l'interface est refaite (voir `RootView`).
    @Published private(set) var language = Lang.current
    /// Programmes déjà connus (chaîne → liste), pour l'affichage immédiat.
    @Published private(set) var programmes: [String: [Programme]] = [:]

    let library = LibraryStore()
    let settings = SettingsStore()
    private(set) var repo: Repository?
    private var toastTask: Task<Void, Never>?
    private var epgRequested = Set<String>()
    private var forwards: [AnyCancellable] = []
    /// Réglages rouverts après un changement de langue (l'interface repart de l'accueil).
    private var reopenSettings = false

    init() {
        // Favoris, reprises et réglages : tous les écrans se mettent à jour
        forwards = [
            library.objectWillChange.sink { [weak self] _ in self?.objectWillChange.send() },
            settings.objectWillChange.sink { [weak self] _ in self?.objectWillChange.send() },
        ]
    }

    // ───────────────────────────── Démarrage, compte ─────────────────────────────

    func start() {
        guard phase == .launching else { return }
        #if DEBUG
        if startForAutomatedTest() { return }
        #endif
        // Source choisie dans l'app, sinon compte intégré (copie personnelle). La démonstration
        // essayée avant d'installer la copie personnelle ne remplace pas le compte intégré.
        var source = SourceKeychain.load()
        if source == nil || source == .demo, let embedded = EmbeddedAccount.load() { source = embedded }
        if let saved = source {
            use(saved)
            phase = .ready
            Task {
                await loadLive()
                await refreshAccount()
            }
        } else {
            phase = .needsSetup
        }
    }

    // ───────────────────────────── Langue ─────────────────────────────

    /// Premier lancement : langue choisie, l'app démarre.
    func chooseLanguage(_ code: String) {
        Lang.save(code)
        language = code
        start()
    }

    /// Changement dans les réglages : les listes sont relues (noms fabriqués par l'app : démo,
    /// « Autres »…), puis toute l'interface est refaite dans la nouvelle langue.
    func changeLanguage(_ code: String) {
        guard code != Lang.current else { return }
        Lang.save(code)
        if let s = source {
            let a = account
            use(s)
            account = a
            Task { await loadLive() }
        }
        reopenSettings = true
        language = code
    }

    /// Interface refaite après un changement de langue : faut-il rouvrir les réglages ?
    func takeReopenSettings() -> Bool {
        defer { reopenSettings = false }
        return reopenSettings
    }

    #if DEBUG
    /// Essais automatiques : fiche ouverte depuis l'accueil.
    @Published var debugMovie: Movie?
    @Published var debugSeries: Series?

    /**
     Essais automatiques sur simulateur (jamais dans la version installée) :
     « -FluxTVDemo YES » ouvre la démonstration, « -FluxTVOpen <id> » ouvre la fiche d'un film ou
     d'une série, « -FluxTVPlay <id> » lance aussitôt un film ou une chaîne de la démonstration
     (« demo:tos », « m3us:demo-open-movies », « demo:bbb », « demo:live2 »…).
     */
    private func startForAutomatedTest() -> Bool {
        let d = UserDefaults.standard
        guard d.bool(forKey: "FluxTVDemo") else { return false }
        Task {
            try? await connect(.demo)
            if let id = d.string(forKey: "FluxTVOpen") {
                try? await Task.sleep(nanoseconds: 800_000_000)
                debugMovie = Demo.movies.first { $0.id == id }
                debugSeries = Demo.series.first { $0.id == id }
            }
            guard let id = d.string(forKey: "FluxTVPlay") else { return }
            if let m = Demo.movies.first(where: { $0.id == id }) {
                play(movie: m, group: nil, fromStart: true)
            } else if let ch = Demo.channels.first(where: { $0.id == id }) {
                play(channel: ch, in: Demo.channels)
            }
        }
        return true
    }
    #endif

    private func use(_ s: Source) {
        source = s
        repo = Repository(source: s)
        live = nil
        liveError = nil
        programmes = [:]
        epgRequested = []
    }

    /// Vérifie la source puis l'enregistre. Lève une erreur lisible si elle ne fonctionne pas.
    func connect(_ s: Source) async throws {
        let candidate = Repository(source: s)
        switch s {
        case .xtream:
            try await candidate.checkAccount()
        case .playlist, .demo:
            let l = try await candidate.live(force: true)
            let vod = try await candidate.vodCategories()
            let series = try await candidate.seriesCategories()
            if l.channels.isEmpty && vod.isEmpty && series.isEmpty { throw FluxError.emptyPlaylist }
        }
        SourceKeychain.save(s)
        source = s
        repo = candidate
        account = await candidate.account
        live = nil
        programmes = [:]
        epgRequested = []
        phase = .ready
        await loadLive()
    }

    func signOut() {
        let old = repo
        Task { await old?.clearCache() }
        SourceKeychain.delete()
        source = nil
        repo = nil
        account = nil
        live = nil
        programmes = [:]
        phase = .needsSetup
    }

    func refreshAccount() async {
        guard let repo, source?.xtream != nil else { return }
        if let info = try? await repo.checkAccount() { account = info }
    }

    /// Actualise toutes les listes (chaînes, films, séries).
    func refreshAll() async {
        await repo?.clearCache()
        programmes = [:]
        epgRequested = []
        await loadLive(force: true)
        showToast(T("Listes actualisées", "Lists refreshed"))
    }

    // ───────────────────────────── Direct ─────────────────────────────

    func loadLive(force: Bool = false) async {
        guard let repo, !liveLoading else { return }
        if live != nil && !force { return }
        liveLoading = true
        liveError = nil
        do {
            live = try await repo.live(force: force)
        } catch {
            liveError = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
        liveLoading = false
    }

    /// Catégories visibles (contenus adultes masqués si demandé).
    func visible(_ categories: [Category]) -> [Category] {
        settings.hideAdult ? categories.filter { !$0.isAdult } : categories
    }

    func visibleChannels(in categoryId: String? = nil) -> [LiveChannel] {
        guard let live else { return [] }
        let hidden: Set<String> = settings.hideAdult ? Set(live.categories.filter(\.isAdult).map(\.id)) : []
        return live.channels.filter { ch in
            (categoryId == nil || ch.categoryId == categoryId) && !hidden.contains(ch.categoryId)
        }
    }

    func categoryName(_ id: String) -> String? { live?.category(id)?.name }

    func channel(withId id: String) -> LiveChannel? { live?.channels.first { $0.id == id } }

    /// Chaînes favorites (dans l'ordre choisi), puis chaînes récentes.
    var favoriteChannels: [LiveChannel] {
        library.data.favorites(of: .live).compactMap { channel(withId: $0.id) }
    }

    var recentChannels: [LiveChannel] {
        library.data.recentLive().compactMap { channel(withId: $0.id) }
    }

    // ───────────────────────────── Guide TV ─────────────────────────────

    /// Programmes d'une chaîne : chargés une fois à la demande (affichage des listes).
    func requestProgrammes(for channel: LiveChannel) {
        if let list = programmes[channel.id], (list.last?.stop ?? .distantPast) > Date() { return }
        guard let repo, !epgRequested.contains(channel.id) else { return }
        epgRequested.insert(channel.id)
        Task {
            let list = await repo.programmes(for: channel)
            programmes[channel.id] = list
            // Liste épuisée plus tard : on pourra la redemander
            epgRequested.remove(channel.id)
        }
    }

    func nowNext(_ channel: LiveChannel) -> (now: Programme?, next: Programme?) {
        Programme.nowNext(programmes[channel.id] ?? [])
    }

    // ───────────────────────────── Lecture ─────────────────────────────

    var liveExtension: String {
        settings.liveFormat == "auto" ? (account?.liveExtension ?? "ts") : settings.liveFormat
    }

    private func url(for ref: ContentRef) -> URL? {
        guard let source else { return nil }
        return PlaybackResolver.url(for: ref, source: source, liveExtension: liveExtension)
    }

    func playItem(for channel: LiveChannel) -> PlayItem? {
        let ref = channel.ref(group: categoryName(channel.categoryId))
        guard let u = url(for: ref) else { return nil }
        let (now, _) = nowNext(channel)
        return PlayItem(ref: ref, url: u, title: channel.name, subtitle: now?.title ?? ref.group, artwork: channel.logo,
                        isLive: true, userAgent: channel.userAgent, startAt: nil)
    }

    func play(channel: LiveChannel, in list: [LiveChannel]) {
        guard let item = playItem(for: channel) else { return showToast(T("Adresse de lecture introuvable", "Playback address not found")) }
        library.addHistory(item.ref)
        player = PlayerRequest(item: item, channels: list, channelIndex: list.firstIndex { $0.id == channel.id })
    }

    func play(movie: Movie, group: String?, fromStart: Bool = false) {
        var ref = movie.ref(group: group)
        if ref.image == nil { ref.image = movie.poster }
        guard let u = url(for: ref) else { return showToast(T("Adresse de lecture introuvable", "Playback address not found")) }
        let start = fromStart ? nil : library.resume(ref.id)?.position
        library.addHistory(ref)
        player = PlayerRequest(item: PlayItem(ref: ref, url: u, title: movie.name, subtitle: movie.yearText,
                                              artwork: movie.poster, isLive: false, userAgent: nil, startAt: start))
    }

    func play(episode: Episode, of series: Series, episodes: [Episode], cover: String?, fromStart: Bool = false) {
        let ref = episode.ref(series: series.seriesId, seriesName: series.name, image: cover ?? series.cover)
        guard let u = url(for: ref) else { return showToast(T("Adresse de lecture introuvable", "Playback address not found")) }
        let start = fromStart ? nil : library.resume(ref.id)?.position
        library.addHistory(ref)
        player = PlayerRequest(item: PlayItem(ref: ref, url: u, title: series.name, subtitle: "\(episode.label) · \(episode.title)",
                                              artwork: episode.image ?? cover ?? series.cover, isLive: false, userAgent: nil,
                                              startAt: start),
                               episodes: episodes, series: series)
    }

    /// Reprise depuis l'accueil (historique, favoris).
    func play(ref: ContentRef) {
        if ref.kind == .live, let ch = channel(withId: ref.id) {
            let list = visibleChannels(in: ch.categoryId)
            play(channel: ch, in: list.isEmpty ? [ch] : list)
            return
        }
        guard let u = url(for: ref) else { return showToast(T("Ce contenu n'est plus disponible", "This content is no longer available")) }
        library.addHistory(ref)
        let start = library.resume(ref.id)?.position
        let title = ref.kind == .episode ? (ref.seriesName ?? ref.name) : ref.name
        let subtitle = ref.kind == .episode ? ref.name : ref.group
        player = PlayerRequest(item: PlayItem(ref: ref, url: u, title: title, subtitle: subtitle, artwork: ref.image,
                                              isLive: ref.kind == .live, userAgent: ref.userAgent, startAt: start))
    }

    // ───────────────────────────── Messages ─────────────────────────────

    func showToast(_ text: String) {
        toastTask?.cancel()
        withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) { toast = text }
        toastTask = Task {
            try? await Task.sleep(nanoseconds: 2_600_000_000)
            if Task.isCancelled { return }
            withAnimation(.easeOut(duration: 0.3)) { toast = nil }
        }
    }

    func toggleFavorite(_ ref: ContentRef) {
        let added = library.toggleFavorite(ref)
        showToast(added ? T("Ajouté aux favoris", "Added to favorites") : T("Retiré des favoris", "Removed from favorites"))
    }
}
