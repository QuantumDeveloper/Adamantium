using System;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering;

/// <summary>
/// The MICA source: the desktop picture behind the window, prepared once and kept.
///
/// <para>The counterpart to <see cref="BackdropCapture"/>, and deliberately its opposite in every way that matters.
/// Acrylic copies the live frame under an element, so it pays a blit per frame and follows whatever moves underneath.
/// Mica shows the WALLPAPER, which is a file: it never changes on its own, so it is decoded, shrunk and blurred ONCE,
/// and after that a material costs nothing but sampling a texture. That is why mica can sit behind a whole window
/// while acrylic is reserved for panes.</para>
///
/// <para>Shrunk hard on purpose. A wallpaper is 4K and a material shows it blurred past recognition - keeping the
/// picture at full size would cost tens of megabytes to say something a thumbnail already says. The scale here is the
/// blur: averaging into a small image IS a box filter, and the sampler's own filtering finishes the job.</para>
/// </summary>
internal sealed class WallpaperBackdrop : IDisposable
{
    // Long edge of the prepared copy. Small enough to be nearly free, large enough that a slow gradient across a
    // 4K monitor still reads as a gradient rather than as bands.
    private const int PreparedLongEdge = 160;

    private WallpaperInfo _prepared = WallpaperInfo.None;
    private BitmapSource _image;

    /// <summary>The blurred picture, or null when the desktop has none (a plain-colour desktop, or a platform that does
    /// not answer). A caller with no image tints <see cref="Background"/> instead - see the note on
    /// <see cref="DesktopWallpaper"/> about the fallback being visible rather than silent.</summary>
    public BitmapSource Image => _image;

    /// <summary>The colour behind the picture, and the whole answer when there is no picture.</summary>
    public Color Background => _prepared.Background;

    /// <summary>The monitor this copy was prepared for, in DESKTOP pixels. A material maps its fragments through it -
    /// which is what makes the picture stay still while the window moves across it.</summary>
    public Rect MonitorBounds => _prepared.MonitorBounds;

    /// <summary>Whether anything is prepared at all.</summary>
    public bool IsReady => _prepared.IsKnown;

    /// <summary>Make sure the copy matches what the desktop shows on the monitor under <paramref name="point"/>.
    /// Returns true when something usable is ready.
    ///
    /// <para>Cheap to call often: asking the platform is a COM call returning a path and a timestamp, and the answer is
    /// compared as a whole - it is a record. Everything expensive happens only when that comparison differs, which is
    /// when the user changed the wallpaper, the slideshow turned the page, or the window moved to another screen.
    /// The timestamp is what catches Spotlight, which rewrites the same path with a new picture.</para></summary>
    public bool Ensure(PixelPoint point)
    {
        var current = DesktopWallpaper.Current(point);
        if (current == _prepared) return IsReady;

        _prepared = current;
        _image = current.File != null ? Prepare(current.File.LocalPath) : null;
        return IsReady;
    }

    /// <summary>Decode the wallpaper, shrink it to a thumbnail (which IS the blur) and hand it over as a bitmap the
    /// renderer can turn into a texture. Returns null when the file cannot be read - a wallpaper the shell names but
    /// we cannot open is the same case as no wallpaper at all.</summary>
    private static BitmapSource Prepare(string path)
    {
        try
        {
            // Qualified: this class has an Image PROPERTY, which would otherwise win over the decoder's type name.
            using var source = Imaging.Image.Load(path);
            var buffer = source?.GetPixelBuffer(0, 0);
            if (buffer == null || buffer.Width == 0 || buffer.Height == 0) return null;

            var pixels = buffer.GetPixels<byte>();
            var stride = buffer.RowStride;
            var bytesPerPixel = stride / (int)buffer.Width;
            if (bytesPerPixel < 3) return null;

            var scale = Math.Max(buffer.Width, buffer.Height) / (double)PreparedLongEdge;
            var width = (uint)Math.Max(1, buffer.Width / scale);
            var height = (uint)Math.Max(1, buffer.Height / scale);

            return new BitmapSource(width, height, 1, 1, SurfaceFormat.R8G8B8A8.UNorm,
                Shrink(pixels, buffer.Width, buffer.Height, stride, bytesPerPixel, width, height,
                    IsBgr(buffer.Format)));
        }
        catch (Exception)
        {
            // A wallpaper we cannot decode is not an error to report anywhere: the desktop still has one, we just draw
            // the tint instead. Formats the shell accepts and our decoders do not (HEIC, an exotic TIFF) land here.
            return null;
        }
    }

    /// <summary>Box-average the source into the small copy. Every source pixel contributes, which is what makes this a
    /// blur and not a subsample - dropping pixels instead would leave the picture sharp and aliased, and a material
    /// built on it would shimmer as the window moves.</summary>
    private static byte[] Shrink(byte[] src, uint srcWidth, uint srcHeight, int stride, int bytesPerPixel,
        uint width, uint height, bool bgr)
    {
        var dst = new byte[width * height * 4];

        for (uint y = 0; y < height; y++)
        {
            var y0 = (uint)((long)y * srcHeight / height);
            var y1 = (uint)Math.Max(y0 + 1, (long)(y + 1) * srcHeight / height);

            for (uint x = 0; x < width; x++)
            {
                var x0 = (uint)((long)x * srcWidth / width);
                var x1 = (uint)Math.Max(x0 + 1, (long)(x + 1) * srcWidth / width);

                long r = 0, g = 0, b = 0, n = 0;
                for (var sy = y0; sy < y1 && sy < srcHeight; sy++)
                {
                    var row = (long)sy * stride;
                    for (var sx = x0; sx < x1 && sx < srcWidth; sx++)
                    {
                        var i = row + (long)sx * bytesPerPixel;
                        if (i + 2 >= src.Length) continue;
                        r += src[i + (bgr ? 2 : 0)];
                        g += src[i + 1];
                        b += src[i + (bgr ? 0 : 2)];
                        n++;
                    }
                }

                if (n == 0) n = 1;
                var o = (y * width + x) * 4;
                dst[o] = (byte)(r / n);
                dst[o + 1] = (byte)(g / n);
                dst[o + 2] = (byte)(b / n);
                dst[o + 3] = 255;
            }
        }

        return dst;
    }

    private static bool IsBgr(Vulkan.Core.Format format)
        => format == Vulkan.Core.Format.B8G8R8A8_UNORM
           || format == Vulkan.Core.Format.B8G8R8A8_SRGB
           || format == Vulkan.Core.Format.B8G8R8_UNORM;

    public void Dispose()
    {
        _image = null;
        _prepared = WallpaperInfo.None;
    }
}
