import Foundation

/**
 Langue de l'app : français ou anglais. Demandée au premier lancement (`LanguageView`), modifiable
 dans les réglages. Tant qu'aucun choix n'est fait : celle de l'appareil (la première de ses langues
 qui est le français ou l'anglais, l'anglais pour toutes les autres).
 */
enum Lang {
    static let fr = "fr", en = "en"
    private static let key = "appLanguage"

    /// Langue choisie, ou nil tant que l'écran de choix n'a pas été validé. Les essais sur simulateur
    /// l'imposent avec « -FluxTVLang en » (argument de lancement : rien n'est enregistré).
    static var chosen: String? {
        let d = UserDefaults.standard
        let v = d.string(forKey: "FluxTVLang") ?? d.string(forKey: key)
        return v == fr || v == en ? v : nil
    }

    /// Langue proposée d'office, d'après les langues réglées sur l'appareil.
    static var device: String {
        for l in Locale.preferredLanguages {
            let code = String(l.prefix(2)).lowercased()
            if code == fr || code == en { return code }
        }
        return en
    }

    static var current: String { Tr.english ? en : fr }

    /// Nom de la langue, toujours écrit dans cette langue (comme dans tous les sélecteurs).
    static func name(_ code: String) -> String { code == fr ? "Français" : "English" }

    /// Au démarrage : la langue choisie (ou celle de l'appareil) s'applique aux textes et aux dates.
    static func apply() { use(chosen ?? device) }

    /// Mémorise le choix. Les éléments fournis par le système (clavier, boutons standard) suivent au
    /// prochain lancement.
    static func save(_ code: String) {
        let d = UserDefaults.standard
        d.set(code, forKey: key)
        d.set([code], forKey: "AppleLanguages")
        use(code)
    }

    private static func use(_ code: String) {
        Tr.english = code == en
        // En anglais, l'heure suit l'horloge réglée sur l'appareil (« 8:05 PM » ou « 20:05 »)
        let pattern = DateFormatter.dateFormat(fromTemplate: "j", options: 0, locale: .current) ?? ""
        Tr.clock24 = !pattern.contains("a")
    }
}
