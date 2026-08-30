using System;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.Vulkan.Core;

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
    // Long edge of the prepared copy. Small on purpose, and the blur it produces is LOad-BEARING.
    //
    // Mica maps the wallpaper through the window's place on the desktop, so it must appear to stand still while the
    // window moves over it. It cannot, quite: the frame is built for where the window is at that instant and reaches the
    // screen after the OS has already moved it on, and no amount of asking the OS more often closes that gap - the
    // system's own mica does not have it only because the compositor moves the window and paints its backdrop in one
    // step. What is left is a sub-pixel wobble.
    //
    // Raising this to 480 was tried and made the wobble THREE TIMES more visible - not worse, just no longer hidden:
    // a sharper picture shows the same displacement better. At 160 the copy is soft enough that the eye has nothing to
    // track. So the blur is not only how mica looks, it is also what makes it sit still.
    private const int PreparedLongEdge = 160;

    // How long a quiet desktop is trusted before the shell is asked again. Only a safety net: a change normally arrives
    // as an announcement, and this catches the mechanisms that do not send one (see DesktopWallpaper.Changed).
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(2);

    private WallpaperInfo _prepared = WallpaperInfo.None;
    private byte[] _pixels;
    private uint _width, _height;
    private Size _picture;
    private ITexture _texture;
    private IGraphicsDevice _device;   // remembered only to retire the old texture when the wallpaper changes
    private bool _loading;
    private bool _announced = true;    // start "announced" so the first frame asks
    private readonly System.Diagnostics.Stopwatch _asked = new();
    private readonly object _gate = new();

    public WallpaperBackdrop() => DesktopWallpaper.Changed += OnDesktopChanged;

    // The announcement does not do the work - it only lifts the interval, so the very next draw asks the shell. Doing
    // the reload here would run it on whatever thread the OS message arrived on.
    private void OnDesktopChanged() => _announced = true;

    /// <summary>The blurred picture as a GPU texture, re-reading the desktop first if it changed. Null when there is
    /// nothing to show - a plain-colour desktop, an undecodable file, or a platform that does not answer - and a caller
    /// then tints <see cref="Background"/>, which is a visible fallback rather than a silently disabled material.
    ///
    /// <para>Asked per draw and nearly free: the platform call returns a path and a timestamp, and everything past the
    /// comparison happens only when the desktop actually changed.</para></summary>
    public ITexture Texture(IGraphicsDevice device, PixelPoint point)
    {
        _device = device;
        if (!Ensure(point)) return null;
        if (_texture != null) return _texture;

        byte[] pixels;
        uint width, height;
        lock (_gate)
        {
            pixels = _pixels;
            width = _width;
            height = _height;
        }

        if (pixels == null) return null;   // still decoding, or nothing to decode - the caller tints instead

        _texture = device.CreateTexture(new TextureDescription
        {
            Width = _width,
            Height = _height,
            Depth = 1,
            ArrayLayers = 1,
            MipLevels = 1,
            Samples = MSAALevel.None,
            Format = Vulkan.Core.Format.R8G8B8A8_UNORM,
            InitialLayout = ImageLayout.Undefined,
            DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageType = ImageType._2d,
            ImageAspect = ImageAspectFlagBits.ColorBit,
            ImageTiling = Vulkan.Core.ImageTiling.Optimal,
            Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit,
            Dimension = TextureDimension.Texture2D
        }, _pixels);

        return _texture;
    }

    /// <summary>The colour behind the picture, and the whole answer when there is no picture.</summary>
    public Color Background => _prepared.Background;

    /// <summary>The monitor this copy was prepared for, in DESKTOP pixels. A material maps its fragments through it -
    /// which is what makes the picture stay still while the window moves across it.</summary>
    public Rect MonitorBounds => _prepared.MonitorBounds;

    /// <summary>Whether anything is prepared at all.</summary>
    public bool IsReady => _prepared.IsKnown;

    /// <summary>Whether the desktop REPEATS the picture rather than placing one copy - the one layout that needs a
    /// repeating sampler, and the reason <see cref="Placement"/> returns a single tile for it.</summary>
    public bool Tiles => _prepared.Fit == WallpaperFit.Tile;

    /// <summary>Where the WHOLE picture lands on the desktop, in DESKTOP pixels - the rectangle the desktop itself
    /// stretched it into. A material maps its fragments through this, which is what keeps the picture still while the
    /// window travels across it: the rectangle is stated on the desktop, not on the window.
    ///
    /// <para>For <see cref="WallpaperFit.Tile"/> it is ONE tile at its natural size, anchored to the monitor's corner -
    /// the repetition is the sampler's job.</para>
    ///
    /// <para><paramref name="virtualScreen"/> matters only for <see cref="WallpaperFit.Span"/>, where one picture is
    /// stretched across every monitor rather than placed on each. Empty (a platform that does not report it) falls back
    /// to this monitor, which is what Span degrades into on a single-screen desktop anyway.</para></summary>
    public Rect Placement(Rect virtualScreen)
    {
        var monitor = _prepared.MonitorBounds;
        if (_picture.Width <= 0 || _picture.Height <= 0) return monitor;

        switch (_prepared.Fit)
        {
            case WallpaperFit.Stretch:
                return monitor;

            case WallpaperFit.Span:
                return virtualScreen.Width > 0 && virtualScreen.Height > 0 ? virtualScreen : monitor;

            case WallpaperFit.Tile:
                return new Rect(monitor.X, monitor.Y, _picture.Width, _picture.Height);

            case WallpaperFit.Center:
                return Centred(monitor, _picture.Width, _picture.Height);

            case WallpaperFit.Fit:
            {
                var scale = Math.Min(monitor.Width / _picture.Width, monitor.Height / _picture.Height);
                return Centred(monitor, _picture.Width * scale, _picture.Height * scale);
            }

            default:
            {
                // Fill: cover the monitor and let the overflow hang off the edges - which is why the rectangle returned
                // here is LARGER than the monitor, and why a material must map through it rather than through the screen.
                var scale = Math.Max(monitor.Width / _picture.Width, monitor.Height / _picture.Height);
                return Centred(monitor, _picture.Width * scale, _picture.Height * scale);
            }
        }
    }

    private static Rect Centred(Rect monitor, double width, double height)
        => new(monitor.X + (monitor.Width - width) / 2, monitor.Y + (monitor.Height - height) / 2, width, height);

    /// <summary>Make sure the copy matches what the desktop shows on the monitor under <paramref name="point"/>.
    /// Returns true when something usable is ready.
    ///
    /// <para>Cheap to call often: asking the platform is a COM call returning a path and a timestamp, and the answer is
    /// compared as a whole - it is a record. Everything expensive happens only when that comparison differs, which is
    /// when the user changed the wallpaper, the slideshow turned the page, or the window moved to another screen.
    /// The timestamp is what catches Spotlight, which rewrites the same path with a new picture.</para></summary>
    public bool Ensure(PixelPoint point)
    {
        // ASKED RARELY, on purpose. The wallpaper service is an OUT-OF-PROCESS COM server, so every question is a
        // marshalled round trip to another process - and this is called per draw. Asking it at frame rate was thousands
        // of cross-process calls a second.
        //
        // So the shell is only asked when something could have changed: the announcement fired, the window moved to
        // another monitor, or enough time passed that a slideshow could have turned the page without announcing it.
        var movedOff = !_prepared.IsKnown
                       || point.X < _prepared.MonitorBounds.X
                       || point.X >= _prepared.MonitorBounds.Right
                       || point.Y < _prepared.MonitorBounds.Y
                       || point.Y >= _prepared.MonitorBounds.Bottom;

        if (!movedOff && !_announced && _asked.IsRunning && _asked.Elapsed < RecheckInterval) return IsReady;

        _announced = false;
        _asked.Restart();

        var current = DesktopWallpaper.Current(point);
        if (current == _prepared) return IsReady;

        // SAME PICTURE, different monitor - the usual case, because most desktops show one wallpaper everywhere. Only
        // the placement changes then, and that is computed from the answer rather than stored: keep the decoded pixels
        // and the texture, and a window dragged between screens costs nothing at all.
        var samePicture = _pixels != null
                          && current.File != null
                          && _prepared.File != null
                          && string.Equals(current.File.LocalPath, _prepared.File.LocalPath, StringComparison.OrdinalIgnoreCase)
                          && current.Revision == _prepared.Revision;

        _prepared = current;
        if (samePicture) return IsReady;

        // The old texture may still be read by frames in flight, so it goes to the deferred queue rather than to
        // Dispose - the same rule the capture ring follows, and for the same reason.
        if (_texture is IDisposable old && _device != null) _device.AddToDeferDisposeQueue(old);
        _texture = null;
        _pixels = null;
        _picture = default;

        // DECODED OFF THE RENDER THREAD. A wallpaper is a 4K photograph, and averaging eight million pixels into a
        // thumbnail is tens of milliseconds at best - done inline it froze the window for SECONDS every time it was
        // dragged to another monitor, because that is exactly when the picture changes.
        //
        // Until it arrives the material has no texture and tints the desktop's background colour instead, which is the
        // same visible fallback as a desktop with no picture at all. One load at a time: a drag across three monitors
        // must not leave three decodes racing to publish.
        if (current.File != null && !_loading)
        {
            _loading = true;
            var path = current.File.LocalPath;
            System.Threading.Tasks.Task.Run(() =>
            {
                var pixels = Prepare(path, out var picture, out var width, out var height);
                lock (_gate)
                {
                    // Published only if the desktop still shows what was asked for - a second move while this decoded
                    // makes it stale, and the newer request is already on its way.
                    if (_prepared.File?.LocalPath == path)
                    {
                        _pixels = pixels;
                        _picture = picture;
                        _width = width;
                        _height = height;
                    }

                    _loading = false;
                }
            });
        }

        return IsReady;
    }

    /// <summary>Decode the wallpaper, shrink it to a thumbnail (which IS the blur) and hand it over as a bitmap the
    /// renderer can turn into a texture. Returns null when the file cannot be read - a wallpaper the shell names but
    /// we cannot open is the same case as no wallpaper at all.</summary>
    private static byte[] Prepare(string path, out Size picture, out uint width, out uint height)
    {
        picture = default;
        width = height = 0;

        try
        {
            // THE DECODE IS THE COST, and it is enormous: a 4K photograph measured 6.4 SECONDS through the engine's own
            // JPEG decoder, against 5 ms for everything this class then does with the pixels. It has no scaled-decode
            // path to ask for a thumbnail instead, so the answer is to pay it once and keep the result - the same trade
            // the shader binary cache makes, for the same reason.
            var cacheFile = CacheFileFor(path);
            if (TryLoadCached(cacheFile, out var cached, out picture, out width, out height)) return cached;

            var source = LoadSmallestUsable(path, out var downscale);
            if (source == null || source.Width == 0 || source.Height == 0) return null;

            var pixels = source.GetRawPixels(0);
            var description = source.GetImageDescription();
            var srcWidth = source.Width;
            var srcHeight = source.Height;

            var bytesPerPixel = (int)(pixels.Length / (srcWidth * srcHeight));
            if (bytesPerPixel < 3) return null;
            var stride = (int)srcWidth * bytesPerPixel;

            // The size the DESKTOP laid the picture out at, which is what decides where it lands (see Placement) - and
            // that is the ORIGINAL, even when what came back was an eighth of it. Scaled back up here, once.
            picture = new Size(srcWidth * downscale, srcHeight * downscale);

            var scale = Math.Max(srcWidth, srcHeight) / (double)PreparedLongEdge;
            width = (uint)Math.Max(1, srcWidth / scale);
            height = (uint)Math.Max(1, srcHeight / scale);

            var result = Shrink(pixels, srcWidth, srcHeight, stride, bytesPerPixel, width, height,
                IsBgr(description.Format));

            SaveCached(cacheFile, result, picture, width, height);
            return result;
        }
        catch (Exception)
        {
            // A wallpaper we cannot decode is not an error to report anywhere: the desktop still has one, we just draw
            // the tint instead. Formats the shell accepts and our decoders do not (HEIC, an exotic TIFF) land here.
            return null;
        }
    }

    // The prepared copy on disk: "<hash>.wallbin" under %LOCALAPPDATA%/Adamantium/WallpaperCache. The hash covers the
    // path, the file's write time and the target size, so a new wallpaper - or a Spotlight rewrite of the same path -
    // lands on a different file instead of loading a thumbnail of the previous picture.
    private static string CacheFileFor(string path)
    {
        var stamp = System.IO.File.GetLastWriteTimeUtc(path).Ticks;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var key = System.Text.Encoding.UTF8.GetBytes($"{path.ToLowerInvariant()}|{stamp}|{PreparedLongEdge}");
        var hash = Convert.ToHexString(sha.ComputeHash(key)).Substring(0, 16);

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(root, "Adamantium", "WallpaperCache", $"{hash}.wallbin");
    }

    // Layout: thumbnail width, height, then the ORIGINAL picture's size (which decides placement), then RGBA bytes.
    private static bool TryLoadCached(string file, out byte[] pixels, out Size picture, out uint width, out uint height)
    {
        pixels = null;
        picture = default;
        width = height = 0;

        try
        {
            if (!System.IO.File.Exists(file)) return false;

            using var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(file));
            width = reader.ReadUInt32();
            height = reader.ReadUInt32();
            picture = new Size(reader.ReadUInt32(), reader.ReadUInt32());
            var expected = (int)(width * height * 4);
            if (width == 0 || height == 0 || expected <= 0) return false;

            pixels = reader.ReadBytes(expected);
            return pixels.Length == expected;
        }
        catch (Exception)
        {
            // A truncated or unreadable cache file is not worth reporting - it just means decoding once more.
            pixels = null;
            return false;
        }
    }

    private static void SaveCached(string file, byte[] pixels, Size picture, uint width, uint height)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            using var writer = new System.IO.BinaryWriter(System.IO.File.Create(file));
            writer.Write(width);
            writer.Write(height);
            writer.Write((uint)picture.Width);
            writer.Write((uint)picture.Height);
            writer.Write(pixels);
        }
        catch (Exception)
        {
            // Failing to cache costs a decode next time and nothing else.
        }
    }

    /// <summary>
    /// The wallpaper at the smallest size that still answers the question - an eighth-scale preview where the JPEG
    /// decoder can produce one, the whole picture otherwise.
    ///
    /// <para>This material shrinks the picture to a thumbnail and blurs it anyway, so decoding four thousand pixels
    /// across to throw away all but a hundred and sixty is work nobody sees. A JPEG carries each block's average as one
    /// coefficient, and reading only those gives the picture at 1/8 directly - which is still far more than needed.</para>
    ///
    /// <para>PNG and everything else fall through to the full decode: only JPEG stores the picture in a form that can
    /// be read at reduced scale.</para>
    /// </summary>
    private static IRawBitmap LoadSmallestUsable(string path, out int downscale)
    {
        downscale = 1;
        var extension = System.IO.Path.GetExtension(path);
        var isJpeg = string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);

        if (isJpeg)
        {
            try
            {
                using var stream = System.IO.File.OpenRead(path);
                var preview = new Imaging.Jpeg.Decoder.JpegDecoder(stream).DecodePreview();
                if (preview != null)
                {
                    downscale = 8;
                    return preview;
                }
            }
            catch (Imaging.Jpeg.Decoder.PreviewNotAvailableException)
            {
                // This picture's blocks do not divide its size; decode it in full below.
            }
            catch (Exception)
            {
                // Any other trouble reading it as a preview is answered the same way - by reading it properly.
            }
        }

        using var full = System.IO.File.OpenRead(path);
        return BitmapLoader.Load(full);
    }

    /// <summary>Average the source into the small copy. Averaged, not sampled: dropping pixels would leave the picture
    /// sharp and aliased, and a material built on it would shimmer as the window moves.
    ///
    /// <para>The averaging STRIDES rather than touching every pixel. A 4K photograph is eight million of them and this
    /// runs on a background thread while the material shows its tint - but "eventually" is still a wait, and averaging
    /// sixteen samples of a cell instead of a thousand is indistinguishable once the result is 160 px wide. Enough
    /// samples that no cell is decided by a single pixel; few enough that the work is bounded by the OUTPUT size.</para></summary>
    private static byte[] Shrink(byte[] src, uint srcWidth, uint srcHeight, int stride, int bytesPerPixel,
        uint width, uint height, bool bgr)
    {
        var dst = new byte[width * height * 4];

        // At most this many samples per axis inside one output cell - so a cell costs at most 16 reads no matter how
        // large the picture is, and the whole shrink is O(output), not O(input).
        const int perAxis = 4;

        for (uint y = 0; y < height; y++)
        {
            var y0 = (uint)((long)y * srcHeight / height);
            var y1 = (uint)Math.Max(y0 + 1, (long)(y + 1) * srcHeight / height);
            var stepY = (uint)Math.Max(1, (y1 - y0) / perAxis);

            for (uint x = 0; x < width; x++)
            {
                var x0 = (uint)((long)x * srcWidth / width);
                var x1 = (uint)Math.Max(x0 + 1, (long)(x + 1) * srcWidth / width);
                var stepX = (uint)Math.Max(1, (x1 - x0) / perAxis);

                long r = 0, g = 0, b = 0, n = 0;
                for (var sy = y0; sy < y1 && sy < srcHeight; sy += stepY)
                {
                    var row = (long)sy * stride;
                    for (var sx = x0; sx < x1 && sx < srcWidth; sx += stepX)
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
        DesktopWallpaper.Changed -= OnDesktopChanged;
        (_texture as IDisposable)?.Dispose();
        _texture = null;
        _pixels = null;
        _prepared = WallpaperInfo.None;
    }
}
