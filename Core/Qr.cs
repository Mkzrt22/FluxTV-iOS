using System;
using System.Collections.Generic;
using System.Text;

namespace FluxTV.Core
{
    /// <summary>
    /// QR code (norme ISO/IEC 18004), en mode « octets » : de quoi afficher une adresse à scanner avec
    /// le téléphone. Écrit pour FluxTV, sans dépendance ; même méthode que les générateurs de référence
    /// (codes de Reed-Solomon, blocs entrelacés, choix du masque le plus lisible).
    /// </summary>
    public sealed class QrCode
    {
        public enum Correction { L, M, Q, H }

        public int Version { get; }
        public int Size { get; }
        public Correction Level { get; }
        public int Mask { get; }

        readonly bool[,] modules;    // [ligne, colonne] : true = module sombre
        bool[,] function;            // modules réservés (repères, format, version), hors données

        /// <summary>Module sombre en colonne x, ligne y (0 ≤ x, y &lt; Size).</summary>
        public bool this[int x, int y] => modules[y, x];

        public static QrCode Encode(string text, Correction level = Correction.M) =>
            EncodeBytes(Encoding.UTF8.GetBytes(text ?? ""), level);

        public static QrCode EncodeBytes(byte[] data, Correction level)
        {
            int version, capacityBits;
            for (version = 1; ; version++)
            {
                if (version > 40) throw new ArgumentException("Texte trop long pour un QR code.");
                capacityBits = DataCodewords(version, level) * 8;
                if (4 + (version <= 9 ? 8 : 16) + data.Length * 8 <= capacityBits) break;
            }
            var bits = new List<bool>(capacityBits);
            Append(bits, 0x4, 4);                                   // mode « octets »
            Append(bits, data.Length, version <= 9 ? 8 : 16);
            foreach (var b in data) Append(bits, b, 8);
            Append(bits, 0, Math.Min(4, capacityBits - bits.Count)); // fin des données
            Append(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11) Append(bits, pad, 8);
            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            return new QrCode(version, level, codewords);
        }

        QrCode(int version, Correction level, byte[] dataCodewords)
        {
            Version = version;
            Size = version * 4 + 17;
            Level = level;
            modules = new bool[Size, Size];
            function = new bool[Size, Size];
            DrawFunctionPatterns();
            DrawCodewords(AddCorrectionAndInterleave(dataCodewords));
            // Le masque qui donne l'image la plus facile à lire
            int best = 0;
            long lowest = long.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                ApplyMask(m);
                DrawFormatBits(m);
                long p = Penalty();
                if (p < lowest) { lowest = p; best = m; }
                ApplyMask(m);
            }
            Mask = best;
            ApplyMask(best);
            DrawFormatBits(best);
            function = null;
        }

        // ───────────────────────────── Repères fixes ─────────────────────────────

        void SetFunction(int x, int y, bool dark)
        {
            modules[y, x] = dark;
            function[y, x] = true;
        }

        void DrawFunctionPatterns()
        {
            for (int i = 0; i < Size; i++)
            {
                SetFunction(6, i, i % 2 == 0);
                SetFunction(i, 6, i % 2 == 0);
            }
            DrawFinder(3, 3);
            DrawFinder(Size - 4, 3);
            DrawFinder(3, Size - 4);
            var pos = AlignmentPositions();
            int n = pos.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!(i == 0 && j == 0 || i == 0 && j == n - 1 || i == n - 1 && j == 0))
                        DrawAlignment(pos[i], pos[j]);
            DrawFormatBits(0);
            DrawVersion();
        }

        void DrawFinder(int x, int y)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetFunction(xx, yy, dist != 2 && dist != 4);
                }
        }

        void DrawAlignment(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        int[] AlignmentPositions()
        {
            if (Version == 1) return new int[0];
            int count = Version / 7 + 2;
            int step = Version == 32 ? 26 : (Version * 4 + count * 2 + 1) / (count * 2 - 2) * 2;
            var result = new int[count];
            result[0] = 6;
            for (int i = count - 1, pos = Size - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        static int LevelBits(Correction level)
        {
            switch (level)
            {
                case Correction.L: return 1;
                case Correction.M: return 0;
                case Correction.Q: return 3;
                default: return 2;
            }
        }

        void DrawFormatBits(int mask)
        {
            int data = LevelBits(Level) << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            for (int i = 0; i <= 5; i++) SetFunction(8, i, Bit(bits, i));
            SetFunction(8, 7, Bit(bits, 6));
            SetFunction(8, 8, Bit(bits, 7));
            SetFunction(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(bits, i));
            for (int i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, Bit(bits, i));
            SetFunction(8, Size - 8, true);
        }

        void DrawVersion()
        {
            if (Version < 7) return;
            int rem = Version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = Version << 12 | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = Bit(bits, i);
                int a = Size - 11 + i % 3, b = i / 3;
                SetFunction(a, b, bit);
                SetFunction(b, a, bit);
            }
        }

        // ───────────────────────────── Données ─────────────────────────────

        byte[] AddCorrectionAndInterleave(byte[] data)
        {
            int blocks = Blocks[(int)Level][Version];
            int eccLen = EccPerBlock[(int)Level][Version];
            int raw = RawDataModules(Version) / 8;
            int shortBlocks = blocks - raw % blocks;
            int shortLen = raw / blocks;
            var divisor = ReedSolomonDivisor(eccLen);
            var all = new List<byte[]>(blocks);
            for (int i = 0, k = 0; i < blocks; i++)
            {
                int datLen = shortLen - eccLen + (i < shortBlocks ? 0 : 1);
                var dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                var ecc = ReedSolomonRemainder(dat, divisor);
                var block = new byte[shortLen + 1];
                Array.Copy(dat, 0, block, 0, datLen);
                Array.Copy(ecc, 0, block, block.Length - eccLen, eccLen);
                all.Add(block);
            }
            var result = new byte[raw];
            int r = 0;
            for (int i = 0; i < shortLen + 1; i++)
                for (int j = 0; j < blocks; j++)
                    if (i != shortLen - eccLen || j >= shortBlocks) result[r++] = all[j][i];
            return result;
        }

        void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < Size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? Size - 1 - vert : vert;
                        if (!function[y, x] && i < data.Length * 8)
                        {
                            modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
            }
        }

        void ApplyMask(int mask)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (function[y, x]) continue;
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (invert) modules[y, x] = !modules[y, x];
                }
        }

        /// <summary>Pénalités de la norme : longues suites, carrés 2 × 2, faux repères, déséquilibre sombre/clair.</summary>
        long Penalty()
        {
            long p = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < Size; a++)
                {
                    int run = 1;
                    for (int b = 1; b <= Size; b++)
                    {
                        if (b < Size && At(pass, a, b) == At(pass, a, b - 1)) { run++; continue; }
                        if (run >= 5) p += 3 + (run - 5);
                        run = 1;
                    }
                    // 1011101 bordé de 4 modules clairs d'un côté (hors du code : clair)
                    for (int b = -4; b < Size; b++)
                    {
                        if (Matches(pass, a, b, FinderLike)) p += 40;
                        if (Matches(pass, a, b, FinderLikeReversed)) p += 40;
                    }
                }
            for (int y = 0; y < Size - 1; y++)
                for (int x = 0; x < Size - 1; x++)
                {
                    bool c = modules[y, x];
                    if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1]) p += 3;
                }
            int dark = 0;
            foreach (var m in modules) if (m) dark++;
            int total = Size * Size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            return p + k * 10;
        }

        static readonly bool[] FinderLike = { true, false, true, true, true, false, true, false, false, false, false };
        static readonly bool[] FinderLikeReversed = { false, false, false, false, true, false, true, true, true, false, true };

        bool At(int pass, int a, int b) => pass == 0 ? modules[a, b] : modules[b, a];

        bool Matches(int pass, int a, int start, bool[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                int b = start + i;
                bool v = b >= 0 && b < Size && At(pass, a, b);
                if (v != pattern[i]) return false;
            }
            return true;
        }

        // ───────────────────────────── Correction d'erreurs ─────────────────────────────

        static byte[] ReedSolomonDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)Multiply(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = Multiply(root, 0x02);
            }
            return result;
        }

        static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (var b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= (byte)Multiply(divisor[i], factor);
            }
            return result;
        }

        /// <summary>Produit dans GF(2⁸) modulo x⁸ + x⁴ + x³ + x² + 1.</summary>
        static int Multiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        // ───────────────────────────── Tables de la norme ─────────────────────────────

        static int RawDataModules(int version)
        {
            int result = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                int count = version / 7 + 2;
                result -= (25 * count - 10) * count - 55;
                if (version >= 7) result -= 36;
            }
            return result;
        }

        static int DataCodewords(int version, Correction level) =>
            RawDataModules(version) / 8 - EccPerBlock[(int)level][version] * Blocks[(int)level][version];

        static readonly int[][] EccPerBlock =
        {
            new[] { -1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            new[] { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 },
            new[] { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            new[] { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
        };

        static readonly int[][] Blocks =
        {
            new[] { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 },
            new[] { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 },
            new[] { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 },
            new[] { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 },
        };

        // ───────────────────────────── Outils ─────────────────────────────

        static void Append(List<bool> bits, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        static bool Bit(int value, int i) => ((value >> i) & 1) != 0;
    }
}
