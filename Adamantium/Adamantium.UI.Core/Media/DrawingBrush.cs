using Adamantium.UI.Core.Media.Drawings;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media;

/// <summary>Fills a shape with a <see cref="Drawings.Drawing"/> - shapes, text and pictures authored in markup rather
/// than loaded from a file. WPF's <c>DrawingBrush</c>. Everything about tiling and fitting is
/// <see cref="TileBrush"/>'s; what this adds is only where the content comes from.
/// <para>The drawing is handed on as a <see cref="Imaging.DrawingImage"/>, which is what the render paths already know
/// how to draw and to bake. A brush that reimplemented that would be a second way to draw the same drawing, and the
/// two would drift.</para></summary>
public sealed class DrawingBrush : TileBrush
{
    // PAINT: the drawing fills the shape it is given, so swapping it re-colours the same pixels and never touches layout.
    public static readonly AdamantiumProperty DrawingProperty = AdamantiumProperty.Register(nameof(Drawing),
        typeof(Drawing), typeof(DrawingBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnDrawingChanged));

    // SHARED with every frozen clone of this brush (see CreateClone): the raster fallback keys its bake cache by this
    // object's identity, so a clone with a fresh one would miss the cache for ever - and, because a snapshot is rebuilt
    // on EVERY property change, order a new bake (with its own render target) per change until the device runs out.
    private DrawingImage _content = new();

    public DrawingBrush() { }

    public DrawingBrush(Drawing drawing) => Drawing = drawing;

    /// <summary>The picture. [Content] so AUML writes it as the child element.</summary>
    [Content]
    public Drawing Drawing
    {
        get => GetValue<Drawing>(DrawingProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(DrawingProperty, value);
        }
    }

    public override ImageSource ContentSource => _content;

    private static void OnDrawingChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is not DrawingBrush brush)
        {
            return;
        }

        // The drawing sits BEHIND a DrawingImage the render paths hold on to, so it is swapped there rather than the
        // image being replaced - a new image would be a new bake key and every consumer's cached texture would be lost.
        brush._content.Drawing = e.NewValue as Drawing;
    }

    protected override Brush CreateClone()
    {
        var clone = new DrawingBrush();
        clone._content = _content;   // BEFORE Drawing, so the setter below writes the shared image, not the fresh one
        clone.Drawing = Drawing;
        CopyTilingTo(clone);
        return clone;
    }
}
