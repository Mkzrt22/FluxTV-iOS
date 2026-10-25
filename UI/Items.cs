using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluxTV.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace FluxTV.UI
{
    /// <summary>Élément affiché dont certaines valeurs changent après coup (guide TV, reprise…).</summary>
    public abstract class Bindable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Notify(name);
            return true;
        }

        protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class Palette
    {
        static Brush Get(string key) => (Brush)Application.Current.Resources[key];
        public static Brush Text => Get("TextBrush");
        public static Brush TextDim => Get("TextDimBrush");
        public static Brush Accent => Get("AccentBrush");
        public static Brush AccentBright => Get("AccentBrightBrush");
        public static Brush AccentSoft => Get("AccentSoftBrush");
        public static Brush OnAir => Get("OnAirBrush");
        public static Brush Gold => Get("GoldBrush");
        public static Brush Glass => Get("GlassBrush");
        public static Brush GlassStrong => Get("GlassStrongBrush");
    }

    public enum CardKind { Channel, Movie, Series, Resume }

    /// <summary>Carte d'une rangée ou d'une grille (affiche, chaîne, reprise).</summary>
    public sealed class CardItem : Bindable
    {
        public CardKind Kind { get; set; }
        /// <summary>LiveChannel, Movie, Series ou ContentRef selon le genre de carte.</summary>
        public object Payload { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public string Badge { get; set; }
        public string Number { get; set; }
        public bool IsLogo { get; set; }
        public bool LiveProgress { get; set; }

        string subtitle;
        public string Subtitle { get => subtitle; set => Set(ref subtitle, value); }

        double progress;
        public double Progress
        {
            get => progress;
            set { if (Set(ref progress, value)) Notify(nameof(HasProgress)); }
        }
        public bool HasProgress => progress > 0.005;

        bool watched;
        public bool Watched { get => watched; set => Set(ref watched, value); }

        public bool HasBadge => !string.IsNullOrEmpty(Badge);
        public bool HasNumber => !string.IsNullOrEmpty(Number);
        /// <summary>Logos de chaînes : image entière et centrée ; affiches : image qui remplit la carte.</summary>
        public Stretch ImageStretch => IsLogo ? Stretch.Uniform : Stretch.UniformToFill;
        public Thickness ImagePadding => IsLogo ? new Thickness(26, 18, 26, 18) : new Thickness(0);
        public Brush ProgressBrush => LiveProgress ? Palette.OnAir : Palette.Accent;
    }

    /// <summary>Catégorie (liste du direct, du catalogue) ou saison d'une série.</summary>
    public sealed class CategoryItem : Bindable
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Season { get; set; }

        string count;
        public string Count { get => count; set => Set(ref count, value); }

        bool isCurrent;
        public bool IsCurrent
        {
            get => isCurrent;
            set
            {
                if (!Set(ref isCurrent, value)) return;
                Notify(nameof(NameBrush));
                Notify(nameof(ChipBrush));
            }
        }

        public Brush NameBrush => isCurrent ? Palette.Text : Palette.TextDim;
        public Brush ChipBrush => isCurrent ? Palette.AccentSoft : Palette.Glass;
    }

    /// <summary>Chaîne de la liste du direct, avec son programme en cours.</summary>
    public sealed class ChannelItem : Bindable
    {
        public LiveChannel Channel { get; }
        public string Name => Channel.Name;
        public string Logo => Channel.Logo;
        public string Number => Channel.Num > 0 ? Channel.Num.ToString() : "";

        public ChannelItem(LiveChannel channel)
        {
            Channel = channel;
        }

        string nowTitle;
        public string NowTitle
        {
            get => nowTitle;
            set { if (Set(ref nowTitle, value)) Notify(nameof(HasNow)); }
        }
        public bool HasNow => !string.IsNullOrEmpty(nowTitle);

        double progress;
        public double Progress { get => progress; set => Set(ref progress, value); }

        bool isFavorite;
        public bool IsFavorite { get => isFavorite; set => Set(ref isFavorite, value); }
    }

    /// <summary>Épisode d'une série.</summary>
    public sealed class EpisodeItem : Bindable
    {
        public Episode Episode { get; }
        public string Label => Episode.Label;
        public string Title { get; }
        public string Plot { get; }
        public string Image { get; }
        public string Duration { get; }

        public EpisodeItem(Episode e, string fallbackImage)
        {
            Episode = e;
            Title = string.IsNullOrWhiteSpace(e.Title) ? "Épisode " + e.Number : e.Title;
            Plot = e.Plot ?? "";
            Image = e.Image ?? fallbackImage;
            Duration = e.DurationSecs is int d && d > 0 ? Format.Duration(d) : "";
        }

        double progress;
        public double Progress
        {
            get => progress;
            set { if (Set(ref progress, value)) Notify(nameof(HasProgress)); }
        }
        public bool HasProgress => progress > 0.005;

        bool watched;
        public bool Watched { get => watched; set => Set(ref watched, value); }
    }

    /// <summary>Choix d'un panneau du lecteur (piste audio, sous-titres, chaîne…).</summary>
    public sealed class OptionItem : Bindable
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public int Index { get; set; }
        public bool HasDetail => !string.IsNullOrEmpty(Detail);

        bool isCurrent;
        public bool IsCurrent { get => isCurrent; set => Set(ref isCurrent, value); }
    }
}
