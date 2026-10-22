import Foundation
import UIKit
import Combine
import AVFoundation
#if os(tvOS)
import TVVLCKit
#else
import MobileVLCKit
#endif

/// Piste audio ou de sous-titres.
struct MediaTrack: Identifiable, Hashable {
    let id: Int32
    let name: String
}

/// Format d'image.
enum AspectMode: String, CaseIterable {
    case fit, fill, stretch

    var label: String {
        switch self {
        case .fit: return "Adapté"
        case .fill: return "Remplir l'écran"
        case .stretch: return "Étiré"
        }
    }
}

/**
 Moteur de lecture : VLC (VLCKit). Il lit tout ce que les fournisseurs envoient — flux
 MPEG-TS du direct, HLS, films MKV / AVI, son AC3, E-AC3 et DTS — là où le lecteur d'Apple
 refuse la moitié des formats.
 */
final class VideoEngine: NSObject, ObservableObject, VLCMediaPlayerDelegate {
    enum Status: Equatable { case idle, opening, buffering, playing, paused, ended, failed }

    @Published private(set) var status: Status = .idle
    /// Position et durée, en secondes (durée 0 = direct).
    @Published private(set) var time: Double = 0
    @Published private(set) var duration: Double = 0
    @Published private(set) var audioTracks: [MediaTrack] = []
    @Published private(set) var subtitleTracks: [MediaTrack] = []
    @Published private(set) var currentAudio: Int32 = -1
    @Published private(set) var currentSubtitle: Int32 = -1
    @Published private(set) var aspect: AspectMode = .fit
    @Published private(set) var hasVideo = false

    let view: UIView = {
        let v = UIView()
        v.backgroundColor = .black
        v.isUserInteractionEnabled = false
        return v
    }()

    private let player = VLCMediaPlayer()
    private var lastTimeUpdate = Date.distantPast

    override init() {
        super.init()
        player.delegate = self
        player.drawable = view
    }

    deinit {
        player.delegate = nil
        player.stop()
    }

    static func configureAudioSession() {
        let s = AVAudioSession.sharedInstance()
        try? s.setCategory(.playback, mode: .moviePlayback, options: [])
        try? s.setActive(true)
    }

    // ───────────────────────────── Ouverture ─────────────────────────────

    /// Ouvre un contenu. `caching` : mémoire tampon réseau (ms).
    func open(_ item: PlayItem, caching: Int) {
        let media = VLCMedia(url: item.url)
        let buffer = item.isLive ? caching : max(caching, 3000)
        media.addOption(":network-caching=\(buffer)")
        media.addOption(":live-caching=\(buffer)")
        media.addOption(":http-reconnect")
        if let ua = item.userAgent, !ua.isEmpty { media.addOption(":http-user-agent=\(ua)") }
        if let start = item.startAt, start > 5 { media.addOption(":start-time=\(Int(start))") }
        onMain {
            self.status = .opening
            self.time = item.startAt ?? 0
            self.duration = 0
            self.audioTracks = []
            self.subtitleTracks = []
            self.hasVideo = false
        }
        player.media = media
        player.play()
        applyAspect()
    }

    func stop() {
        player.stop()
        onMain { self.status = .idle }
    }

    // ───────────────────────────── Commandes ─────────────────────────────

    var isPlaying: Bool { player.isPlaying }

    func togglePause() {
        if player.isPlaying { player.pause() } else { player.play() }
    }

    func play() { player.play() }
    func pause() { if player.canPause { player.pause() } }

    var isSeekable: Bool { player.isSeekable && duration > 0 }

    func seek(to seconds: Double) {
        guard isSeekable else { return }
        let target = max(0, min(seconds, duration - 1))
        player.time = VLCTime(int: Int32(target * 1000))
        onMain { self.time = target }
    }

    func jump(by seconds: Double) { seek(to: time + seconds) }

    func selectAudio(_ id: Int32) {
        player.currentAudioTrackIndex = id
        onMain { self.currentAudio = id }
    }

    func selectSubtitle(_ id: Int32) {
        player.currentVideoSubTitleIndex = id
        onMain { self.currentSubtitle = id }
    }

    func setAspect(_ mode: AspectMode) {
        onMain { self.aspect = mode }
        aspectMode = mode
        applyAspect()
    }

    private var aspectMode: AspectMode = .fit

    /// Recadrage d'après les proportions de l'écran (remplir) ou déformation (étiré).
    func applyAspect() {
        let size = view.bounds.size
        let ratio: String? = size.width > 0 && size.height > 0
            ? "\(Int(size.width.rounded())):\(Int(size.height.rounded()))" : nil
        switch aspectMode {
        case .fit:
            setCString(nil) { self.player.videoCropGeometry = $0 }
            setCString(nil) { self.player.videoAspectRatio = $0 }
        case .fill:
            setCString(nil) { self.player.videoAspectRatio = $0 }
            setCString(ratio) { self.player.videoCropGeometry = $0 }
        case .stretch:
            setCString(nil) { self.player.videoCropGeometry = $0 }
            setCString(ratio) { self.player.videoAspectRatio = $0 }
        }
    }

    private func setCString(_ s: String?, _ apply: (UnsafeMutablePointer<CChar>?) -> Void) {
        guard let s else { apply(nil); return }
        // VLC recopie la valeur : le pointeur n'a besoin d'être valide que pendant l'appel
        s.withCString { apply(UnsafeMutablePointer(mutating: $0)) }
    }

    // ───────────────────────────── Événements VLC ─────────────────────────────

    func mediaPlayerStateChanged(_ aNotification: Notification) {
        let state = player.state
        let newStatus: Status?
        switch state {
        case .opening: newStatus = .opening
        case .buffering: newStatus = player.isPlaying ? .playing : .buffering
        case .playing: newStatus = .playing
        case .paused: newStatus = .paused
        case .ended: newStatus = .ended
        case .error: newStatus = .failed
        case .stopped: newStatus = status == .ended || status == .failed ? nil : .idle
        case .esAdded: newStatus = nil
        @unknown default: newStatus = nil
        }
        refreshTracks()
        onMain {
            if let s = newStatus, s != self.status { self.status = s }
        }
    }

    func mediaPlayerTimeChanged(_ aNotification: Notification) {
        // Limité à 4 mises à jour par seconde (l'affichage n'a pas besoin de plus)
        let now = Date()
        guard now.timeIntervalSince(lastTimeUpdate) > 0.25 else { return }
        lastTimeUpdate = now
        let t = Double(player.time.intValue) / 1000
        let lengthMs = player.media?.length.intValue ?? 0
        let d = lengthMs > 0 ? Double(lengthMs) / 1000 : 0
        let video = player.hasVideoOut
        onMain {
            self.time = max(0, t)
            if abs(self.duration - d) > 0.5 { self.duration = d }
            if self.status == .opening || self.status == .buffering { self.status = .playing }
            if video != self.hasVideo { self.hasVideo = video }
            if self.audioTracks.isEmpty { self.refreshTracks() }
        }
    }

    private func refreshTracks() {
        let audio = Self.tracks(names: player.audioTrackNames, indexes: player.audioTrackIndexes)
        let subs = Self.tracks(names: player.videoSubTitlesNames, indexes: player.videoSubTitlesIndexes)
        let a = player.currentAudioTrackIndex
        let s = player.currentVideoSubTitleIndex
        onMain {
            if audio != self.audioTracks { self.audioTracks = audio }
            if subs != self.subtitleTracks { self.subtitleTracks = subs }
            self.currentAudio = a
            self.currentSubtitle = s
        }
    }

    private static func tracks(names: [Any], indexes: [Any]) -> [MediaTrack] {
        zip(names, indexes).compactMap { name, index in
            let id: Int32
            if let n = index as? NSNumber { id = n.int32Value } else if let i = index as? Int { id = Int32(i) } else { return nil }
            let label = (name as? String).map(Self.trackLabel) ?? "Piste \(id)"
            return MediaTrack(id: id, name: id < 0 ? "Désactivés" : label)
        }
    }

    /// « Track 1 - [French] » → « French », langues courantes en français.
    static func trackLabel(_ raw: String) -> String {
        var s = raw
        if let open = s.lastIndex(of: "["), let close = s.lastIndex(of: "]"), open < close {
            s = String(s[s.index(after: open)..<close])
        } else if let dash = s.range(of: " - ") {
            s = String(s[dash.upperBound...])
        }
        let names = ["french": "Français", "english": "Anglais", "german": "Allemand", "spanish": "Espagnol",
                     "italian": "Italien", "arabic": "Arabe", "portuguese": "Portugais", "dutch": "Néerlandais",
                     "fre": "Français", "fra": "Français", "eng": "Anglais", "ger": "Allemand", "deu": "Allemand",
                     "spa": "Espagnol", "ita": "Italien", "ara": "Arabe", "disable": "Désactivés"]
        let key = s.trimmingCharacters(in: .whitespaces).lowercased()
        return names[key] ?? s.trimmingCharacters(in: .whitespaces)
    }

    private func onMain(_ work: @escaping () -> Void) {
        if Thread.isMainThread { work() } else { DispatchQueue.main.async(execute: work) }
    }
}
