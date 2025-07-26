using Adamantium.Fonts;

namespace Adamantium.UI.Controls.Text;

public class FontFamily
{
    public FontFamily(string fontName)
    {
        Typeface = TypefaceStore.GetTypeface(fontName, true);
    }

    public FontFamily(Uri fontPath)
    {
        Typeface = TypefaceStore.GetTypeface(fontPath.OriginalString);
    }
    
    public Typeface Typeface { get; }

    public IReadOnlyList<IFont> Fonts => Typeface.Fonts;

    public double BaseLine => Typeface.GetFont(0).Baseline;

    public double LineGap => Typeface.GetFont(0).LineGap;
}