import Foundation

/**
 Langue des textes affichés : français ou anglais. Réglée au démarrage par l'app (choix du premier
 lancement, modifiable dans les réglages) et par les tests. Les deux versions de chaque texte sont
 côte à côte dans le code : `T("Réglages", "Settings")`.
 */
public enum Tr {
    public static var english = false
    /// Horloge sur 24 heures (en anglais, l'appareil peut préférer « 8:05 PM »).
    public static var clock24 = true
    /// Dates et nombres : français de France ou anglais des États-Unis.
    public static var locale: Locale { Locale(identifier: english ? "en_US" : "fr_FR") }
}

/// Texte dans la langue de l'app.
public func T(_ fr: String, _ en: String) -> String { Tr.english ? en : fr }
