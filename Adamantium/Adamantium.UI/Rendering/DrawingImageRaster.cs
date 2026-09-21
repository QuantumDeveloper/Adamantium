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
    private static readonly ConcurrentDictionary<(DrawingImage Image, int Width, int Height, int Scale, int Slice), BitmapSource> _baked = new();
    private static readonly ConcurrentDictionary<(DrawingImage Image, int Width, int Height, int Scale, int Slice), byte> _pending = new();

    /// <summary>"All of it" - the slice a caller that says nothing about a viewbox means.</summary>
    private static readonly Vector4F WholePicture = new(0, 0, 1, 1);

    private static readonly HashSet<DrawingImage> _watched = [];

    // Who asked for a bake - the elements to re-record when the palette is repainted. Concurrent: Get runs on the
    // render thread.
    private static readonly ConcurrentDictionary<IUIComponent, byte> _owners = new();

    // The palette the current bakes were drawn under.
    private static int _bakedPalette = -1;

    private static IVisualRenderer _renderer;

    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();

    /// <summary>Throw the bakes away when the PALETTE has been repainted. A drawing is baked to pixels, and the key is
    /// the drawing and the size - not the colours, which live in brushes the palette owns and rewrites in place. So an
    /// icon baked under one variant kept its pixels under the next, and the chevrons stayed in the colour they were
    /// first drawn in while every vector around them followed.</summary>
    private static void DropStaleBakes()
    {
        var palette = Core.Resources.ThemeManager.PaletteVersion;
        if (palette == _bakedPalette) return;

        _bakedPalette = palette;
        _baked.Clear();
        _pending.Clear();

        // Dropping the pixels is not enough: whoever DREW them holds the texture, and its Source is the same drawing
        // object it always was - no property changed, so nothing would ask again. Tell the elements that asked for a
        // bake to re-record; they are the few that draw icons, not the scene.
        foreach (var owner in _owners.Keys)
        {
            owner.InvalidateRender(false);
        }
    }

    /// <summary>What to draw for this drawing at this size RIGHT NOW - the exact bake, else a stand-in, else null - and
    /// the exact bake is queued on the way out when it does not exist yet. Render-thread safe: it reads the cache and
    /// posts the bake to the loop thread, never waiting for one.
    /// <para>The queueing lives IN HERE, not at the call site, and that is the whole point. It used to be the caller's
    /// second step, taken only when this returned null - so the moment a stand-in was good enough to return, the exact
    /// bake was never ordered and the stand-in became permanent. On screen that read as a viewbox that changed nothing:
    /// every new slice was answered, for ever, by whichever slice had been baked first.</para></summary>
    public static BitmapSource Get(DrawingImage image, Size size, IUIComponent owner, Vector4F slice = default)
    {
        DropStaleBakes();
        if (owner != null) _owners[owner] = 0;

        if (slice == default) slice = WholePicture;
        var key = KeyOf(image, size, DeviceScaleOf(owner), slice);
        if (key.Width <= 0 || key.Height <= 0)
        {
            return null;
        }

        if (_baked.TryGetValue(key, out var baked))
        {
            return baked;
        }

        // MISS, but this drawing may already be baked at another size or another slice. Drawing nothing until the new
        // one lands is what made a fill flicker - and vanish - while a slider moved: every frame asks for something that
        // is not there yet. So something in hand stands in meanwhile, and is replaced when the exact one arrives.
        var standIn = StandInFor(image, key);
        Request(image, size, owner, slice);
        return standIn;
    }

    /// <summary>How good a bake already in hand is as a stand-in for the one being asked for - lower is better, -1 for
    /// unusable. Only a mismatched ASPECT disqualifies: the fill samples the whole texture, so a picture of another
    /// shape would arrive distorted, while another size or another part of the drawing merely looks wrong for a frame.
    /// </summary>
    private static int StandInRank((int Width, int Height, int Slice) candidate, (int Width, int Height, int Slice) wanted)
    {
        if (candidate.Height <= 0 || wanted.Height <= 0)
        {
            return -1;
        }

        var aspect = (double)candidate.Width / candidate.Height;
        return System.Math.Abs(aspect - (double)wanted.Width / wanted.Height) > 0.01
            ? -1
            : candidate.Slice == wanted.Slice ? 0 : 1;
    }

    /// <summary>Which of the bakes in hand to show while the asked-for one is queued; -1 when none may. Both ranks were
    /// learned from a defect on the stand, and it is the ORDER between them that makes each safe.
    /// <para>The SAME slice at another size comes first: it is the same picture, only sharper or blurrier. Preferring
    /// anything else there is what made the viewbox appear to do nothing at all - every not-yet-baked viewbox was
    /// handed whichever slice happened to be baked first.</para>
    /// <para>Another slice is taken only when this one has no bake at any size - which is exactly what a viewbox that
    /// has just changed is. Refusing it left the fill BLANK on every step of the slider, so the picture blinked its way
    /// through the drag. It settles honestly because the exact bake lands a frame later and outranks it from then on -
    /// a wrong slice is what shows in flight, never what shows at rest.</para></summary>
    internal static int PickStandIn(IReadOnlyList<(int Width, int Height, int Slice)> candidates, (int Width, int Height, int Slice) wanted)
    {
        var best = -1;
        var bestRank = int.MaxValue;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < candidates.Count; i++)
        {
            var rank = StandInRank(candidates[i], wanted);
            if (rank < 0)
            {
                continue;
            }

            var distance = System.Math.Abs(candidates[i].Width - wanted.Width);
            if (rank > bestRank || (rank == bestRank && distance >= bestDistance))
            {
                continue;
            }

            best = i;
            bestRank = rank;
            bestDistance = distance;
        }

        return best;
    }

    // The bake to show while the asked-for one is queued - PickStandIn decides which of this drawing's is it.
    private static BitmapSource StandInFor(DrawingImage image, (DrawingImage Image, int Width, int Height, int Scale, int Slice) key)
    {
        List<(DrawingImage Image, int Width, int Height, int Scale, int Slice)> mine = [];
        List<(int Width, int Height, int Slice)> shapes = [];

        foreach (var pair in _baked)
        {
            if (!ReferenceEquals(pair.Key.Image, image))
            {
                continue;
            }

            mine.Add(pair.Key);
            shapes.Add((pair.Key.Width, pair.Key.Height, pair.Key.Slice));
        }

        var pick = PickStandIn(shapes, (key.Width, key.Height, key.Slice));
        return pick < 0 ? null : _baked.GetValueOrDefault(mine[pick]);
    }

    /// <summary>Queue a bake for a size that has none yet, and repaint whoever asked once it exists. Called from
    /// <see cref="Get"/> on every miss, so it has to be cheap: a size already baked or already queued does nothing.
    /// </summary>
    private static void Request(DrawingImage image, Size size, IUIComponent owner, Vector4F slice)
    {
        if (slice == default) slice = WholePicture;
        var key = KeyOf(image, size, DeviceScaleOf(owner), slice);
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
        var palette = _bakedPalette;
        var asked = slice;
        dispatcher.Post(() => Bake(key, new Size(key.Width, key.Height), owner, palette, asked));
    }

    /// <summary>Throw away everything baked from this drawing - its picture changed, so every size of it is now wrong.</summary>
    public static void Invalidate(DrawingImage image)
    {
        List<(DrawingImage Image, int Width, int Height, int Scale, int Slice)> stale = [];
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

    private static void Evict((DrawingImage Image, int Width, int Height, int Scale, int Slice) newest)
    {
        List<(DrawingImage Image, int Width, int Height, int Scale, int Slice)> mine = [];
        foreach (var key in _baked.Keys)
        {
            if (ReferenceEquals(key.Image, newest.Image))
            {
                mine.Add(key);
            }
        }

        // The one just baked is NOT a candidate. It used to be: the list is sorted by distance in WIDTH, and every slice
        // of a drawing bakes at the same width (the tile's), so the sort could not tell them apart and threw away the
        // very bitmap that had just been stored - the exact bake never survived long enough to replace the stand-in, so
        // walking the viewbox slider left the fill showing whatever slice was baked first.
        mine.Remove(newest);
        if (mine.Count <= KeptSizesPerDrawing - 1)
        {
            return;
        }

        // Another SLICE goes before another size: a slice nobody is showing is dead weight, while a nearby size of the
        // slice in use is the stand-in that keeps the fill on screen while the exact size bakes.
        mine.Sort((a, b) =>
        {
            var aOther = a.Slice != newest.Slice ? 1 : 0;
            var bOther = b.Slice != newest.Slice ? 1 : 0;
            return aOther != bOther
                ? bOther.CompareTo(aOther)
                : System.Math.Abs(b.Width - newest.Width).CompareTo(System.Math.Abs(a.Width - newest.Width));
        });

        for (var i = 0; i < mine.Count - (KeptSizesPerDrawing - 1); i++)
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
    private static (DrawingImage Image, int Width, int Height, int Scale, int Slice) KeyOf(DrawingImage image, Size size, double deviceScale, Vector4F slice)
    {
        var longest = System.Math.Max(size.Width, size.Height);
        if (longest <= 0)
        {
            return (image, 0, 0, 0, 0);
        }

        var scale = System.Math.Ceiling(longest / SizeStep) * SizeStep / longest;
        return (image,
            (int)System.Math.Round(size.Width * scale),
            (int)System.Math.Round(size.Height * scale),
            (int)System.Math.Round(System.Math.Max(0.01, deviceScale) * 100),
            SliceKey(slice));
    }

    /// <summary>The slice, folded into one number so it can join the cache key. It HAS to be in there: two brushes
    /// showing different parts of one drawing at the same bake size are different pictures, and without this the second
    /// would be handed the first one's pixels. Quantised to 1/1000 of the source - finer than any viewbox a person
    /// states, coarse enough that a slider dragged through it does not mint a bake per frame.</summary>
    private static int SliceKey(Vector4F slice)
    {
        if (slice.X == 0 && slice.Y == 0 && slice.Z == 1 && slice.W == 1) return 0;   // the whole picture

        static int Q(float v) => (int)System.Math.Round(System.Math.Clamp(v, -1f, 2f) * 1000);
        return HashCode.Combine(Q(slice.X), Q(slice.Y), Q(slice.Z), Q(slice.W));
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
    private static void Bake((DrawingImage Image, int Width, int Height, int Scale, int Slice) key, Size size, IUIComponent owner, int palette, Vector4F slice)
    {
        var renderer = Renderer;
        if (renderer == null)
        {
            _pending.TryRemove(key, out _);
            return;
        }

        var picture = new Image
        {
            Source = key.Image,
            Stretch = Stretch.Fill,
            Width = size.Width,
            Height = size.Height,
            // A drawing resource binds against whoever shows it; a host shown by nobody has no DataContext, so an icon
            // whose brushes bind to a view model bakes out blank.
            DataContext = owner?.DataContext
        };

        IUIComponent host = picture;

        // A VIEWBOX asks for part of the picture, and that part is what gets the pixels: the drawing is blown up so the
        // slice fills the bake, then clipped to it. Baking the whole thing and sampling a slice of the result spends the
        // resolution on what nobody sees - a 0.3 viewbox drew its edges over 2 px where a full one took 1.
        if (slice.Z > 0 && slice.W > 0 && (slice.X != 0 || slice.Y != 0 || slice.Z != 1 || slice.W != 1))
        {
            picture.Width = size.Width / slice.Z;
            picture.Height = size.Height / slice.W;
            picture.HorizontalAlignment = HorizontalAlignment.Left;
            picture.VerticalAlignment = VerticalAlignment.Top;

            var canvas = new Controls.Panels.Canvas
            {
                Width = size.Width,
                Height = size.Height,
                ClipToBounds = true,
                DataContext = owner?.DataContext
            };
            Controls.Panels.Canvas.SetLeft(picture, -slice.X * picture.Width);
            Controls.Panels.Canvas.SetTop(picture, -slice.Y * picture.Height);
            canvas.Children.Add(picture);
            host = canvas;
        }

        // The LOGICAL size lays the host out; the device scale decides how many PIXELS come back. Baking at 1.0 on a
        // 150% display handed the fill a texture two thirds of the resolution it is drawn at - the whole reason a vector
        // source exists is that it does not have to blur.
        renderer.RequestRender(host, size, key.Scale / 100.0, Colors.Transparent, image => Store(key, image, owner, palette));
    }

    // UI thread, once the render thread has drawn and read the bake back.
    private static void Store((DrawingImage Image, int Width, int Height, int Scale, int Slice) key, ImageSource rendered, IUIComponent owner, int palette)
    {
        _pending.TryRemove(key, out _);

        // Drawn BEFORE the palette was repainted: these are last variant's pixels, and filing them would pin the icon
        // to the colour it had when the bake was asked for. Ask again instead.
        if (palette != _bakedPalette)
        {
            owner?.InvalidateRender(false);
            return;
        }

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
