import Foundation
import Security
import SwiftUI

/**
 Compte ou playlist de l'utilisateur, chiffré sur l'appareil : dans le trousseau d'Apple, ou à
 défaut (app signée sans les droits du trousseau, simulateur) dans un fichier chiffré par iOS,
 lisible uniquement par FluxTV et exclu des sauvegardes.
 */
enum SourceKeychain {
    private static let service = "bzh.fluxtv.source"
    private static let account = "current"

    private static var fallbackURL: URL {
        LibraryFile.defaultURL.deletingLastPathComponent().appendingPathComponent("source.json")
    }

    static func load() -> Source? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var item: CFTypeRef?
        if SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess, let data = item as? Data,
           let source = try? JSONDecoder().decode(Source.self, from: data) {
            return source
        }
        guard let data = try? Data(contentsOf: fallbackURL) else { return nil }
        return try? JSONDecoder().decode(Source.self, from: data)
    }

    @discardableResult
    static func save(_ source: Source) -> Bool {
        guard let data = try? JSONEncoder().encode(source) else { return false }
        delete()
        let attrs: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        ]
        if SecItemAdd(attrs as CFDictionary, nil) == errSecSuccess { return true }
        // Trousseau refusé : fichier chiffré par iOS (sinon le compte serait redemandé à chaque lancement)
        do {
            var url = fallbackURL
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: url, options: [.atomic, .completeFileProtectionUntilFirstUserAuthentication])
            var values = URLResourceValues()
            values.isExcludedFromBackup = true
            try? url.setResourceValues(values)
            return true
        } catch {
            return false
        }
    }

    static func delete() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(query as CFDictionary)
        try? FileManager.default.removeItem(at: fallbackURL)
    }
}

/// Favoris, historique et reprises, enregistrés sur l'appareil.
@MainActor
final class LibraryStore: ObservableObject {
    @Published private(set) var data: LibraryData
    private let file: LibraryFile
    private var saveTask: Task<Void, Never>?

    init(file: LibraryFile = LibraryFile(url: LibraryFile.defaultURL)) {
        self.file = file
        self.data = file.load()
    }

    private func update(_ change: (inout LibraryData) -> Void) {
        var d = data
        change(&d)
        data = d
        scheduleSave()
    }

    private func scheduleSave() {
        saveTask?.cancel()
        let snapshot = data
        let file = self.file
        saveTask = Task.detached(priority: .utility) {
            try? await Task.sleep(nanoseconds: 600_000_000)
            if Task.isCancelled { return }
            try? file.save(snapshot)
        }
    }

    /// Enregistrement immédiat (passage en arrière-plan).
    func flush() {
        saveTask?.cancel()
        try? file.save(data)
    }

    func isFavorite(_ id: String) -> Bool { data.isFavorite(id) }

    @discardableResult
    func toggleFavorite(_ ref: ContentRef) -> Bool {
        var now = false
        update { now = $0.toggleFavorite(ref) }
        return now
    }

    func moveFavorite(from: Int, to: Int) { update { $0.moveFavorite(from: from, to: to) } }
    func addHistory(_ ref: ContentRef) { update { $0.addHistory(ref) } }
    func removeHistory(_ id: String) { update { $0.removeHistory(id) } }

    @discardableResult
    func savePosition(_ ref: ContentRef, position: Double, duration: Double) -> Bool {
        var finished = false
        update { finished = $0.savePosition(ref, position: position, duration: duration) }
        return finished
    }

    func resume(_ id: String) -> PlaybackPosition? { data.resume(id) }
    func isWatched(_ id: String) -> Bool { data.isWatched(id) }
    func setWatched(_ id: String, _ watched: Bool) {
        update { lib in
            if watched { lib.markWatched(id) } else { lib.markUnwatched(id) }
        }
    }
    func clearAll() { update { $0 = LibraryData() } }
}

/// Réglages de l'utilisateur.
@MainActor
final class SettingsStore: ObservableObject {
    private let defaults = UserDefaults.standard

    @Published var hideAdult: Bool { didSet { defaults.set(hideAdult, forKey: "hideAdult") } }
    @Published var autoplayNext: Bool { didSet { defaults.set(autoplayNext, forKey: "autoplayNext") } }
    /// Format du direct : "auto", "ts" ou "m3u8".
    @Published var liveFormat: String { didSet { defaults.set(liveFormat, forKey: "liveFormat") } }
    /// Mémoire tampon du lecteur, en millisecondes.
    @Published var networkCaching: Int { didSet { defaults.set(networkCaching, forKey: "networkCaching") } }
    @Published var lastTab: String { didSet { defaults.set(lastTab, forKey: "lastTab") } }

    init() {
        // (self n'est utilisable qu'une fois toutes les propriétés initialisées)
        let d = UserDefaults.standard
        d.register(defaults: ["hideAdult": true, "autoplayNext": true, "liveFormat": "auto",
                              "networkCaching": 1500, "lastTab": "home"])
        hideAdult = d.bool(forKey: "hideAdult")
        autoplayNext = d.bool(forKey: "autoplayNext")
        liveFormat = d.string(forKey: "liveFormat") ?? "auto"
        networkCaching = d.integer(forKey: "networkCaching")
        lastTab = d.string(forKey: "lastTab") ?? "home"
    }
}
