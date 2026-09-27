using System.IO;
using Adamantium.Imaging;
using Adamantium.Imaging.Png;
using NUnit.Framework;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using static Adamantium.ImagingTests.TestPng;

namespace Adamantium.ImagingTests;

/// <summary>
/// Chunks around the image data: the background color keeps all three channels, and an unknown chunk stops the decode
/// only when it is critical.
/// </summary>
[TestFixture]
public class PngChunkTests
{
    private static byte[] Rgb2x2(params (string Type, byte[] Data)[] chunks) =>
        Build(2, 2, 8, 2, Zlib(new byte[2 * (1 + 2 * 3)], CompressionLevel.Optimal), chunks);

    [Test]
    public void RgbBackground_KeepsItsGreen()
    {
        var png = Rgb2x2(("bKGD", [0x00, 0x11, 0x00, 0x22, 0x00, 0x33]));

        var info = ((PngImage)BitmapLoader.Load(new MemoryStream(png))).State.InfoPng;

        Assert.That(info.IsBackgroundDefined, Is.True);
        Assert.That((info.BackgroundR, info.BackgroundG, info.BackgroundB), Is.EqualTo((0x11u, 0x22u, 0x33u)));
    }

    [Test]
    public void PaletteColor_ConvertsWithItsGreen()
    {
        var palette = PngColorMode.Create(PngColorType.Palette, 8);
        palette.Palette = [10, 20, 30, 255, 40, 50, 60, 255];
        palette.PaletteSize = 2;
        var rgb16 = PngColorMode.Create(PngColorType.RGB, 16);

        uint r = 0, g = 0, b = 0;
        var error = PngColorConversion.ConvertRGB(ref r, ref g, ref b, 1, 0, 0, rgb16, palette);

        Assert.That(error, Is.EqualTo(0u));
        Assert.That((r, g, b), Is.EqualTo((40u * 257, 50u * 257, 60u * 257)));
    }

    [Test]
    public void UnknownAncillaryChunk_IsSkipped()
    {
        var png = Rgb2x2(("sBIT", [8, 8, 8]));

        Assert.That(BitmapLoader.Load(new MemoryStream(png)).GetRawPixels(0), Has.Length.EqualTo(2 * 2 * 4));
    }

    [Test]
    public void UnknownCriticalChunk_StopsTheDecode()
    {
        var png = Rgb2x2(("ABCD", [1]));

        Assert.Throws<PngDecodeException>(() => BitmapLoader.Load(new MemoryStream(png)));
    }
}
