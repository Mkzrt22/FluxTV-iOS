using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FluxTV.Core
{
    /// <summary>
    /// Lecture tolérante des réponses des fournisseurs : selon les panels, un même champ arrive en
    /// texte, en nombre, à null, voire en tableau. Objets = Dictionary, tableaux = List, nombres =
    /// double. Un élément mal formé ne fait jamais échouer toute une liste.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var p = new Parser(text);
                return p.Value();
            }
            catch (FormatException) { return null; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        public static Dictionary<string, object> ParseObject(string text) => Parse(text) as Dictionary<string, object>;

        public static List<object> ParseArray(string text) => Parse(text) as List<object>;

        /// <summary>
        /// Objets d'une grande liste, lus un par un (peu de mémoire même avec 100 000 entrées).
        /// Accepte un tableau, ou un objet dont les valeurs sont les éléments.
        /// </summary>
        public static void EachObject(string text, Action<Dictionary<string, object>> each)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                new Parser(text).EachItem(v =>
                {
                    if (v is Dictionary<string, object> o) each(o);
                });
            }
            catch (FormatException) { }
            catch (ArgumentOutOfRangeException) { }
        }

        // ───────────────────────────── Écriture ─────────────────────────────

        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Number(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        // ───────────────────────────── Analyse ─────────────────────────────

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string text)
            {
                s = text;
                i = 0;
            }

            void Ws()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '﻿') i++;
                    else break;
                }
            }

            public object Value()
            {
                Ws();
                if (i >= s.Length) throw new FormatException();
                switch (s[i])
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default: return Num();
                }
            }

            public void EachItem(Action<object> each)
            {
                Ws();
                if (i >= s.Length) return;
                if (s[i] == '[')
                {
                    i++;
                    Ws();
                    if (i < s.Length && s[i] == ']') return;
                    while (true)
                    {
                        each(Value());
                        Ws();
                        if (i >= s.Length) return;
                        if (s[i] == ',') { i++; continue; }
                        return;
                    }
                }
                if (s[i] == '{')
                {
                    foreach (var v in Obj().Values) each(v);
                }
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw new FormatException();
                i += w.Length;
            }

            Dictionary<string, object> Obj()
            {
                i++;
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                Ws();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws();
                    if (i >= s.Length || s[i] != '"') throw new FormatException();
                    var key = Str();
                    Ws();
                    if (i >= s.Length || s[i] != ':') throw new FormatException();
                    i++;
                    d[key] = Value();
                    Ws();
                    if (i >= s.Length) throw new FormatException();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException();
                }
            }

            List<object> Arr()
            {
                i++;
                var list = new List<object>();
                Ws();
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(Value());
                    Ws();
                    if (i >= s.Length) throw new FormatException();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw new FormatException();
                }
            }

            string Str()
            {
                i++; // guillemet ouvrant
                int start = i;
                StringBuilder sb = null;
                while (true)
                {
                    if (i >= s.Length) throw new FormatException();
                    char c = s[i];
                    if (c == '"')
                    {
                        string result = sb == null ? s.Substring(start, i - start) : sb.Append(s, start, i - start).ToString();
                        i++;
                        return result;
                    }
                    if (c != '\\') { i++; continue; }
                    if (sb == null) sb = new StringBuilder();
                    sb.Append(s, start, i - start);
                    if (i + 1 >= s.Length) throw new FormatException();
                    char e = s[i + 1];
                    i += 2;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException();
                            int code;
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw new FormatException();
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                    start = i;
                }
            }

            object Num()
            {
                int start = i;
                while (i < s.Length)
                {
                    char c = s[i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                    else break;
                }
                if (i == start) throw new FormatException();
                double d;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    throw new FormatException();
                return d;
            }
        }
    }

    /// <summary>Lecture des champs d'un objet JSON, quel que soit leur type d'origine.</summary>
    public static class JsonFields
    {
        /// <summary>Texte non vide (les nombres sont convertis, "null" et "" donnent null).</summary>
        public static string Str(this Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return null;
            string s;
            if (v is string x) s = x;
            else if (v is bool b) s = b ? "1" : "0";
            else if (v is double d)
                s = Math.Abs(d) < 1e15 && d == Math.Floor(d)
                    ? ((long)d).ToString(CultureInfo.InvariantCulture)
                    : d.ToString("R", CultureInfo.InvariantCulture);
            else return null;
            s = s.Trim();
            return s.Length == 0 || s == "null" ? null : s;
        }

        public static long? Long(this Dictionary<string, object> o, string key)
        {
            var s = o.Str(key);
            if (s == null) return null;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d)
                && Math.Abs(d) < 9e18)
                return (long)d;
            return null;
        }

        public static int? Int(this Dictionary<string, object> o, string key)
        {
            var l = o.Long(key);
            if (l == null || l > int.MaxValue || l < int.MinValue) return null;
            return (int)l.Value;
        }

        public static double? Dbl(this Dictionary<string, object> o, string key)
        {
            var s = o.Str(key);
            if (s == null) return null;
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                && !double.IsNaN(d) && !double.IsInfinity(d))
                return d;
            return null;
        }

        /// <summary>"1", 1 ou true.</summary>
        public static bool Flag(this Dictionary<string, object> o, string key)
        {
            var s = o.Str(key);
            if (s == null) return false;
            s = s.ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes";
        }

        public static Dictionary<string, object> Obj(this Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> Arr(this Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as List<object> : null;

        /// <summary>Date en secondes Unix (texte ou nombre).</summary>
        public static DateTimeOffset? UnixDate(this Dictionary<string, object> o, string key)
        {
            var t = o.Long(key);
            if (t == null || t <= 0 || t > 32503680000) return null;
            return DateTimeOffset.FromUnixTimeSeconds(t.Value);
        }

        /// <summary>Première adresse http d'un champ « texte ou tableau de textes » (backdrop_path…).</summary>
        public static string FirstUrl(this Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v)) return null;
            if (v is string s) return s.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? s.Trim() : null;
            if (v is List<object> list)
                foreach (var item in list)
                    if (item is string u && u.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return u.Trim();
            return null;
        }

        /// <summary>Note sur 10 (« 7.5 », 7.5 ; « 0 » donne null ; une note sur 100 est ramenée sur 10).</summary>
        public static double? Rating(this Dictionary<string, object> o, string key)
        {
            var d = o.Dbl(key);
            if (d == null || d <= 0) return null;
            return d > 10 ? d / 10 : d;
        }
    }
}
