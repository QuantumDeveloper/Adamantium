using System;
using System.IO;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using NUnit.Framework;

namespace Adamantium.ImagingTests;

/// <summary>
/// What the JPEG decoder must keep producing while it is made faster.
///
/// <para>Written BEFORE optimising it, and that is the point: the decoder is a hand-tuned port with an inverse DCT in
/// its hot path, nothing covered it, and "faster" is worthless if it quietly changes pixels. A 4K wallpaper took 6.4
/// seconds through it, of which 3.9 were the IDCT alone - so it is going to be edited, and this is what says the edits
/// were harmless.</para>
///
/// <para>Lossy, so the comparison is a TOLERANCE, not equality: the encoder throws away high-frequency detail by
/// design. The pictures below are deliberately smooth for that reason - a gradient survives quantisation, a checkerboard
/// would not, and a test that fails on the format's own behaviour teaches nothing.</para>
/// </summary>
[TestFixture]
public class JpegDecodeTests
{
    private static byte[] Gradient(uint width, uint height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i] = (byte)(x * 255 / Math.Max(1, width - 1));
                pixels[i + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
                pixels[i + 2] = 128;
                pixels[i + 3] = 255;
            }
        }
        return pixels;
    }

    private static IRawBitmap DecodeRoundTrip(uint width, uint height, out byte[] expected)
    {
        expected = Gradient(width, height);

        var image = Image.New2D(width, height, 1, SurfaceFormat.R8G8B8A8.UNorm);
        System.Runtime.InteropServices.Marshal.Copy(expected, 0, image.PixelBuffer[0].DataPointer, expected.Length);

        var stream = new MemoryStream();
        BitmapLoader.Save(image.ConvertToRawBitmap(), stream, ImageFileType.Jpg);
        return BitmapLoader.Load(new MemoryStream(stream.ToArray()));
    }

    [TestCase(16u, 16u)]
    [TestCase(64u, 64u)]
    [TestCase(97u, 41u)]   // not a multiple of 8 - the blocks do not tile the picture exactly
    public void DecodesToTheSamePictureWithinTolerance(uint width, uint height)
    {
        var decoded = DecodeRoundTrip(width, height, out var expected);

        Assert.That(decoded, Is.Not.Null, "the decoder must read back what the encoder wrote");
        Assert.That(decoded.Width, Is.EqualTo(width));
        Assert.That(decoded.Height, Is.EqualTo(height));

        var actual = decoded.GetRawPixels(0);
        var channels = (int)(actual.Length / (width * height));
        Assert.That(channels, Is.GreaterThanOrEqualTo(3), "colour is expected back");

        // MEAN error, not per-pixel: JPEG's own ringing puts a few edge pixels well off, and asserting on the worst one
        // makes the test about the format rather than about the decoder. A drift in the IDCT moves the whole picture.
        double sum = 0;
        var count = 0;
        for (var i = 0; i < width * height; i++)
        {
            for (var ch = 0; ch < 3; ch++)
            {
                sum += Math.Abs(actual[i * channels + ch] - expected[i * 4 + ch]);
                count++;
            }
        }

        var mean = sum / count;
        Assert.That(mean, Is.LessThan(12.0), $"average channel error {mean:F2} - the decoder is no longer producing the picture it used to");
    }
}
