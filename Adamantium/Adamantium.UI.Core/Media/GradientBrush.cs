namespace Adamantium.UI.Core.Media;

/// <summary>Base for the gradient brushes (<see cref="LinearGradientBrush"/>, <see cref="RadialGradientBrush"/>): a set of
/// colour <see cref="GradientStops"/> and how the gradient extends past its ends (<see cref="SpreadMethod"/>). The
/// concrete brush adds the geometry (a linear axis or a radial ellipse) the stops are laid out along. All coordinates are
/// RELATIVE to the filled bounds (0..1), so one brush paints any tile size.</summary>
public abstract class GradientBrush : Brush
{
    protected GradientBrush()
    {
        GradientStops = [];
    }

    protected GradientBrush(GradientStopCollection stops)
    {
        GradientStops = stops ?? [];
    }

    public static readonly AdamantiumProperty SpreadMethodProperty = AdamantiumProperty.Register(nameof(SpreadMethod),
        typeof(GradientSpreadMethod), typeof(GradientBrush), new PropertyMetadata(GradientSpreadMethod.Pad));

    /// <summary>The colour stops, ordered by the author; the renderer reads them sorted by <see cref="GradientStop.Offset"/>.
    /// A collection (not an AdamantiumProperty) so it is never a shared mutable default across instances. [Content] so AUML
    /// populates it from the brush's child &lt;GradientStop/&gt; elements (like GeometryGroup.Children).</summary>
    [Content]
    public GradientStopCollection GradientStops { get; }

    public GradientSpreadMethod SpreadMethod
    {
        get => GetValue<GradientSpreadMethod>(SpreadMethodProperty);
        set => SetValue(SpreadMethodProperty, value);
    }
}
