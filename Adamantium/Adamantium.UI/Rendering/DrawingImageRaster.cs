using System;
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
    private static readonly Dictionary<(DrawingImage Image, int Width, int Height), BitmapSource> _baked = new();
    private static readonly HashSet<(DrawingImage Image, int Width, int Height)> _pending = [];

    private static readonly HashSet<DrawingImage> _watched = [];

    private static IVisualRenderer _renderer;

    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();

    /// <summary>The bake for this drawing at this size, or null while there is none. Render-thread safe: it only reads.
    /// A miss queues the bake (see <see cref="Request"/>) - it never blocks the frame waiting for one.</summary>
    public static BitmapSource Get(DrawingImage image, Size size)
    {
        var key = KeyOf(image, size);
        if (key.Width <= 0 || key.Height <= 0) return null;

        return _baked.TryGetValue(key, out var baked) ? baked : null;
    }

    /// <summary>Queue a bake for a size that has none yet, and repaint whoever asked once it exists. Cheap to call every
    /// frame: a size already baked or already queued does nothing.</summary>
    public static void Request(DrawingImage image, Size size, IUIComponent owner)
    {
        var key = KeyOf(image, size);
        if (key.Width <= 0 || key.Height <= 0) return;
        if (_baked.ContainsKey(key) || !_pending.Add(key)) return;

        var dispatcher = UIAppContext.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _pending.Remove(key);
            return;
        }

        dispatcher.Post(() => Bake(key, size, owner));
    }

    /// <summary>Throw away everything baked from this drawing - its picture changed, so every size of it is now wrong.</summary>
    public static void Invalidate(DrawingImage image)
    {
        List<(DrawingImage Image, int Width, int Height)> stale = [];
        foreach (var key in _baked.Keys)
        {
            if (ReferenceEquals(key.Image, image)) stale.Add(key);
        }

        foreach (var key in stale)
        {
            _baked[key]?.Dispose();
            _baked.Remove(key);
        }
    }

    private static (DrawingImage Image, int Width, int Height) KeyOf(DrawingImage image, Size size) =>
        (image, (int)System.Math.Round(size.Width), (int)System.Math.Round(size.Height));

    // LOOP thread. The drawing is baked THROUGH the vector path rather than through a second renderer: an Image showing
    // it draws exactly what the on-screen one draws, so the raster can never disagree with the vector.
    private static void Bake((DrawingImage Image, int Width, int Height) key, Size size, IUIComponent owner)
    {
        _pending.Remove(key);

        var renderer = Renderer;
        if (renderer == null) return;

        var host = new Image
        {
            Source = key.Image,
            Width = size.Width,
            Height = size.Height,
            Stretch = Stretch.Fill,
            // The OWNER's data. A drawing resource binds against whoever shows it, and a bake host shown by nobody has
            // no DataContext at all - so an icon whose brushes bind to a view model baked out completely blank.
            DataContext = owner?.DataContext
        };

        if (renderer.Render(host, size, 1.0, Colors.Transparent) is not BitmapSource baked) return;

        // Hook the drawing ONCE, here rather than at the ask: a bake goes stale the moment the picture changes, and a
        // brush - unlike an element - has nobody watching the drawing on its behalf.
        if (_watched.Add(key.Image))
        {
            key.Image.Changed += (sender, _) => Invalidate((DrawingImage)sender);
        }

        _baked[key] = baked;
        // RE-RECORD, not a paint re-bake: the texture is asked for while the unit is being routed into a batch, and a
        // paint invalidation only re-bakes what was already recorded - it never asks again, so the bake would sit in the
        // cache unused and the fill stay empty for ever.
        owner?.InvalidateRender(false);
    }
}
