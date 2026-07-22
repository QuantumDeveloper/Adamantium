using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Core.RoutedEvents;

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
        WatchStops();
    }

    protected GradientBrush(GradientStopCollection stops)
    {
        GradientStops = stops ?? [];
        WatchStops();
    }

    // The stops are a collection, not brush properties, so the base Brush's own PropertyChanged->Changed bridge misses
    // them. Raise Changed when the stop SET changes (add/remove) OR any stop's Offset/Color changes - so an ANIMATED
    // gradient (a shimmer sweeping the offsets forever) repaints the drawing element every frame.
    private void WatchStops()
    {
        foreach (var stop in GradientStops) stop.PropertyChanged += OnStopChanged;
        GradientStops.CollectionChanged += OnStopsCollectionChanged;
    }

    private void OnStopsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (GradientStop stop in e.OldItems) stop.PropertyChanged -= OnStopChanged;
        if (e.NewItems != null)
            foreach (GradientStop stop in e.NewItems) stop.PropertyChanged += OnStopChanged;
        RaiseChanged();
    }

    private void OnStopChanged(object sender, AdamantiumPropertyChangedEventArgs e) => RaiseChanged();

    // PAINT: how the gradient extends past its ends is evaluated per-fragment; the shape is untouched (see Brush.Opacity).
    public static readonly AdamantiumProperty SpreadMethodProperty = AdamantiumProperty.Register(nameof(SpreadMethod),
        typeof(GradientSpreadMethod), typeof(GradientBrush), new PropertyMetadata(GradientSpreadMethod.Pad, PropertyMetadataOptions.AffectsPaint));

    // PAINT: how the stops are interpolated (sRGB vs OKLab) is a per-fragment colour choice, never a shape/layout change.
    public static readonly AdamantiumProperty ColorInterpolationModeProperty = AdamantiumProperty.Register(nameof(ColorInterpolationMode),
        typeof(ColorInterpolationMode), typeof(GradientBrush), new PropertyMetadata(ColorInterpolationMode.Srgb, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The colour stops, ordered by the author; the renderer reads them sorted by <see cref="GradientStop.Offset"/>.
    /// A collection (not an AdamantiumProperty) so it is never a shared mutable default across instances. [Content] so AUML
    /// populates it from the brush's child &lt;GradientStop/&gt; elements (like GeometryGroup.Children).</summary>
    [Content]
    public GradientStopCollection GradientStops { get; }

    public GradientSpreadMethod SpreadMethod
    {
        get => GetValue<GradientSpreadMethod>(SpreadMethodProperty);
        set { if (IsFrozen) return; SetValue(SpreadMethodProperty, value); }
    }

    /// <summary>How to interpolate between stops - sRGB (default, WPF-like) or perceptual OKLab.</summary>
    public ColorInterpolationMode ColorInterpolationMode
    {
        get => GetValue<ColorInterpolationMode>(ColorInterpolationModeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(ColorInterpolationModeProperty, value);
        }
    }

    // A deep copy of the stops as NEW GradientStop instances, so a frozen gradient never aliases the live, animatable stop
    // objects (freezing must SNAPSHOT the stop colours/offsets). Used by each concrete brush's CreateFrozenCore.
    protected GradientStopCollection CopyStops() => [..GradientStops.Select(s => new GradientStop(s.Color, s.Offset))];
}
