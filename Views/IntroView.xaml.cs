using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace FluxTV.Views
{
    /// <summary>
    /// Animation de lancement, reprise de la version PC (même déroulé, mêmes durées). Le fond animé
    /// (nébuleuse, étoiles, ondes de choc, étincelles) est dessiné par Win2D, comme le canvas du PC.
    /// </summary>
    public sealed partial class IntroView : UserControl
    {
        /// <summary>La plongée commence : l'app peut apparaître derrière.</summary>
        public event Action Revealing;
        /// <summary>Animation terminée : la vue peut être retirée.</summary>
        public event Action Finished;

        readonly Starfield field = new Starfield();
        readonly List<Grid> letters = new List<Grid>();
        readonly TaskCompletionSource<bool> shown = new TaskCompletionSource<bool>();

        public IntroView()
        {
            InitializeComponent();
            Fx.Draw += field.Draw;
            BuildWord();
            Loaded += (s, e) =>
            {
                Focus(FocusState.Programmatic);
                shown.TrySetResult(true);
            };
            SizeChanged += (s, e) => UpdateCenter();
        }

        /// <summary>Le fond animé est centré sur l'emblème, comme sur PC.</summary>
        void UpdateCenter()
        {
            try
            {
                var p = MarkSlot.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(50, 50));
                field.SetCenter((float)p.X, (float)p.Y);
            }
            catch (Exception) { }
        }

        /// <summary>« FluxTV » lettre par lettre, du blanc au violet, avec un halo.</summary>
        void BuildWord()
        {
            const string text = "FluxTV";
            for (int i = 0; i < text.Length; i++)
            {
                var k = i / (double)(text.Length - 1);
                var color = Color.FromArgb(255, Mix(0xF1, 0xC0, k), Mix(0xF2, 0x84, k), Mix(0xFF, 0xFC, k));
                var glow = new Grid();
                var letter = new TextBlock
                {
                    Text = text[i].ToString(),
                    FontFamily = (FontFamily)Application.Current.Resources["FontDisplay"],
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    FontSize = 66,
                    Foreground = new SolidColorBrush(color),
                    IsTextScaleFactorEnabled = false,
                };
                var cell = new Grid
                {
                    Opacity = 0,
                    Margin = new Thickness(0, 0, -2, 0),
                    Projection = new PlaneProjection { RotationX = 80, CenterOfRotationY = 1 },
                    RenderTransform = new TranslateTransform { Y = -33 },
                };
                cell.Children.Add(glow);
                cell.Children.Add(letter);
                letter.SizeChanged += (s, e) => AttachGlow(glow, letter);
                Word.Children.Add(cell);
                letters.Add(cell);
            }
        }

        static byte Mix(int a, int b, double k) => (byte)Math.Round(a + (b - a) * k);

        static void AttachGlow(Grid host, TextBlock letter)
        {
            try
            {
                var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
                var shadow = compositor.CreateDropShadow();
                shadow.Mask = letter.GetAlphaMask();
                shadow.Color = Color.FromArgb(140, 124, 92, 245);
                shadow.BlurRadius = 24;
                var sprite = compositor.CreateSpriteVisual();
                sprite.Size = new Vector2((float)letter.ActualWidth, (float)letter.ActualHeight);
                sprite.Shadow = shadow;
                ElementCompositionPreview.SetElementChildVisual(host, sprite);
            }
            catch (Exception) { }
        }

        // ───────────────────────────── Déroulé ─────────────────────────────

        /// <summary>
        /// Lance l'animation. Elle attend ensuite que l'app soit prête (6 s au plus), puis plonge vers l'app.
        /// hold : reste sur l'écran d'intro (captures d'écran automatiques).
        /// </summary>
        public async void Play(Func<bool> isReady, Func<string> status, bool hold = false)
        {
            // L'animation part quand la vue est à l'écran (après l'écran de démarrage du système)
            await shown.Task;
            UpdateCenter();
            Status.Text = (status() ?? "").ToUpperInvariant();

            var intro = new Storyboard();
            // Rayons qui apparaissent
            Add(intro, Rays, "Opacity", 0, K(0, 0), K(1600, 1, EaseOut(2)));
            // 1. Emblème : bascule 3D avec rebond
            Add(intro, Mark, "Opacity", 150, K(0, 0), K(665, 1, EaseOut(2)));
            Add(intro, MarkFlip, "RotationY", 150, K(0, -120), K(665, 12, EaseOut(3)), K(950, 0, InOut()));
            Add(intro, MarkFlip, "RotationX", 150, K(0, 25), K(665, -4, EaseOut(3)), K(950, 0, InOut()));
            Add(intro, MarkScale, "ScaleX", 150, K(0, 0.25), K(665, 1.1, EaseOut(3)), K(950, 1, InOut()));
            Add(intro, MarkScale, "ScaleY", 150, K(0, 0.25), K(665, 1.1, EaseOut(3)), K(950, 1, InOut()));
            // La lecture surgit, les ondes se dessinent
            Add(intro, PlayPop, "ScaleX", 700, K(0, 0), K(390, 1.25, EaseOut(2)), K(600, 1, InOut()));
            Add(intro, PlayPop, "ScaleY", 700, K(0, 0), K(390, 1.25, EaseOut(2)), K(600, 1, InOut()));
            Add(intro, PlayPop, "Rotation", 700, K(0, -40), K(390, 6, EaseOut(2)), K(600, 0, InOut()));
            Add(intro, Wave1, "StrokeDashOffset", 950, K(0, 5.2), K(420, 0, EaseOut(2)));
            Add(intro, Wave2, "StrokeDashOffset", 1120, K(0, 8.9), K(520, 0, EaseOut(2)));
            // 2. Salve : reflet, battement, lettres
            Add(intro, Glare0, "Offset", 1020, K(0, -0.6), K(1000, 1.4, EaseOut(4)));
            Add(intro, Glare1, "Offset", 1020, K(0, -0.45), K(1000, 1.55, EaseOut(4)));
            Add(intro, Glare2, "Offset", 1020, K(0, -0.3), K(1000, 1.7, EaseOut(4)));
            Add(intro, PulseScale, "ScaleX", 1020, K(0, 1), K(220, 1.12, EaseOut(2)), K(520, 1, Spring()));
            Add(intro, PulseScale, "ScaleY", 1020, K(0, 1), K(220, 1.12, EaseOut(2)), K(520, 1, Spring()));
            for (int i = 0; i < letters.Count; i++)
            {
                var cell = letters[i];
                var begin = 1020 + i * 75;
                Add(intro, cell, "Opacity", begin, K(0, 0), K(450, 1, EaseOut(2)));
                Add(intro, cell.RenderTransform, "Y", begin, K(0, -33), K(900, 0, Spring()));
                Add(intro, cell.Projection, "RotationX", begin, K(0, 80), K(900, 0, Spring()));
            }
            // Slogan qui se resserre
            Add(intro, Tagline, "Opacity", 1520, K(0, 0), K(1100, 1, EaseOut(4)));
            Add(intro, TagSpread, "ScaleX", 1520, K(0, 1.7), K(1100, 1, EaseOut(4)));
            // Barre de chargement, état, signature
            Add(intro, Bar, "Opacity", 1670, K(0, 0), K(500, 1));
            Add(intro, Status, "Opacity", 1870, K(0, 0), K(600, 1));
            Add(intro, Credit, "Opacity", 2070, K(0, 0), K(1000, 1));
            Add(intro, BarScale, "ScaleX", 1670, K(0, 0.04), K(1600, 0.85, EaseOut(4)));
            intro.Begin();

            // En boucle : rayons qui tournent, reflet de la barre, ondes qui pulsent
            var loops = new Storyboard();
            Add(loops, RaysTurn, "Angle", 0, K(0, 0), K(40000, 360)).RepeatBehavior = RepeatBehavior.Forever;
            Add(loops, Shimmer, "X", 1670, K(0, -57), K(1200, 210, InOut())).RepeatBehavior = RepeatBehavior.Forever;
            Add(loops, Wave2, "Opacity", 2500, K(0, 0.55), K(450, 1), K(900, 0.55), K(1800, 0.55)).RepeatBehavior = RepeatBehavior.Forever;
            loops.Begin();

            await Task.Delay(1020);
            UpdateCenter();
            field.Burst(1);
            await Task.Delay(2050);

            // Attente : l'app charge ses chaînes pendant l'animation
            var waitStart = DateTime.UtcNow;
            while (true)
            {
                Status.Text = (status() ?? "").ToUpperInvariant();
                if (hold) { await Task.Delay(250); continue; }
                if (isReady() || (DateTime.UtcNow - waitStart).TotalSeconds > 6) break;
                await Task.Delay(120);
            }

            // Sortie : la barre se remplit, plongée à travers l'emblème, éclair, l'app apparaît
            var outro = new Storyboard();
            Add(outro, BarScale, "ScaleX", 0, K(0, 0.85), K(350, 1, EaseOut(2)));
            Add(outro, CoreZoom, "ScaleX", 300, K(0, 1), K(750, 5, EaseIn(3)));
            Add(outro, CoreZoom, "ScaleY", 300, K(0, 1), K(750, 5, EaseIn(3)));
            Add(outro, SplashCore, "Opacity", 300, K(0, 1), K(750, 0, EaseIn(2)));
            Add(outro, Credit, "Opacity", 300, K(0, 1), K(300, 0));
            Add(outro, Rays, "Opacity", 300, K(0, 1), K(300, 0));
            Add(outro, Flash, "Opacity", 680, K(0, 0), K(160, 0.85), K(900, 0, EaseOut(2)));
            Add(outro, Backdrop, "Opacity", 680, K(0, 1), K(700, 0, EaseOut(2)));
            Add(outro, Fx, "Opacity", 1180, K(0, 1), K(600, 0));
            outro.Begin();
            await Task.Delay(300);
            field.Outro();
            await Task.Delay(380);
            Revealing?.Invoke();
            await Task.Delay(1150);
            loops.Stop();
            Finished?.Invoke();
        }

        /// <summary>Libère le dessin Win2D (à appeler une fois la vue retirée).</summary>
        public void Release()
        {
            try
            {
                Fx.Paused = true;
                Fx.Draw -= field.Draw;
                Fx.RemoveFromVisualTree();
            }
            catch (Exception) { }
        }

        // ───────────────────────────── Outils d'animation ─────────────────────────────

        static (double Ms, double Value, EasingFunctionBase Ease) K(double ms, double value, EasingFunctionBase ease = null) =>
            (ms, value, ease);

        static EasingFunctionBase EaseOut(int power) => new PowerEase { Power = power, EasingMode = EasingMode.EaseOut };
        static EasingFunctionBase EaseIn(int power) => new PowerEase { Power = power, EasingMode = EasingMode.EaseIn };
        static EasingFunctionBase InOut() => new SineEase { EasingMode = EasingMode.EaseInOut };
        /// <summary>Rebond léger (cubic-bezier(.34,1.56,.64,1) de la version PC).</summary>
        static EasingFunctionBase Spring() => new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut };

        static DoubleAnimationUsingKeyFrames Add(Storyboard sb, DependencyObject target, string property, double beginMs,
            params (double Ms, double Value, EasingFunctionBase Ease)[] frames)
        {
            var anim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(beginMs),
                EnableDependentAnimation = true,
            };
            foreach (var f in frames)
                anim.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(f.Ms)),
                    Value = f.Value,
                    EasingFunction = f.Ease,
                });
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, property);
            sb.Children.Add(anim);
            return anim;
        }
    }

    /// <summary>
    /// Fond animé de l'intro (portage du canvas de la version PC) : nébuleuse qui respire, étoiles en
    /// « vitesse lumière », ondes de choc, étincelles et éclair. Dessiné sur le fil de rendu de Win2D.
    /// Les longueurs sont en pixels effectifs (960 × 540 sur la télé), la moitié des pixels du PC en 1080p.
    /// </summary>
    sealed class Starfield
    {
        sealed class Star { public float A, D, V, Z; public Color C; }
        sealed class Ring { public double Born; public float K; }
        sealed class Spark { public float X, Y, Vx, Vy, R, Life; public Color C; }

        static readonly Color[] Colors =
        {
            Color.FromArgb(255, 0xFF, 0xFF, 0xFF), Color.FromArgb(255, 0xC7, 0xD2, 0xFE), Color.FromArgb(255, 0xE9, 0xD5, 0xFF),
            Color.FromArgb(255, 0x8E, 0x97, 0xFF), Color.FromArgb(255, 0xF0, 0xAB, 0xFC),
        };

        readonly object gate = new object();
        readonly Random rnd = new Random();
        readonly List<Star> stars = new List<Star>();
        readonly List<Ring> rings = new List<Ring>();
        readonly List<Spark> sparks = new List<Spark>();
        float cx = 480, cy = 200, warp = 1, warpTarget = 1, flash;
        double t, nextRing = 2.6;
        CanvasStrokeStyle round;
        CanvasRadialGradientBrush nebula, rose;

        public Starfield()
        {
            for (int i = 0; i < 260; i++)
            {
                var s = new Star();
                Spawn(s, true);
                stars.Add(s);
            }
        }

        float Rand() => (float)rnd.NextDouble();

        void Spawn(Star s, bool anywhere)
        {
            s.A = Rand() * (float)Math.PI * 2;
            s.D = anywhere ? Rand() : Rand() * 0.06f;
            s.V = 0.1f + Rand() * 0.38f;
            s.Z = 0.7f + Rand() * 1.8f;
            s.C = Colors[rnd.Next(Colors.Length)];
        }

        public void SetCenter(float x, float y)
        {
            lock (gate)
            {
                cx = x;
                cy = y;
            }
        }

        /// <summary>Salve : onde de choc, accélération des étoiles, éclair et 140 étincelles.</summary>
        public void Burst(float k)
        {
            lock (gate)
            {
                rings.Add(new Ring { Born = t, K = k });
                warp = 7 * k + 1;
                flash = 0.18f * k;
                for (int i = 0; i < 140 * k; i++)
                {
                    var a = Rand() * Math.PI * 2;
                    var v = 100 + Rand() * 350;
                    sparks.Add(new Spark
                    {
                        X = cx, Y = cy, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v,
                        R = 0.4f + Rand() * 1.1f, Life = 0.6f + Rand() * 0.9f, C = Colors[rnd.Next(Colors.Length)],
                    });
                }
            }
        }

        /// <summary>Plongée finale : les étoiles filent.</summary>
        public void Outro()
        {
            lock (gate)
            {
                warpTarget = 16;
                flash = 0.12f;
            }
        }

        static Color Alpha(Color c, float a) => Color.FromArgb((byte)(Math.Max(0, Math.Min(1, a)) * 255), c.R, c.G, c.B);

        public void Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            var size = sender.Size;
            float w = (float)size.Width, h = (float)size.Height;
            if (w <= 0 || h <= 0) return;
            float dt = (float)Math.Min(0.05, args.Timing.ElapsedTime.TotalSeconds);
            lock (gate)
            {
                t += dt;
                // Ressources liées à la carte graphique : recréées si elle a été réinitialisée
                if (round == null || nebula.Device != sender.Device)
                {
                    round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
                    nebula = new CanvasRadialGradientBrush(sender, new[]
                    {
                        new CanvasGradientStop { Position = 0, Color = Color.FromArgb(140, 109, 76, 245) },
                        new CanvasGradientStop { Position = 0.45f, Color = Color.FromArgb(46, 91, 92, 240) },
                        new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 0, 0, 0) },
                    });
                    rose = new CanvasRadialGradientBrush(sender, new[]
                    {
                        new CanvasGradientStop { Position = 0, Color = Color.FromArgb(31, 244, 63, 94) },
                        new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 0, 0, 0) },
                    });
                }

                // Nébuleuse qui respire
                var r = Math.Min(w, h) * 0.6f * (0.9f + 0.1f * (float)Math.Sin(t * 1.6));
                nebula.Center = new Vector2(cx, cy);
                nebula.RadiusX = nebula.RadiusY = r;
                ds.FillRectangle(0, 0, w, h, nebula);
                rose.Center = new Vector2(cx, cy);
                rose.RadiusX = rose.RadiusY = r * 0.8f;
                rose.OriginOffset = new Vector2((float)Math.Cos(t * 0.6) * r * 0.3f, (float)Math.Sin(t * 0.5) * r * 0.2f);
                ds.FillRectangle(0, 0, w, h, rose);

                // Étoiles en « vitesse lumière »
                warp += (warpTarget - warp) * Math.Min(1, dt * 3);
                var reach = Math.Max(w, h) * 0.78f;
                ds.Blend = CanvasBlend.Add;
                foreach (var s in stars)
                {
                    var prev = s.D;
                    s.D += s.V * (0.2f + s.D * 2.2f) * warp * dt;
                    if (s.D > 1.15f)
                    {
                        Spawn(s, false);
                        continue;
                    }
                    float ca = (float)Math.Cos(s.A), sa = (float)Math.Sin(s.A);
                    var tail = 1 + (warp - 1) * 0.9f;
                    float x2 = cx + ca * s.D * reach, y2 = cy + sa * s.D * reach;
                    float x1 = x2 - (x2 - (cx + ca * prev * reach)) * tail, y1 = y2 - (y2 - (cy + sa * prev * reach)) * tail;
                    var alpha = Math.Min(1, s.D * 4) * (0.4f + 0.6f * Math.Min(1, s.D));
                    ds.DrawLine(x1, y1, x2, y2, Alpha(s.C, alpha), s.Z * 0.5f * (0.6f + s.D * 1.4f), round);
                }

                // Ondes de choc
                if (t > nextRing)
                {
                    rings.Add(new Ring { Born = t, K = 0.45f });
                    nextRing = t + 2.3;
                }
                for (int i = rings.Count - 1; i >= 0; i--)
                {
                    var ring = rings[i];
                    var p = (float)((t - ring.Born) / 1.7);
                    if (p >= 1)
                    {
                        rings.RemoveAt(i);
                        continue;
                    }
                    var e = 1 - (float)Math.Pow(1 - p, 3);
                    ds.DrawCircle(cx, cy, 35 + 310 * e, Color.FromArgb((byte)((1 - p) * ring.K * 255), 0xB4, 0xA5, 0xFF), (8 * (1 - p) + 1) * 0.5f);
                }

                // Étincelles de la salve
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    var s = sparks[i];
                    s.Life -= dt;
                    if (s.Life <= 0)
                    {
                        sparks.RemoveAt(i);
                        continue;
                    }
                    s.Vx *= 0.96f;
                    s.Vy = s.Vy * 0.96f + 20 * dt;
                    s.X += s.Vx * dt;
                    s.Y += s.Vy * dt;
                    ds.FillCircle(s.X, s.Y, s.R, Alpha(s.C, s.Life * 1.6f));
                }

                // Éclair
                ds.Blend = CanvasBlend.SourceOver;
                if (flash > 0.01f)
                {
                    ds.FillRectangle(0, 0, w, h, Color.FromArgb((byte)(flash * 255), 0xB9, 0xA8, 0xFF));
                    flash *= 1 - Math.Min(1, dt * 4);
                }
                if (warpTarget == 1) warp += (1 - warp) * Math.Min(1, dt * 2.5f);
            }
        }
    }
}
