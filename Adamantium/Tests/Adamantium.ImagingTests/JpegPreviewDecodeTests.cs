using System;
using System.IO;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Imaging.Jpeg.Decoder;
using NUnit.Framework;

namespace Adamantium.ImagingTests;

/// <summary>
/// The eighth-scale preview decode against the full one.
///
/// <para>It skips the inverse DCT entirely and reads each block's average instead, so the question is not whether it is
/// pixel-identical - it cannot be - but whether it is the same PICTURE: same size, same colours in the same places. A
/// preview that is subtly shifted, or that has its chroma planes misplaced under subsampling, would still look like a
/// photograph and be quietly wrong wherever it is used.</para>
/// </summary>
[TestFixture]
public class JpegPreviewDecodeTests
{
    // Smooth, with a strong diagonal: shifted output shows up as a colour error, and a plane placed wrong shows up as
    // the wrong hue rather than as noise.
    private static byte[] Picture(uint width, uint height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i] = (byte)(x * 255 / Math.Max(1, width - 1));
                pixels[i + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
                pixels[i + 2] = (byte)(255 - x * 255 / Math.Max(1, width - 1));
                pixels[i + 3] = 255;
            }
        }
        return pixels;
    }

    private static byte[] EncodeJpeg(uint width, uint height)
    {
        var image = Image.New2D(width, height, 1, SurfaceFormat.R8G8B8A8.UNorm);
        var pixels = Picture(width, height);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, image.PixelBuffer[0].DataPointer, pixels.Length);

        var stream = new MemoryStream();
        BitmapLoader.Save(image.ConvertToRawBitmap(), stream, ImageFileType.Jpg);
        return stream.ToArray();
    }

    [TestCase(64u, 64u)]
    [TestCase(128u, 96u)]
    public void PreviewIsAnEighthOfTheFullPicture(uint width, uint height)
    {
        var jpeg = EncodeJpeg(width, height);

        var preview = new JpegDecoder(new MemoryStream(jpeg)).DecodePreview();
        var full = new JpegDecoder(new MemoryStream(jpeg)).Decode();

        Assert.That(preview.Width, Is.EqualTo((width + 7) / 8), "an eighth of the width, rounded up");
        Assert.That(preview.Height, Is.EqualTo((height + 7) / 8), "an eighth of the height, rounded up");

        var previewPixels = preview.GetRawPixels(0);
        var fullPixels = full.GetRawPixels(0);
        var channels = (int)(fullPixels.Length / (full.Width * full.Height));

        // Each preview pixel against the AVERAGE of the block it stands for - which is what a DC coefficient is.
        //
        // Edge blocks are measured SEPARATELY. Where the picture does not fill a whole block, the encoder padded it
        // with samples of its own choosing, and the DC term is the average including that padding - while the full
        // decode only ever shows the part inside the picture. The two therefore disagree there by construction, and
        // holding the edge to the same standard would be testing the encoder's padding, not this decoder.
        double worst = 0;
        double worstEdge = 0;
        for (var by = 0u; by < preview.Height; by++)
        {
            for (var bx = 0u; bx < preview.Width; bx++)
            {
                for (var ch = 0; ch < 3; ch++)
                {
                    long sum = 0;
                    var count = 0;
                    for (var y = by * 8; y < Math.Min((by + 1) * 8, full.Height); y++)
                    {
                        for (var x = bx * 8; x < Math.Min((bx + 1) * 8, full.Width); x++)
                        {
                            sum += fullPixels[(y * full.Width + x) * channels + ch];
                            count++;
                        }
                    }

                    var expected = (double)sum / Math.Max(1, count);
                    var actual = previewPixels[(by * preview.Width + bx) * channels + ch];
                    var error = Math.Abs(actual - expected);

                    var partial = count < 64;   // this block runs off the edge of the picture
                    if (partial) worstEdge = Math.Max(worstEdge, error);
                    else worst = Math.Max(worst, error);
                }
            }
        }

        // The blocks fully inside the picture are what actually proves the traversal: a misplaced plane or an
        // off-by-one walk shows up here immediately, since a preview pixel would then carry a neighbour's colour.
        Assert.That(worst, Is.LessThan(40.0), $"interior block differs by {worst:F1} from its average");

        // The edge is still checked, only loosely - it must be the right REGION of the picture even if the padding
        // pulls its value about.
        Assert.That(worstEdge, Is.LessThan(90.0), $"edge block differs by {worstEdge:F1} from its visible average");
    }

    [TestCase(100u, 70u)]
    [TestCase(33u, 17u)]
    public void PreviewRefusesWhereBlocksDoNotFitWhole(uint width, uint height)
    {
        // A picture whose edge falls inside a block is REFUSED rather than previewed approximately. The edge column
        // came out carrying the wrong chroma, and a preview that is quietly wrong at the edge is worse than one that
        // says it cannot be made - the caller then decodes at full scale, which always works.
        var jpeg = EncodeJpeg(width, height);
        var decoder = new JpegDecoder(new MemoryStream(jpeg));

        Assert.Throws<PreviewNotAvailableException>(() => decoder.DecodePreview());

        // ...and the ordinary decode of that same picture is unaffected.
        var full = new JpegDecoder(new MemoryStream(jpeg)).Decode();
        Assert.That(full.Width, Is.EqualTo(width));
        Assert.That(full.Height, Is.EqualTo(height));
    }
}
