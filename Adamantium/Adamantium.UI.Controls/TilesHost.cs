using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Items host for a 3D flip-tile board (the Windows-8-start-screen control family): owns the pointer TILT FIELD
/// (every tile leans toward the cursor, the angle growing with distance), the board-wide FLIP WAVE
/// (<see cref="IsFlipped"/> sweeps the board diagonally) and the shared <see cref="Photo"/> whose fragments the tiles
/// reveal. ONE image property - loaded asynchronously once for the whole board - and each tile's UV fragment is
/// computed from its ACTUAL arranged bounds, so the mosaic lines run straight across the inter-tile gaps for any tile
/// size/margin. Replaces the per-view TiltField/FlipAll behaviors with a reusable control.
/// </summary>
public class TilesHost : ItemsControl
{
    /// <summary>The one photo the whole board reveals; decoded once, its texture shared by every tile.</summary>
    public static readonly AdamantiumProperty PhotoProperty = AdamantiumProperty.Register(nameof(Photo),
        typeof(ImageSource), typeof(TilesHost), new PropertyMetadata(null, OnPhotoChanged));

    /// <summary>Board state: setting it flips every tile as a diagonal wave (see <see cref="WaveDuration"/>).</summary>
    public static readonly AdamantiumProperty IsFlippedProperty = AdamantiumProperty.Register(nameof(IsFlipped),
        typeof(bool), typeof(TilesHost), new PropertyMetadata(false, OnIsFlippedChanged));

    /// <summary>Tilt clamp in degrees - how far a tile may lean toward the cursor.</summary>
    public static readonly AdamantiumProperty TiltMaxAngleProperty = AdamantiumProperty.Register(nameof(TiltMaxAngle),
        typeof(double), typeof(TilesHost), new PropertyMetadata(34.0));

    /// <summary>How fast the lean grows with the tile's distance from the cursor (degrees per pixel).</summary>
    public static readonly AdamantiumProperty TiltAnglePerPixelProperty = AdamantiumProperty.Register(nameof(TiltAnglePerPixel),
        typeof(double), typeof(TilesHost), new PropertyMetadata(0.045));

    /// <summary>How long the flip wave takes to sweep from the board's first tile to its last (seconds); each tile's
    /// start delay is its diagonal position within the board scaled into this window.</summary>
    public static readonly AdamantiumProperty WaveDurationProperty = AdamantiumProperty.Register(nameof(WaveDuration),
        typeof(double), typeof(TilesHost), new PropertyMetadata(0.6));

    public ImageSource Photo { get => GetValue<ImageSource>(PhotoProperty); set => SetValue(PhotoProperty, value); }
    public bool IsFlipped { get => GetValue<bool>(IsFlippedProperty); set => SetValue(IsFlippedProperty, value); }
    public double TiltMaxAngle { get => GetValue<double>(TiltMaxAngleProperty); set => SetValue(TiltMaxAngleProperty, value); }
    public double TiltAnglePerPixel { get => GetValue<double>(TiltAnglePerPixelProperty); set => SetValue(TiltAnglePerPixelProperty, value); }
    public double WaveDuration { get => GetValue<double>(WaveDurationProperty); set => SetValue(WaveDurationProperty, value); }

    private readonly List<FlipTile> _tiles = new();

    public TilesHost()
    {
        MouseMove += OnHostMouseMove;
        MouseLeave += OnHostMouseLeave;
    }

    // --- Photo + UV fragments -------------------------------------------------------------------------------------

    private static void OnPhotoChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TilesHost host) host.AssignFragments();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // Tiles have their final bounds now: hand each one the shared photo and its gap-true UV fragment. Repeat
        // arranges re-write the same values (a no-op set) - only a real layout change moves the fragments.
        AssignFragments();
        return size;
    }

    private void AssignFragments()
    {
        CollectTiles();
        if (_tiles.Count == 0) return;

        // The photo maps over the UNION of the tile rects (host space) - the exact tile field, independent of any
        // panel/host padding. Each tile then samples the photo portion its VISIBLE rect covers, so image lines
        // continue straight across the inter-tile gaps instead of jumping at every boundary.
        double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
        var rects = new Rect[_tiles.Count];
        for (var i = 0; i < _tiles.Count; i++)
        {
            rects[i] = RectInHost(_tiles[i]);
            l = Math.Min(l, rects[i].X);
            t = Math.Min(t, rects[i].Y);
            r = Math.Max(r, rects[i].Right);
            b = Math.Max(b, rects[i].Bottom);
        }
        var w = r - l;
        var h = b - t;
        if (w <= 0 || h <= 0) return;

        var photo = Photo;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            tile.Photo = photo;
            tile.SourceU = (rects[i].X - l) / w;
            tile.SourceV = (rects[i].Y - t) / h;
            tile.SourceUW = rects[i].Width / w;
            tile.SourceVH = rects[i].Height / h;
        }
    }

    // --- Flip wave ------------------------------------------------------------------------------------------------

    private static void OnIsFlippedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // Same Unset-gate trap as FlipTile.OnIsFlippedChanged: the binding's first push arrives with OldValue = Unset,
        // and gating on it swallowed the board's FIRST sweep (state flipped, no wave). Compare effective values.
        var was = e.OldValue is bool oldFlipped && oldFlipped;
        var now = (bool)e.NewValue;
        if (a is TilesHost host && was != now)
            host.FlipWave(now);
    }

    private void FlipWave(bool flipped)
    {
        CollectTiles();
        if (_tiles.Count == 0) return;

        // Diagonal wave: a tile's start delay is its (x+y) position across the board normalised into WaveDuration.
        double maxDiag = 0;
        var centres = new Vector2[_tiles.Count];
        for (var i = 0; i < _tiles.Count; i++)
        {
            var rect = RectInHost(_tiles[i]);
            centres[i] = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            maxDiag = Math.Max(maxDiag, centres[i].X + centres[i].Y);
        }
        var wave = WaveDuration;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            tile.FlipDelay = maxDiag > 0 ? (centres[i].X + centres[i].Y) / maxDiag * wave : 0;
            tile.IsFlipped = flipped;
        }
    }

    // --- Tilt field -----------------------------------------------------------------------------------------------

    private void OnHostMouseMove(object sender, MouseEventArgs e)
    {
        var cursor = e.GetPosition(this);
        var maxAngle = TiltMaxAngle;
        var perPixel = TiltAnglePerPixel;

        CollectTiles();
        foreach (var tile in _tiles)
        {
            var rect = RectInHost(tile);
            var dx = cursor.X - (rect.X + rect.Width / 2);
            var dy = cursor.Y - (rect.Y + rect.Height / 2);
            // CONCAVE dish around the pointer (a satellite antenna, not a bump): every tile's near edge - the one
            // facing the cursor - sinks away from the viewer, so the whole board reads as a bowl centred on the cursor.
            var rotY = Math.Clamp(dx * perPixel, -maxAngle, maxAngle);
            var rotX = Math.Clamp(-dy * perPixel, -maxAngle, maxAngle);
            tile.SetFieldTilt(rotX, rotY);
        }
    }

    private void OnHostMouseLeave(object sender, MouseEventArgs e)
    {
        CollectTiles();
        foreach (var tile in _tiles)
            tile.EaseTiltBack();
    }

    // --- Helpers --------------------------------------------------------------------------------------------------

    private Rect RectInHost(FlipTile tile)
    {
        double x = tile.Bounds.X, y = tile.Bounds.Y;
        for (IUIComponent p = tile.VisualParent; p != null && !ReferenceEquals(p, this); p = p.VisualParent)
        {
            x += p.Bounds.X;
            y += p.Bounds.Y;
        }
        return new Rect(x, y, tile.Bounds.Width, tile.Bounds.Height);
    }

    private void CollectTiles()
    {
        _tiles.Clear();
        Collect(this, _tiles);

        static void Collect(IUIComponent node, List<FlipTile> tiles)
        {
            if (node is FlipTile tile) { tiles.Add(tile); return; }
            foreach (var child in node.VisualChildren)
                Collect(child, tiles);
        }
    }
}
