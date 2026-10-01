using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests;

// The dynamic atlas is filled from several threads: every request for new letters is rasterized and packed on a worker
// of its own. Two batches packed at once read the same cursor and landed in one cell, and a letter on screen showed
// another letter's pixels.
public class AtlasPackingConcurrencyTests
{
    private const string Letters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,;:!?()[]{}+-*/=<>&%$#@";

    [Test]
    public void BatchesPackedAtOnce_NeverShareACell()
    {
        var typeface = Typeface.LoadFont(@"OTFFonts/Crimson-Italic.otf", 3);
        var font = typeface.GetFont(0);

        for (var run = 0; run < 20; run++)
        {
            var atlasData = new FontAtlasData(32, new Size(1024, 1024), 8);
            var generator = new TextureAtlasGenerator(typeface, font, atlasData, FontParameters.Default());
            var glyphs = font.TranslateIntoGlyphs(Letters).DistinctBy(x => x.Index).Where(x => !x.IsEmpty).ToArray();

            // One glyph a batch: packing is short next to rasterizing, and small batches give it the most chances to overlap.
            Parallel.ForEach(glyphs, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                glyph => generator.GenerateTextureForGlyphs([glyph]));

            var cells = glyphs
                .Select(x => atlasData.GetGlyphData(x.Index))
                .Where(x => x != null && !x.IsEmpty)
                .ToArray();

            for (var i = 0; i < cells.Length; i++)
            {
                for (var j = i + 1; j < cells.Length; j++)
                {
                    Assert.That(Overlap(cells[i], cells[j]), Is.False,
                        $"run {run}: glyphs {cells[i].GlyphIndex} and {cells[j].GlyphIndex} were packed into one cell " +
                        $"on layer {cells[i].DepthLayer}, at {cells[i].BoundingRect.Left},{cells[i].BoundingRect.Top} and " +
                        $"{cells[j].BoundingRect.Left},{cells[j].BoundingRect.Top}");
                }
            }
        }
    }

    private static bool Overlap(GlyphTextureData a, GlyphTextureData b)
    {
        if (a.DepthLayer != b.DepthLayer)
        {
            return false;
        }

        var aw = (int)a.FullGlyphSize.Width;
        var ah = (int)a.FullGlyphSize.Height;
        var bw = (int)b.FullGlyphSize.Width;
        var bh = (int)b.FullGlyphSize.Height;
        return a.BoundingRect.Left < b.BoundingRect.Left + bw && b.BoundingRect.Left < a.BoundingRect.Left + aw
            && a.BoundingRect.Top < b.BoundingRect.Top + bh && b.BoundingRect.Top < a.BoundingRect.Top + ah;
    }
}
