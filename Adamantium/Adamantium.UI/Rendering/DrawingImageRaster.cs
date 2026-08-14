using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;

namespace Adamantium.UI.Rendering;

/// <summary>
/// The RASTER half of <see cref="DrawingImage"/>: bakes a drawing into a real picture for the consumers that cannot
/// replay it - anything sampling a texture (<c>ImageBrush</c>, <c>NineSliceBrush</c>) rather than issuing draws. The
/// vector path stays the default and this is the fallback, exactly as the tile-brush plan sets out: a bake gives up
/// resolution independence, so it happens only where nothing else will do.
/// <para>Baking cannot happen where the texture is ASKED for. That question is put on the render thread while batches
/// are being filled, and a bake is loop-thread work - it builds a visual, measures it, arranges it and runs a frame. So
/// the ask is answered from this cache, and a miss QUEUES the bake onto the loop thread and repaints when it lands.
/// That is the same shape as <c>TexturedBrushSource</c>, which already solves it for a picture still being decoded.</para>
/// </summary>
internal static class DrawingImageRaster
{
    // Keyed by the drawing AND the size it was baked at: the same icon used as a small tiled fill and as a large one
    // needs two bakes, and handing the small one to the large fill is precisely the blur the vector path exists to avoid.
    // TWO THREADS touch these: Get/Request are asked on the RENDER thread while batches are filled, Bake runs on the
    // LOOP thread. Plain collections lost and duplicated entries at random, silently.
    private static readonly ConcurrentDictionary<(DrawingImage Image, int Width, int Height, int Scale), BitmapSource> _baked = new();
    private static readonly ConcurrentDictionary<(DrawingImage Image, int Width, int Height, int Scale), byte> _pending = new();

    private static readonly HashSet<DrawingImage> _watched = [];

    private static IVisualRenderer _renderer;

    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();

    /// <summary>The bake for this drawing at this size, or null while there is none. Render-thread safe: it only reads.
    /// A miss queues the bake (see <see cref="Request"/>) - it never blocks the frame waiting for one.</summary>
    public static BitmapSource Get(DrawingImage image, Size size, IUIComponent owner)
    {
        var key = KeyOf(image, size, DeviceScaleOf(owner));
        if (key.Width <= 0 || key.Height <= 0)
        {
            return null;
        }

        if (_baked.TryGetValue(key, out var baked))
        {
            return baked;
        }

        // MISS, but this drawing may already be baked at another size. Drawing nothing until the new one lands is what
        // made a fill flicker - and vanish - while a size slider moved: every frame asked for a size that was not there
        // yet. Any bake of the SAME SHAPE shows the same picture (the uv is normalised), so it stands in meanwhile and
        // is simply replaced when the exact one arrives. A different aspect would distort, so only the shape matches.
        return NearestOfSameShape(image, key);
    }

    // The closest already-baked size for this drawing whose aspect matches - closest, so the stand-in is as near the
    // asked-for resolution as anything available.
    private static BitmapSource NearestOfSameShape(DrawingImage image, (DrawingImage Image, int Width, int Height, int Scale) key)
    {
        BitmapSource best = null;
        var bestDistance = double.MaxValue;
        var wanted = (double)key.Width / key.Height;

        foreach (var pair in _baked)
        {
            if (!ReferenceEquals(pair.Key.Image, image) || pair.Key.Height <= 0)
            {
                continue;
            }

            var aspect = (double)pair.Key.Width / pair.Key.Height;
            if (System.Math.Abs(aspect - wanted) > 0.01)
            {
                continue;
            }

            var distance = System.Math.Abs(pair.Key.Width - key.Width);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pair.Value;
            }
        }

        return best;
    }

    /// <summary>Queue a bake for a size that has none yet, and repaint whoever asked once it exists. Cheap to call every
    /// frame: a size already baked or already queued does nothing.</summary>
    public static void Request(DrawingImage image, Size size, IUIComponent owner)
    {
        var key = KeyOf(image, size, DeviceScaleOf(owner));
        if (key.Width <= 0 || key.Height <= 0 || _baked.ContainsKey(key) || !_pending.TryAdd(key, 0))
        {
            return;
        }

        var dispatcher = UIAppContext.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _pending.TryRemove(key, out _);
            return;
        }

        // At the KEY's size, not the asked-for one: the key is snapped, and a bitmap that does not match the key it is
        // filed under is handed to every later ask for that key.
        dispatcher.Post(() => Bake(key, new Size(key.Width, key.Height), owner));
    }

    /// <summary>Throw away everything baked from this drawing - its picture changed, so every size of it is now wrong.</summary>
    public static void Invalidate(DrawingImage image)
    {
        List<(DrawingImage Image, int Width, int Height, int Scale)> stale = [];
        foreach (var key in _baked.Keys)
        {
            if (ReferenceEquals(key.Image, image))
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            if (_baked.TryRemove(key, out var baked))
            {
                baked?.Dispose();
            }
        }
    }

    // How many sizes of ONE drawing are kept. A drag still walks through keys, and each is a GPU texture, so the set is
    // bounded: the ones furthest from the size in use now go first, since that is the one being asked for.
    private const int KeptSizesPerDrawing = 6;

    private static void Evict((DrawingImage Image, int Width, int Height, int Scale) newest)
    {
        List<(DrawingImage Image, int Width, int Height, int Scale)> mine = [];
        foreach (var key in _baked.Keys)
        {
            if (ReferenceEquals(key.Image, newest.Image))
            {
                mine.Add(key);
            }
        }

        if (mine.Count <= KeptSizesPerDrawing)
        {
            return;
        }

        mine.Sort((a, b) => System.Math.Abs(b.Width - newest.Width).CompareTo(System.Math.Abs(a.Width - newest.Width)));
        for (var i = 0; i < mine.Count - KeptSizesPerDrawing; i++)
        {
            if (_baked.TryRemove(mine[i], out var stale))
            {
                stale?.Dispose();
            }
        }
    }

    // A bake per PIXEL of size is what a drag produces, and each one is a render target: the size is snapped UP to a
    // step so a slider crosses a handful of keys instead of one per frame. Both axes are scaled by the SAME factor -
    // the bake's aspect has to stay the content's, or the picture is sampled into a rect it was not drawn for.
    private const double SizeStep = 32.0;

    // The device scale is PART of the key: the same drawing at the same logical size needs a different number of pixels
    // on a 150% display than on a 100% one, and one standing in for the other is exactly the blur this was meant to
    // avoid. Quantised to a hundredth so a scale that arrives as 1.4999999 does not key a second bake.
    private static (DrawingImage Image, int Width, int Height, int Scale) KeyOf(DrawingImage image, Size size, double deviceScale)
    {
        var longest = System.Math.Max(size.Width, size.Height);
        if (longest <= 0)
        {
            return (image, 0, 0, 0);
        }

        var scale = System.Math.Ceiling(longest / SizeStep) * SizeStep / longest;
        return (image,
            (int)System.Math.Round(size.Width * scale),
            (int)System.Math.Round(size.Height * scale),
            (int)System.Math.Round(System.Math.Max(0.01, deviceScale) * 100));
    }

    /// <summary>Device pixels per logical unit for whatever window shows <paramref name="owner"/> - the scale the bake has
    /// to be drawn at to be as sharp as everything around it. 1.0 when there is no window to ask (a detached host, a
    /// headless render).</summary>
    private static double DeviceScaleOf(IUIComponent owner)
    {
        for (var node = owner; node != null; node = node.VisualParent)
        {
            if (node is IWindow { Renderer: { } renderer })
            {
                return renderer.RenderScale <= 0 ? 1.0 : renderer.RenderScale;
            }
        }

        return 1.0;
    }

    // LOOP thread. Baked THROUGH the vector path - an Image showing the drawing draws exactly what the on-screen one
    // does - and QUEUED, never rendered here: the GPU half shares one device with the render thread, so submitting it
    // from this thread interleaved with a live frame and the bake came back with one shape wearing another's colour.
    private static void Bake((DrawingImage Image, int Width, int Height, int Scale) key, Size size, IUIComponent owner)
    {
        var renderer = Renderer;
        if (renderer == null)
        {
            _pending.TryRemove(key, out _);
            return;
        }

        var host = new Image
        {
            Source = key.Image,
            Stretch = Stretch.Fill,
            Width = size.Width,
            Height = size.Height,
            // A drawing resource binds against whoever shows it; a host shown by nobody has no DataContext, so an icon
            // whose brushes bind to a view model bakes out blank.
            DataContext = owner?.DataContext
        };

        // The LOGICAL size lays the host out; the device scale decides how many PIXELS come back. Baking at 1.0 on a
        // 150% display handed the fill a texture two thirds of the resolution it is drawn at - the whole reason a vector
        // source exists is that it does not have to blur.
        renderer.RequestRender(host, size, key.Scale / 100.0, Colors.Transparent, image => Store(key, image, owner));
    }

    // UI thread, once the render thread has drawn and read the bake back.
    private static void Store((DrawingImage Image, int Width, int Height, int Scale) key, ImageSource rendered, IUIComponent owner)
    {
        _pending.TryRemove(key, out _);
        if (rendered is not BitmapSource baked)
        {
            return;
        }

        // Hook the drawing ONCE, here rather than at the ask: a bake goes stale the moment the picture changes, and a
        // brush - unlike an element - has nobody watching the drawing on its behalf.
        if (_watched.Add(key.Image))
        {
            key.Image.Changed += (sender, _) => Invalidate((DrawingImage)sender);
        }

        _baked[key] = baked;
        Evict(key);
        // RE-RECORD, not a paint re-bake: the texture is asked for while the unit is being routed into a batch, and a
        // paint invalidation only re-bakes what was already recorded - it never asks again, so the bake would sit in the
        // cache unused and the fill stay empty for ever.
        owner?.InvalidateRender(false);
    }
}
