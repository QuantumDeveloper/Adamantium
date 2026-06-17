using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Shapes;

public class Path : Shape
{
    public static readonly AdamantiumProperty DataProperty =
        AdamantiumProperty.Register(nameof(Data), typeof(Geometry), typeof(Path),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, DataChangedCallback));

    public static readonly AdamantiumProperty FillRuleProperty =
        AdamantiumProperty.Register(nameof(FillRule), typeof(FillRule), typeof(Path),
            new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender, FillRuleChangedCallback));

    // Whether FillRule was set explicitly (vs. left at the default). Set from the CLR setter, which both markup
    // (reflection PropertyInfo.SetValue) and the code-behind generator go through; the property system's own
    // default initialization writes the value container directly, bypassing the setter, so it stays false there.
    // Only an explicit value overrides the rule the geometry already carries (a GeometryGroup's own FillRule, or
    // an SVG Data string's F0/F1 token).
    private bool _fillRuleSet;

    private static void DataChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Path path)
        {
            if (e.OldValue is Geometry geometry1) geometry1.ComponentUpdated -= path.OnGeometryUpdated;
            if (e.NewValue is Geometry geometry2) geometry2.ComponentUpdated += path.OnGeometryUpdated;
            path.ApplyFillRule(e.NewValue as Geometry);
        }
    }

    private static void FillRuleChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Path path) path.ApplyFillRule(path.Data);
    }

    // Push an explicitly-set FillRule into the geometry that actually carries it (StreamGeometry from SVG Data, or
    // a GeometryGroup). Other geometry types have no fill rule, so they're left untouched.
    private void ApplyFillRule(Geometry geometry)
    {
        if (!_fillRuleSet || geometry == null) return;

        switch (geometry)
        {
            case StreamGeometry stream: stream.FillRule = FillRule; break;
            case GeometryGroup group: group.FillRule = FillRule; break;
            default: return;
        }
        geometry.InvalidateGeometry();
    }

    private void OnGeometryUpdated(object sender, ComponentUpdatedEventArgs e)
    {
        InvalidateMeasure();
    }

    public Path()
    {
    }

    public Geometry Data
    {
        get => GetValue<Geometry>(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>How the interior of <see cref="Data"/> is determined. Left unset it uses the rule the geometry
    /// already carries (a GeometryGroup's FillRule, or an SVG Data string's F0/F1 token); setting it explicitly
    /// overrides that.</summary>
    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set { _fillRuleSet = true; SetValue(FillRuleProperty, value); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Data != null)
        {
            Data.RecalculateBounds();

            Rect = Data.Bounds;
        }
        return base.MeasureOverride(Rect.Size);
    }

    protected override void OnRender(IDrawingContext context)
    {
        // No geometry -> nothing to draw. Guards against a NullReferenceException in the geometry render unit
        // (it dereferences the payload geometry) when a Path has no Data, e.g. mid-edit in the live designer.
        if (Data == null) return;

        context.ForControl(this).DrawGeometry(Fill, Data, GetPen());
    }
}