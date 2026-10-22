import SwiftUI
import UIKit
import Combine

/// Surface vidéo (la vue de VLC), recadrée à chaque changement de taille (rotation).
struct VideoSurface: UIViewRepresentable {
    let engine: VideoEngine

    func makeUIView(context: Context) -> SurfaceView {
        let v = SurfaceView()
        v.backgroundColor = .black
        v.onLayout = { [weak engine] in engine?.applyAspect() }
        engine.view.frame = v.bounds
        engine.view.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        v.addSubview(engine.view)
        return v
    }

    func updateUIView(_ uiView: SurfaceView, context: Context) {}

    final class SurfaceView: UIView {
        var onLayout: (() -> Void)?
        private var lastSize = CGSize.zero
        override func layoutSubviews() {
            super.layoutSubviews()
            if bounds.size != lastSize {
                lastSize = bounds.size
                onLayout?()
            }
        }
    }
}

/// Lecteur plein écran : direct (zapping, programme en cours) et vidéo à la demande.
struct PlayerView: View {
    @EnvironmentObject private var app: AppModel
    @Environment(\.scenePhase) private var scenePhase
    @StateObject private var engine = VideoEngine()

    let request: PlayerRequest
    @State private var item: PlayItem
    @State private var channelIndex: Int?
    @State private var controlsVisible = true
    @State private var panel: Panel?
    @State private var hideTask: Task<Void, Never>?
    @State private var lastSave = Date.distantPast
    @State private var upNext: Episode?
    @State private var countdown = 0
    @State private var countdownTask: Task<Void, Never>?
    @State private var retries = 0
    /// Direct : nouvelle tentative programmée (pas de message d'erreur en attendant).
    @State private var retryPending = false
    @State private var scrubbing = false
    @State private var scrubValue: Double = 0
    @State private var seekFlash: String?
    #if os(tvOS)
    @FocusState private var catcherFocused: Bool
    #endif

    enum Panel { case channels, options }

    init(request: PlayerRequest) {
        self.request = request
        _item = State(initialValue: request.item)
        _channelIndex = State(initialValue: request.channelIndex)
    }

    private var currentChannel: LiveChannel? {
        guard let i = channelIndex, request.channels.indices.contains(i) else { return nil }
        return request.channels[i]
    }

    private var currentEpisodeIndex: Int? { request.episodes.firstIndex { $0.id == item.ref.id } }

    private var nextEpisode: Episode? {
        guard let i = currentEpisodeIndex, i + 1 < request.episodes.count else { return nil }
        return request.episodes[i + 1]
    }

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            VideoSurface(engine: engine).ignoresSafeArea()

            #if os(tvOS)
            remoteCatcher
            #else
            // Toucher l'image affiche ou masque les commandes
            Color.clear
                .contentShape(Rectangle())
                .ignoresSafeArea()
                .onTapGesture { toggleControls() }
            #endif

            statusLayer

            if controlsVisible && panel == nil && upNext == nil {
                controlsLayer.transition(.opacity)
            }
            if let flash = seekFlash {
                Text(flash)
                    .font(Theme.title(20))
                    .foregroundStyle(.white)
                    .padding(.horizontal, Theme.s(18)).padding(.vertical, Theme.s(10))
                    .background(.ultraThinMaterial, in: Capsule())
                    .transition(.opacity)
            }
            if panel == .channels {
                ChannelPanel(channels: request.channels, currentId: item.ref.id, onPick: { zap(to: $0) }, onClose: { closePanel() })
                    .transition(.move(edge: .leading).combined(with: .opacity))
            }
            #if os(tvOS)
            if panel == .options {
                OptionsPanel(engine: engine, onClose: { closePanel() })
                    .transition(.move(edge: .trailing).combined(with: .opacity))
            }
            #endif
            if let next = upNext {
                UpNextCard(episode: next, seriesName: request.series?.name ?? item.title, countdown: countdown,
                           onPlay: { playNext() }, onCancel: { cancelUpNext() })
                    .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .animation(.easeInOut(duration: 0.25), value: controlsVisible)
        .animation(.spring(response: 0.35, dampingFraction: 0.85), value: panel)
        .animation(.easeOut(duration: 0.2), value: seekFlash)
        .preferredColorScheme(.dark)
        .onAppear { start() }
        .onDisappear { finish() }
        .onChange(of: engine.status) { _, s in handle(status: s) }
        .onReceive(engine.$time) { t in saveIfNeeded(t) }
        .onChange(of: scenePhase) { _, phase in
            if phase != .active { save(force: true) }
        }
        #if os(iOS)
        .statusBarHidden(true)
        .persistentSystemOverlays(.hidden)
        #endif
        #if os(tvOS)
        .onPlayPauseCommand {
            // Direct : la pause n'a pas d'intérêt, ⏯ ouvre l'audio et les sous-titres
            if item.isLive { openPanel(.options) } else { engine.togglePause(); showControls() }
        }
        .onExitCommand { onExit() }
        #endif
    }

    // ───────────────────────────── Couches ─────────────────────────────

    /// Message d'erreur à l'écran : plus aucune nouvelle tentative automatique prévue.
    private var showsError: Bool {
        switch engine.status {
        case .failed: return !retryPending
        case .ended: return item.isLive && retries >= 3
        default: return false
        }
    }

    @ViewBuilder private var statusLayer: some View {
        if showsError {
            ErrorCard(message: engine.status == .ended ? "Le direct s'est interrompu." :
                        (item.isLive ? "Cette chaîne ne répond pas pour le moment." : "Lecture impossible : le serveur ne renvoie pas cette vidéo."),
                      onRetry: { retries = 0; retry() }, onClose: { close() })
        } else if [.opening, .buffering, .idle, .failed].contains(engine.status) || (engine.status == .ended && item.isLive) {
            VStack(spacing: Theme.s(14)) {
                ProgressView().tint(.white).scaleEffect(1.4 * Theme.scale)
                if engine.status == .opening {
                    Text(item.isLive ? "Ouverture de \(item.title)…" : "Chargement…")
                        .font(Theme.label(14)).foregroundStyle(.white.opacity(0.85))
                }
            }
            .allowsHitTesting(false)
        }
    }

    private var controlsLayer: some View {
        ZStack {
            VStack(spacing: 0) {
                LinearGradient(colors: [.black.opacity(0.75), .clear], startPoint: .top, endPoint: .bottom)
                    .frame(height: Theme.s(150))
                Spacer()
                LinearGradient(colors: [.clear, .black.opacity(0.85)], startPoint: .top, endPoint: .bottom)
                    .frame(height: Theme.s(item.isLive ? 220 : 200))
            }
            .ignoresSafeArea()
            .allowsHitTesting(false)

            VStack(spacing: 0) {
                topBar
                Spacer()
                #if os(iOS)
                centerButtons
                Spacer()
                #endif
                if item.isLive { liveInfo } else { vodBar }
            }
            .padding(.horizontal, Theme.pagePadding)
            .padding(.vertical, Theme.s(14))
        }
        #if os(iOS)
        .contentShape(Rectangle())
        .onTapGesture { toggleControls() }
        #endif
    }

    private var topBar: some View {
        HStack(alignment: .top, spacing: Theme.s(14)) {
            #if os(iOS)
            RoundIconButton(systemImage: "xmark", label: "Fermer") { close() }
            #endif
            VStack(alignment: .leading, spacing: Theme.s(3)) {
                Text(item.title).font(Theme.title(20)).foregroundStyle(.white).lineLimit(1)
                if let sub = item.subtitle, !sub.isEmpty, !item.isLive {
                    Text(sub).font(Theme.body(14)).foregroundStyle(.white.opacity(0.75)).lineLimit(1)
                }
            }
            Spacer(minLength: 0)
            #if os(iOS)
            optionsMenus
            #endif
            ClockLabel()
        }
    }

    #if os(iOS)
    private var centerButtons: some View {
        HStack(spacing: Theme.s(44)) {
            if item.isLive {
                RoundIconButton(systemImage: "chevron.up", label: "Chaîne précédente") { zap(by: -1) }
                    .opacity(request.channels.count > 1 ? 1 : 0)
            } else {
                RoundIconButton(systemImage: "gobackward.10", label: "Reculer de 10 secondes") { jump(-10) }
            }
            Button {
                engine.togglePause()
                showControls()
            } label: {
                Image(systemName: engine.status == .paused ? "play.fill" : "pause.fill")
                    .font(.system(size: 30, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 76, height: 76)
                    .background(Circle().fill(.ultraThinMaterial))
            }
            .buttonStyle(FluxButtonStyle(cornerRadius: 40, showsRing: false))
            .accessibilityLabel(engine.status == .paused ? "Lecture" : "Pause")
            if item.isLive {
                RoundIconButton(systemImage: "chevron.down", label: "Chaîne suivante") { zap(by: 1) }
                    .opacity(request.channels.count > 1 ? 1 : 0)
            } else {
                RoundIconButton(systemImage: "goforward.10", label: "Avancer de 10 secondes") { jump(10) }
            }
        }
    }

    /// Audio, sous-titres, format d'image : menus natifs de l'iPhone.
    private var optionsMenus: some View {
        HStack(spacing: Theme.s(10)) {
            if item.isLive && request.channels.count > 1 {
                RoundIconButton(systemImage: "list.bullet", label: "Liste des chaînes") { openPanel(.channels) }
            }
            Menu {
                if !engine.audioTracks.isEmpty {
                    Section("Audio") {
                        ForEach(engine.audioTracks) { t in
                            Button { engine.selectAudio(t.id) } label: {
                                if t.id == engine.currentAudio { Label(t.name, systemImage: "checkmark") } else { Text(t.name) }
                            }
                        }
                    }
                }
                if !engine.subtitleTracks.isEmpty {
                    Section("Sous-titres") {
                        ForEach(engine.subtitleTracks) { t in
                            Button { engine.selectSubtitle(t.id) } label: {
                                if t.id == engine.currentSubtitle { Label(t.name, systemImage: "checkmark") } else { Text(t.name) }
                            }
                        }
                    }
                }
                Section("Image") {
                    ForEach(AspectMode.allCases, id: \.self) { m in
                        Button { engine.setAspect(m) } label: {
                            if m == engine.aspect { Label(m.label, systemImage: "checkmark") } else { Text(m.label) }
                        }
                    }
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 46, height: 46)
                    .background(Circle().fill(Theme.glassStrong))
            }
            .accessibilityLabel("Options : audio, sous-titres, image")
            if nextEpisode != nil {
                RoundIconButton(systemImage: "forward.end.fill", label: "Épisode suivant") { playNext() }
            }
        }
    }
    #endif

    private var liveInfo: some View {
        let ch = currentChannel
        let nn: (now: Programme?, next: Programme?) = ch.map { app.nowNext($0) } ?? (nil, nil)
        return HStack(alignment: .center, spacing: Theme.s(16)) {
            RemoteImage(url: item.artwork, contentMode: .fit, maxSize: 90, placeholderTitle: item.title, placeholderIcon: "tv")
                .padding(Theme.s(8))
                .frame(width: Theme.s(96), height: Theme.s(64))
                .background(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).fill(Color.white.opacity(0.1)))
                .clipShape(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous))
            VStack(alignment: .leading, spacing: Theme.s(5)) {
                HStack(spacing: Theme.s(10)) {
                    if let n = ch?.num, n > 0 {
                        Text(String(n)).font(Theme.strong(13)).foregroundStyle(Theme.text)
                            .padding(.horizontal, Theme.s(7)).padding(.vertical, Theme.s(2))
                            .background(RoundedRectangle(cornerRadius: Theme.s(6)).fill(Theme.accent.opacity(0.35)))
                    }
                    Badge(text: "Direct", style: .live)
                    ForEach(ch?.badges ?? [], id: \.self) { Badge(text: $0) }
                }
                Text(nn.now?.title ?? item.title)
                    .font(Theme.title(19)).foregroundStyle(.white).lineLimit(1)
                if let now = nn.now {
                    HStack(spacing: Theme.s(10)) {
                        Text(Format.hour.string(from: now.start)).font(Theme.mono(12)).foregroundStyle(.white.opacity(0.8))
                        ProgressLine(value: now.progress(), color: Theme.onAir, height: 3)
                        Text(Format.hour.string(from: now.stop)).font(Theme.mono(12)).foregroundStyle(.white.opacity(0.8))
                    }
                    .frame(maxWidth: Theme.s(520))
                }
                if let next = nn.next {
                    Text("Ensuite  \(Format.hour.string(from: next.start))  \(next.title)")
                        .font(Theme.body(13)).foregroundStyle(.white.opacity(0.7)).lineLimit(1)
                }
                #if os(tvOS)
                Text(request.channels.count > 1 ? "⬆︎ ⬇︎ chaînes   ·   OK liste des chaînes   ·   ⏯ audio et sous-titres" : "⏯ audio et sous-titres")
                    .font(Theme.body(11)).foregroundStyle(.white.opacity(0.55)).padding(.top, Theme.s(4))
                #endif
            }
            Spacer(minLength: 0)
        }
    }

    private var vodBar: some View {
        let shown = scrubbing ? scrubValue : engine.time
        return VStack(spacing: Theme.s(8)) {
            #if os(iOS)
            if engine.duration > 0 {
                Slider(value: Binding(get: { scrubbing ? scrubValue : engine.time },
                                      set: { scrubValue = $0 }),
                       in: 0...max(1, engine.duration),
                       onEditingChanged: { editing in
                           if editing {
                               scrubValue = engine.time
                               scrubbing = true
                               hideTask?.cancel()
                           } else {
                               engine.seek(to: scrubValue)
                               scrubbing = false
                               scheduleHide()
                           }
                       })
                    .tint(Theme.accentBright)
            }
            #else
            ProgressLine(value: engine.duration > 0 ? engine.time / engine.duration : 0, color: Theme.accentBright, height: 6)
            #endif
            HStack {
                Text(Format.clock(shown)).font(Theme.mono(13)).foregroundStyle(.white.opacity(0.9))
                Spacer()
                #if os(tvOS)
                Text("◀︎ ▶︎ ±10 s   ·   OK pause   ·   ⬇︎ audio et sous-titres").font(Theme.body(11)).foregroundStyle(.white.opacity(0.55))
                Spacer()
                #endif
                Text(engine.duration > 0 ? "−" + Format.clock(max(0, engine.duration - shown)) : "")
                    .font(Theme.mono(13)).foregroundStyle(.white.opacity(0.9))
            }
        }
    }

    #if os(tvOS)
    /// Apple TV : la télécommande pilote le lecteur (aucun bouton à viser).
    private var remoteCatcher: some View {
        Button(action: onSelect) {
            Color.clear.frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .buttonStyle(InvisibleButtonStyle())
        .focused($catcherFocused)
        .onMoveCommand(perform: onMove)
        // Erreur : la télécommande doit pouvoir atteindre « Réessayer » et « Fermer »
        .disabled(panel != nil || upNext != nil || showsError)
        .ignoresSafeArea()
    }

    private func onSelect() {
        if item.isLive {
            if request.channels.count > 1 { openPanel(.channels) } else { showControls() }
        } else {
            engine.togglePause()
            showControls()
        }
    }

    private func onMove(_ direction: MoveCommandDirection) {
        switch direction {
        case .left:
            if item.isLive { showControls() } else { jump(-10) }
        case .right:
            if item.isLive { showControls() } else { jump(10) }
        case .up:
            if item.isLive { zap(by: -1) } else { showControls() }
        case .down:
            if item.isLive { zap(by: 1) } else { openPanel(.options) }
        @unknown default:
            break
        }
    }

    private func onExit() {
        if panel != nil { closePanel() }
        else if upNext != nil { cancelUpNext() }
        else if controlsVisible && engine.status == .playing { controlsVisible = false }
        else { close() }
    }
    #endif

    // ───────────────────────────── Actions ─────────────────────────────

    private func start() {
        UIApplication.shared.isIdleTimerDisabled = true
        VideoEngine.configureAudioSession()
        engine.open(item, caching: app.settings.networkCaching)
        if let ch = currentChannel { app.requestProgrammes(for: ch) }
        scheduleHide()
        #if os(tvOS)
        catcherFocused = true
        #endif
    }

    private func finish() {
        save(force: true)
        hideTask?.cancel()
        countdownTask?.cancel()
        engine.stop()
        UIApplication.shared.isIdleTimerDisabled = false
    }

    private func close() {
        save(force: true)
        engine.stop()
        app.player = nil
    }

    private func handle(status: VideoEngine.Status) {
        switch status {
        case .playing:
            retries = 0
        case .ended:
            if item.isLive {
                // Flux coupé : on le rouvre (trois fois au plus, puis message)
                if retries < 3 {
                    retries += 1
                    retry()
                }
            } else if let next = nextEpisode, app.settings.autoplayNext {
                save(force: true, ended: true)
                startUpNext(next)
            } else {
                save(force: true, ended: true)
                close()
            }
        case .failed:
            // Le direct décroche parfois : deux nouvelles tentatives avant d'afficher l'erreur
            if item.isLive && retries < 2 {
                retries += 1
                retryPending = true
                Task {
                    try? await Task.sleep(nanoseconds: 1_500_000_000)
                    retryPending = false
                    engine.open(item, caching: app.settings.networkCaching)
                }
            }
        default:
            break
        }
    }

    private func retry() {
        var it = item
        if !it.isLive { it.startAt = engine.time > 5 ? engine.time : it.startAt }
        engine.open(it, caching: app.settings.networkCaching)
        #if os(tvOS)
        // Le message d'erreur disparaît : la télécommande revient au lecteur
        Task { catcherFocused = true }
        #endif
    }

    private func jump(_ seconds: Double) {
        guard engine.isSeekable else { return }
        engine.jump(by: seconds)
        flash(seconds > 0 ? "+\(Int(seconds)) s" : "−\(Int(-seconds)) s")
        showControls()
    }

    private func flash(_ text: String) {
        seekFlash = text
        Task {
            try? await Task.sleep(nanoseconds: 700_000_000)
            if seekFlash == text { seekFlash = nil }
        }
    }

    private func zap(by delta: Int) {
        guard !request.channels.isEmpty, let i = channelIndex else { return }
        let n = request.channels.count
        zap(to: request.channels[(i + delta + n) % n])
    }

    private func zap(to channel: LiveChannel) {
        guard let newItem = app.playItem(for: channel) else { return }
        panel = nil
        channelIndex = request.channels.firstIndex { $0.id == channel.id }
        item = newItem
        retries = 0
        app.library.addHistory(newItem.ref)
        app.requestProgrammes(for: channel)
        engine.open(newItem, caching: app.settings.networkCaching)
        showControls()
    }

    private func openPanel(_ p: Panel) {
        hideTask?.cancel()
        panel = p
    }

    private func closePanel() {
        panel = nil
        showControls()
        #if os(tvOS)
        catcherFocused = true
        #endif
    }

    private func toggleControls() {
        if controlsVisible { controlsVisible = false; hideTask?.cancel() } else { showControls() }
    }

    private func showControls() {
        controlsVisible = true
        scheduleHide()
    }

    private func scheduleHide() {
        hideTask?.cancel()
        hideTask = Task {
            try? await Task.sleep(nanoseconds: 4_500_000_000)
            if Task.isCancelled { return }
            if engine.status == .playing && !scrubbing && panel == nil { controlsVisible = false }
        }
    }

    // ───────────────────────────── Reprise, épisode suivant ─────────────────────────────

    private func saveIfNeeded(_ t: Double) {
        guard !item.isLive, Date().timeIntervalSince(lastSave) > 10 else { return }
        save(force: false)
    }

    private func save(force: Bool, ended: Bool = false) {
        guard !item.isLive else { return }
        let d = engine.duration
        let t = ended ? d : engine.time
        guard d > 0 || force, t > 0 else { return }
        lastSave = Date()
        app.library.savePosition(item.ref, position: t, duration: d)
    }

    private func startUpNext(_ ep: Episode) {
        upNext = ep
        countdown = 8
        countdownTask?.cancel()
        countdownTask = Task {
            while countdown > 0 {
                try? await Task.sleep(nanoseconds: 1_000_000_000)
                if Task.isCancelled { return }
                countdown -= 1
            }
            playNext()
        }
    }

    private func cancelUpNext() {
        countdownTask?.cancel()
        upNext = nil
        close()
    }

    private func playNext() {
        countdownTask?.cancel()
        guard let next = upNext ?? nextEpisode, let series = request.series else { upNext = nil; return }
        let ref = next.ref(series: series.seriesId, seriesName: series.name, image: series.cover)
        guard let source = app.source, let url = PlaybackResolver.url(for: ref, source: source, liveExtension: app.liveExtension) else {
            upNext = nil
            return
        }
        save(force: true)
        let newItem = PlayItem(ref: ref, url: url, title: series.name, subtitle: "\(next.label) · \(next.title)",
                               artwork: next.image ?? series.cover, isLive: false, userAgent: nil,
                               startAt: app.library.resume(ref.id)?.position)
        upNext = nil
        item = newItem
        lastSave = .distantPast
        app.library.addHistory(ref)
        engine.open(newItem, caching: app.settings.networkCaching)
        showControls()
    }
}

// ───────────────────────────── Éléments du lecteur ─────────────────────────────

/// Bouton sans aucun effet visuel (zone de capture de la télécommande).
struct InvisibleButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View { configuration.label.contentShape(Rectangle()) }
}

/// Heure courante.
struct ClockLabel: View {
    var body: some View {
        TimelineView(.periodic(from: .now, by: 20)) { context in
            Text(Format.hour.string(from: context.date))
                .font(Theme.display(20))
                .foregroundStyle(.white)
                .monospacedDigit()
        }
    }
}

private struct ErrorCard: View {
    let message: String
    let onRetry: () -> Void
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: Theme.s(12)) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: Theme.s(30))).foregroundStyle(Theme.onAir)
            Text("Lecture interrompue").font(Theme.title(20)).foregroundStyle(.white)
            Text(message).font(Theme.bodyRegular(14)).foregroundStyle(Theme.textDim).multilineTextAlignment(.center)
            HStack(spacing: Theme.s(12)) {
                PillButton(title: "Réessayer", systemImage: "arrow.clockwise", action: onRetry)
                PillButton(title: "Fermer", primary: false, action: onClose)
            }
            .padding(.top, Theme.s(6))
        }
        .padding(Theme.s(28))
        .frame(maxWidth: Theme.s(460))
        .background(RoundedRectangle(cornerRadius: Theme.s(24), style: .continuous).fill(Theme.surface.opacity(0.96)))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(24), style: .continuous).strokeBorder(Theme.onAir.opacity(0.5), lineWidth: 1))
        .padding(Theme.s(20))
    }
}

private struct UpNextCard: View {
    let episode: Episode
    let seriesName: String
    let countdown: Int
    let onPlay: () -> Void
    let onCancel: () -> Void

    var body: some View {
        VStack {
            Spacer()
            HStack {
                Spacer()
                VStack(alignment: .leading, spacing: Theme.s(8)) {
                    Eyebrow(text: "Épisode suivant")
                    Text("\(episode.label) · \(episode.title)").font(Theme.title(18)).foregroundStyle(.white).lineLimit(2)
                    Text("Lecture dans \(countdown) s").font(Theme.body(13)).foregroundStyle(Theme.textDim)
                    HStack(spacing: Theme.s(10)) {
                        PillButton(title: "Lire", systemImage: "play.fill", action: onPlay)
                        PillButton(title: "Annuler", primary: false, action: onCancel)
                    }
                    .padding(.top, Theme.s(4))
                }
                .padding(Theme.s(20))
                .frame(maxWidth: Theme.s(420), alignment: .leading)
                .background(RoundedRectangle(cornerRadius: Theme.s(22), style: .continuous).fill(Theme.surface.opacity(0.95)))
                .overlay(RoundedRectangle(cornerRadius: Theme.s(22), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
            }
        }
        .padding(Theme.pagePadding)
        .padding(.bottom, Theme.s(20))
    }
}

/// Liste des chaînes pendant la lecture (zapping).
private struct ChannelPanel: View {
    @EnvironmentObject private var app: AppModel
    let channels: [LiveChannel]
    let currentId: String
    let onPick: (LiveChannel) -> Void
    let onClose: () -> Void
    @FocusState private var focused: String?

    var body: some View {
        HStack(spacing: 0) {
            VStack(alignment: .leading, spacing: Theme.s(10)) {
                HStack {
                    Text("Chaînes").font(Theme.title(20)).foregroundStyle(.white)
                    Spacer()
                    #if os(iOS)
                    RoundIconButton(systemImage: "xmark", label: "Fermer la liste", action: onClose)
                    #endif
                }
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(spacing: Theme.s(6)) {
                            ForEach(channels) { ch in
                                Button { onPick(ch) } label: { row(ch) }
                                    .buttonStyle(FluxButtonStyle(focusScale: 1.03, cornerRadius: 14))
                                    .focused($focused, equals: ch.id)
                                    .id(ch.id)
                                    .onAppear { app.requestProgrammes(for: ch) }
                            }
                        }
                        .padding(.vertical, Theme.s(8))
                        .padding(.horizontal, Theme.s(6))
                    }
                    .onAppear {
                        proxy.scrollTo(currentId, anchor: .center)
                        focused = currentId
                    }
                }
            }
            .padding(Theme.s(18))
            .frame(width: Theme.s(380))
            .frame(maxHeight: .infinity)
            .background(Theme.abyss.opacity(0.94))
            .overlay(alignment: .trailing) { Rectangle().fill(Theme.stroke).frame(width: 1) }
            Spacer(minLength: 0)
        }
        .ignoresSafeArea(edges: .vertical)
        #if os(iOS)
        .background(Color.black.opacity(0.001).onTapGesture { onClose() })
        #endif
    }

    private func row(_ ch: LiveChannel) -> some View {
        let now = app.nowNext(ch).now
        let playing = ch.id == currentId
        return HStack(spacing: Theme.s(12)) {
            Text(ch.num > 0 ? String(ch.num) : "").font(Theme.mono(12)).foregroundStyle(Theme.textFaint).frame(width: Theme.s(34), alignment: .trailing)
            RemoteImage(url: ch.logo, contentMode: .fit, maxSize: 60, placeholderTitle: ch.name, placeholderIcon: "tv")
                .padding(Theme.s(4))
                .frame(width: Theme.s(56), height: Theme.s(38))
                .background(RoundedRectangle(cornerRadius: Theme.s(8)).fill(Color.white.opacity(0.08)))
                .clipShape(RoundedRectangle(cornerRadius: Theme.s(8)))
            VStack(alignment: .leading, spacing: Theme.s(2)) {
                Text(ch.name).font(Theme.label(14)).foregroundStyle(playing ? Theme.accentBright : Theme.text).lineLimit(1)
                if let now {
                    Text(now.title).font(Theme.body(12)).foregroundStyle(Theme.textDim).lineLimit(1)
                }
            }
            Spacer(minLength: 0)
            if playing { Badge(text: "En cours", style: .live) }
        }
        .padding(.horizontal, Theme.s(10))
        .padding(.vertical, Theme.s(8))
        .background(RoundedRectangle(cornerRadius: Theme.s(14), style: .continuous).fill(playing ? Theme.accent.opacity(0.22) : Color.white.opacity(0.05)))
        .contentShape(Rectangle())
    }
}

#if os(tvOS)
/// Apple TV : audio, sous-titres et format d'image.
private struct OptionsPanel: View {
    @ObservedObject var engine: VideoEngine
    let onClose: () -> Void

    var body: some View {
        HStack(spacing: 0) {
            Spacer(minLength: 0)
            ScrollView {
                VStack(alignment: .leading, spacing: Theme.s(14)) {
                    Text("Options").font(Theme.title(22)).foregroundStyle(.white)
                    if !engine.audioTracks.isEmpty {
                        section("Audio")
                        ForEach(engine.audioTracks) { t in
                            option(t.name, selected: t.id == engine.currentAudio) { engine.selectAudio(t.id) }
                        }
                    }
                    if !engine.subtitleTracks.isEmpty {
                        section("Sous-titres")
                        ForEach(engine.subtitleTracks) { t in
                            option(t.name, selected: t.id == engine.currentSubtitle) { engine.selectSubtitle(t.id) }
                        }
                    }
                    section("Image")
                    ForEach(AspectMode.allCases, id: \.self) { m in
                        option(m.label, selected: m == engine.aspect) { engine.setAspect(m) }
                    }
                }
                .padding(Theme.s(24))
            }
            .frame(width: Theme.s(420))
            .frame(maxHeight: .infinity)
            .background(Theme.abyss.opacity(0.95))
        }
        .ignoresSafeArea()
        .onExitCommand { onClose() }
    }

    private func section(_ t: String) -> some View {
        Eyebrow(text: t).padding(.top, Theme.s(8))
    }

    private func option(_ title: String, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack {
                Image(systemName: selected ? "checkmark.circle.fill" : "circle")
                    .foregroundStyle(selected ? Theme.accentBright : Theme.textFaint)
                Text(title).font(Theme.label(15)).foregroundStyle(.white)
                Spacer()
            }
            .padding(.horizontal, Theme.s(14)).padding(.vertical, Theme.s(10))
            .background(RoundedRectangle(cornerRadius: Theme.s(12)).fill(selected ? Theme.accent.opacity(0.25) : Color.white.opacity(0.05)))
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.03, cornerRadius: 12))
    }
}
#endif
