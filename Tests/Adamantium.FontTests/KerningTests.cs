using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class KerningTests
{
    [TestCase("OTFFonts/SourceSans3-Regular.otf", "yT", -20)]
    [TestCase("OTFFonts/SourceSans3-Regular.otf", "x,", 7)]
    [TestCase("OTFFonts/Crimson-Italic.otf", "AC", -24)]
    [TestCase("TTFFonts/PlayfairDisplay-Regular.ttf", "TA", -96)]
    [TestCase("TTFFonts/Sarabun-Regular.ttf", "AC", -30)]
    [TestCase("TTFFonts/SourceSans3-It.ttf", ".j", 20)]
    public void KerningMatchesTheReferenceShaper(string path, string pair, int expected)
    {
        var typeface = Typeface.LoadFont(path, 3);
        var font = typeface.GetFont(0);
        var container = new GlyphLayoutContainer(typeface, font);
        var glyphs = font.TranslateIntoGlyphs(pair);
        container.SetText(pair);

        var kerning = font.FeatureService.ApplyFeature(Features.kern, container, 0, (uint)glyphs.Count)
            ? container.GetAdvance(0).X
            : font.GetKerningValue((ushort)glyphs[0].Index, (ushort)glyphs[1].Index);

        Assert.That(kerning, Is.EqualTo(expected));
    }
}
