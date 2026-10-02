using System;
using System.Linq;
using System.Threading;
using Adamantium.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class GlyphOutlineTests
{
    private const int Threads = 8;

    [TestCase("TTFFonts/SourceSans3-Regular.ttf")]
    [TestCase("OTFFonts/SourceSans3-Regular.otf")]
    [TestCase("OTFFonts/CFF2/AdobeVFPrototype.otf")]
    [TestCase("WoffFonts/Sarabun-Regular.woff")]
    [TestCase("WoffFonts/Sarabun-Regular.woff2")]
    [TestCase("OTFFonts/Quicksand-Bold.otf")]
    [TestCase("source-sans-3v028R/VAR/SourceSans3VF-Roman.otf")]
    public void EveryGlyphBuildsOutlinesInsideItsBounds(string path)
    {
        var typeface = Typeface.LoadFont(path, 3);

        foreach (var glyph in typeface.Glyphs)
        {
            if (glyph.IsInvalid)
            {
                Assert.Fail($"glyph {glyph.Index} could not be read");
            }

            var bounds = glyph.BoundingRectangle;
            var minX = Math.Min(bounds.Left, bounds.Right) - 1;
            var maxX = Math.Max(bounds.Left, bounds.Right) + 1;
            var minY = Math.Min(bounds.Top, bounds.Bottom) - 1;
            var maxY = Math.Max(bounds.Top, bounds.Bottom) + 1;
            foreach (var point in glyph.TransformBasicOutlines(Matrix3x2.Identity).SelectMany(x => x.Points))
            {
                if (!point.IsControl && (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY))
                {
                    Assert.Fail($"glyph {glyph.Index} has a point at {point.X},{point.Y} outside its bounds " +
                                $"{bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}");
                }
            }
        }
    }

    [TestCase("TTFFonts/SourceSans3-Regular.ttf")]
    [TestCase("WoffFonts/Sarabun-Regular.woff2")]
    public void CompositeGlyphHoldsEveryOutlineOfItsComponents(string path)
    {
        var typeface = Typeface.LoadFont(path, 3);
        var glyphs = typeface.Glyphs.ToArray();

        foreach (var glyph in glyphs.Where(x => x.IsComposite))
        {
            var expected = glyph.CompositeGlyphComponents
                .Sum(x => glyphs[x.SimpleGlyphIndex].TransformBasicOutlines(Matrix3x2.Identity).Count);
            var actual = glyph.TransformBasicOutlines(Matrix3x2.Identity).Count;
            if (actual != expected)
            {
                Assert.Fail($"composite glyph {glyph.Index} ({glyph.Name}) has {actual} outlines, its components {expected}");
            }
        }
    }

    [TestCase("WoffFonts/Sarabun-Regular.woff", "WoffFonts/Sarabun-Regular.woff2")]
    [TestCase("TTFFonts/SourceSans3-Regular.ttf", "source-sans-3v028R/WOFF2/TTF/SourceSans3-Regular.ttf.woff2")]
    public void Woff2DecodesToTheSameGlyphsAsTheFontItPacks(string original, string packed)
    {
        var expectedGlyphs = Typeface.LoadFont(original, 3).Glyphs.ToArray();
        var woff2 = Typeface.LoadFont(packed, 3).Glyphs.ToArray();

        Assert.That(woff2, Has.Length.EqualTo(expectedGlyphs.Length));
        for (var i = 0; i < expectedGlyphs.Length; i++)
        {
            var expected = $"{expectedGlyphs[i].BoundingRectangle}, {Describe(expectedGlyphs[i])}, " +
                           $"instructions {Convert.ToHexString(expectedGlyphs[i].Instructions ?? [])}";
            var actual = $"{woff2[i].BoundingRectangle}, {Describe(woff2[i])}, " +
                         $"instructions {Convert.ToHexString(woff2[i].Instructions ?? [])}";
            if (actual != expected)
            {
                Assert.Fail($"glyph {expectedGlyphs[i].Index} ({expectedGlyphs[i].Name}) decodes from WOFF2 as {actual}, " +
                            $"from {original} as {expected}");
            }
        }
    }

    [Test]
    public void OutlinesAskedFromManyThreadsAreBuiltOnce()
    {
        const string path = "OTFFonts/SourceSans3-Regular.otf";
        var expected = Typeface.LoadFont(path, 3).Glyphs.Select(Describe).ToArray();
        var glyphs = Typeface.LoadFont(path, 3).Glyphs.ToArray();

        using var start = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads).Select(t => new Thread(() =>
        {
            start.SignalAndWait();
            foreach (var glyph in glyphs)
            {
                _ = glyph.HasOutlines;
            }
        })).ToList();
        workers.ForEach(x => x.Start());
        workers.ForEach(x => x.Join());

        for (var i = 0; i < glyphs.Length; i++)
        {
            var actual = Describe(glyphs[i]);
            if (actual != expected[i])
            {
                Assert.Fail($"glyph {glyphs[i].Index} was built as {actual}, alone it is {expected[i]}");
            }
        }
    }

    [Test]
    public void GlyphSampledFromManyThreadsAtOnceIsSampledOnce()
    {
        const string path = "OTFFonts/SourceSans3-Regular.otf";
        const int sampledGlyphs = 40;
        var expected = Typeface.LoadFont(path, 3).Glyphs.Take(sampledGlyphs).Select(x => x.Sample(3).Length).ToArray();
        var glyphs = Typeface.LoadFont(path, 3).Glyphs.Take(sampledGlyphs).ToArray();
        Exception failure = null;

        using var start = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads).Select(t => new Thread(() =>
        {
            start.SignalAndWait();
            foreach (var glyph in glyphs)
            {
                try
                {
                    glyph.Sample(3);
                }
                catch (Exception e)
                {
                    Interlocked.CompareExchange(ref failure, e, null);
                }
            }
        })).ToList();
        workers.ForEach(x => x.Start());
        workers.ForEach(x => x.Join());

        Assert.That(failure, Is.Null);
        Assert.That(glyphs.Select(x => x.Sample(3).Length), Is.EqualTo(expected));
    }

    private static string Describe(Glyph glyph)
    {
        var outlines = glyph.TransformBasicOutlines(Matrix3x2.Identity);
        return $"{outlines.Count} outlines, {outlines.Sum(x => x.Points.Count)} points";
    }
}
