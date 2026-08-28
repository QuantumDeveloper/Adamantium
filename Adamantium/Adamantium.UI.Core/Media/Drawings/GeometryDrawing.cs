using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>One shape of a drawing: a <see cref="Media.Geometry"/> filled with <see cref="Brush"/> and/or stroked with
/// <see cref="Pen"/>. The workhorse of the family - a vector icon is a handful of these in a <see cref="DrawingGroup"/>.</summary>
public class GeometryDrawing : Drawing
{
    public static readonly AdamantiumProperty GeometryProperty = AdamantiumProperty.Register(nameof(Geometry),
        typeof(Geometry), typeof(GeometryDrawing), new PropertyMetadata(null));

    public static readonly AdamantiumProperty BrushProperty = AdamantiumProperty.Register(nameof(Brush),
        typeof(Brush), typeof(GeometryDrawing), new PropertyMetadata(null, BrushChangedCallback));

    public static readonly AdamantiumProperty PenProperty = AdamantiumProperty.Register(nameof(Pen),
        typeof(Pen), typeof(GeometryDrawing), new PropertyMetadata(null));

    /// <summary>The shape, in the drawing's own coordinates.</summary>
    public Geometry Geometry
    {
        get => GetValue<Geometry>(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    /// <summary>The fill. Null or transparent draws outline only.</summary>
    public Brush Brush
    {
        get => GetValue<Brush>(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>The outline. Null draws fill only.</summary>
    public Pen Pen
    {
        get => GetValue<Pen>(PenProperty);
        set => SetValue(PenProperty, value);
    }

    /// <summary>The stroke brush, for markup. A <see cref="Media.Pen"/> is immutable by design - every field set once,
    /// a new pen built on any change - so it cannot be authored property-by-property in AUML. A shape does not ask for
    /// one either: <c>Path</c> takes Stroke + StrokeThickness and builds the pen itself, and a drawing does the same
    /// here. Without this a vector icon could only ever be a FILL, which rules out half of any icon set - a cross, a
    /// checkmark, an arrow are strokes.
    /// <para>Setting <see cref="Pen"/> directly still wins: it is the explicit, full form.</para></summary>
    public static readonly AdamantiumProperty StrokeProperty = AdamantiumProperty.Register(nameof(Stroke),
        typeof(Brush), typeof(GeometryDrawing), new PropertyMetadata(null, StrokeBrushChangedCallback));

    public static readonly AdamantiumProperty StrokeThicknessProperty = AdamantiumProperty.Register(nameof(StrokeThickness),
        typeof(double), typeof(GeometryDrawing), new PropertyMetadata(1.0, StrokeChangedCallback));

    public Brush Stroke
    {
        get => GetValue<Brush>(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue<double>(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    // Either half arriving builds the pen. Both are ordinary property writes from markup and can land in any order, so
    // this must not assume the brush comes first.
    private static void StrokeChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not GeometryDrawing drawing) return;

        drawing.Pen = drawing.Stroke == null ? null : new Pen(drawing.Stroke, drawing.StrokeThickness);
    }

    // The stroke brush is watched exactly as the fill brush is below, and for the same reason: half an icon set IS a
    // stroke - a cross, a checkmark, an arrow - and recolouring one changes the picture just as much. Only Brush was
    // hooked, so a recoloured stroke told nobody, and whoever holds PIXELS of the drawing (a baked tile brush, a
    // nine-slice) kept the old ones for good - a bake is only ever thrown away on this event.
    private static void StrokeBrushChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not GeometryDrawing drawing) return;

        if (e.OldValue is Brush oldBrush)
        {
            oldBrush.Changed -= drawing.OnChildChanged;
        }

        if (e.NewValue is Brush newBrush)
        {
            newBrush.Changed += drawing.OnChildChanged;
        }

        StrokeChangedCallback(a, e);
    }

    // A brush mutating INTERNALLY (a recoloured fill, an animated gradient stop) changes the picture just as much as
    // swapping the brush does, and the drawing is not an element, so nothing else is listening on its behalf.
    private static void BrushChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not GeometryDrawing drawing) return;

        if (e.OldValue is Brush oldBrush)
        {
            oldBrush.Changed -= drawing.OnChildChanged;
        }

        if (e.NewValue is Brush newBrush)
        {
            newBrush.Changed += drawing.OnChildChanged;
        }
    }

    // The geometry too, not just the brush: <RectangleGeometry Rect="{Binding IconRect}"/> is the same markup and would
    // fail the same silent way.
    protected override void AttachChildren()
    {
        AttachOwned(Brush);
        AttachOwned(Stroke);
        AttachOwned(Geometry);
        AttachOwned(Geometry?.Transform);
    }

    /// <summary>The shape's extent, widened by half the stroke - a stroke straddles the contour, so an icon whose outline
    /// reaches the viewbox edge would otherwise have its outer half clipped away by the consumer's viewbox mapping.</summary>
    public override Rect Bounds
    {
        get
        {
            var geometry = Geometry;
            if (geometry == null) return Rect.Empty;

            // Geometry.Bounds is only filled in by tessellation, and a drawing is MEASURED before anything tessellates
            // it - so reading it raw hands back an empty box, the consumer measures zero and the icon never appears.
            // Recalculating is the cheap half of ProcessGeometry (no mesh), and it is idempotent.
            geometry.RecalculateBounds();

            var bounds = geometry.Bounds;
            var pen = Pen;
            if (pen == null) return bounds;

            return bounds.Inflate(pen.Thickness / 2.0);
        }
    }

    public override void Render(IDrawingSession session, Matrix4x4F transform)
    {
        var geometry = Geometry;
        if (geometry == null) return;

        session.DrawGeometry(Brush, geometry, Pen, transform);
    }
}
