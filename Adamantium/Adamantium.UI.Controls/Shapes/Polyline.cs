using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Shapes;

public class Polyline : CurveBase
{
    public Polyline()
    {
    }

    public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
        typeof(PointsCollection), typeof(Polyline),
        new PropertyMetadata(null,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender, 
            PointsChangedCallback));
        
    private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Polyline line)
        {
            if (e.OldValue is PointsCollection collection1) collection1.CollectionChanged -= line.PointsOnCollectionChanged;
            
            if (e.NewValue is PointsCollection collection2) collection2.CollectionChanged += line.PointsOnCollectionChanged;
        }
    }

    protected virtual void PointsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
    }
        
    public PointsCollection Points
    {
        get => GetValue<PointsCollection>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Points == null || Points.Count == 0) return Size.Zero;
        
        var maxX = Points.Select(x=>x.X).Max();
        var maxY = Points.Select(y=>y.Y).Max();
        Rect = new Rect(Vector2.Zero, new Vector2(maxX, maxY));
        return base.MeasureOverride(availableSize);
    }

    protected override void OnRender(IDrawingContext context)
    {
        // FRESH geometry each render (see CubicBezierCurve): the render cache compares geometry by reference, so a
        // reused instance mutated in place is never seen as changed - a Points/Samples change would not rebuild the stroke.
        var geometry = new StreamGeometry { IsClosed = false };
        var streamContext = geometry.Open();
        streamContext.BeginFigure(Points[0], false, false).PolylineLineTo(Points.Skip(1), true);

        // A polyline is OPEN and stroke-first: fill with Fill (null = none), not Stroke, and don't close the figure.
        context.ForControl(this).DrawGeometry(Fill, geometry, GetPen());
    }
}