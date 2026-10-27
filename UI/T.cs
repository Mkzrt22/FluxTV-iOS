using FluxTV.Core;
using Windows.UI.Xaml.Markup;

namespace FluxTV.UI
{
    /// <summary>
    /// Texte dans la langue de l'app, écrit dans les deux langues directement dans le XAML :
    /// <c>Text="{ui:T Fr='Réglages', En='Settings'}"</c>. Lu à la création de la page.
    /// </summary>
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class T : MarkupExtension
    {
        public string Fr { get; set; } = "";
        public string En { get; set; } = "";

        protected override object ProvideValue() => Tr.T(Fr, En);
    }
}
