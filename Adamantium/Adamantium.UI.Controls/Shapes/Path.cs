using System;
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
        if (Data == null) return new Size(0, 0);
        Data.RecalculateBounds();
        // Use the mesh contours for the exact miter extent only if the geometry is already tessellated (e.g. by a
        // prior render). Forcing ProcessGeometry here is unsafe: a boolean CombinedGeometry yields an empty mesh at
        // measure time, which both collapses the size and (via the IsProcessed cache) would break its rendering. The
        // bbox floor from Data.Bounds always holds, so combined/group geometries size correctly without contours.
        var contours = Data.IsProcessed ? GetContours(Data.Mesh) : null;
        return MeasureStrokedGeometry(Data.Bounds, contours);
    }

    protected override void OnRender(IDrawingContext context)
    {
        // No geometry -> nothing to draw. Guards against a NullReferenceException in the geometry render unit
        // (it dereferences the payload geometry) when a Path has no Data, e.g. mid-edit in the live designer.
        if (Data == null) return;

        context.ForControl(this).DrawGeometry(Fill, Data, GetPen());
    }

    // Tight hit test: point-in-geometry over the mesh contours (even-odd) for a filled path, plus proximity to a
    // contour edge for the stroke. Falls back to the bounding box only if the mesh isn't built yet. This excludes the
    // empty parts of the (often large) bounding box - e.g. between the teeth/inside the gaps of a combined geometry.
    public override bool HitTestCore(Vector2 localPoint)
    {
        var mesh = Data?.Mesh;
        if (mesh?.Contours == null || mesh.Contours.Count == 0) return base.HitTestCore(localPoint);

        var inside = false;
        var minDist = double.MaxValue;
        foreach (var contour in mesh.Contours)
        {
            var pts = contour.Points;
            if (pts == null || pts.Length < 2) continue;
            int n = pts.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = pts[i];
                var pj = pts[j];
                if (((pi.Y > localPoint.Y) != (pj.Y > localPoint.Y)) &&
                    (localPoint.X < (pj.X - pi.X) * (localPoint.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                    inside = !inside;
                minDist = Math.Min(minDist, DistanceToSegment(localPoint, pi, pj));
            }
        }

        if (IsVisibleBrush(Fill) && inside) return true;
        return IsVisibleBrush(Stroke) && minDist <= StrokeThickness / 2.0 + 1.0;
    }
}