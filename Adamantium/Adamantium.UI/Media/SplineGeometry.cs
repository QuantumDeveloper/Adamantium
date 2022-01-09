using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public class SplineGeometry : Geometry
{
    private Rect bounds;
    public override Rect Bounds => bounds;
    
    public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
        typeof(PointsCollection), typeof(SplineGeometry),
        new PropertyMetadata(null,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender, 
            PointsChangedCallback));
    
    private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is SplineGeometry spline)
        {
            if (e.OldValue is PointsCollection collection1) collection1.CollectionChanged -= spline.PointsOnCollectionChanged;
            
            if (e.NewValue is PointsCollection collection2) collection2.CollectionChanged += spline.PointsOnCollectionChanged;
        }
    }
    
    protected virtual void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
    }
    
    public PointsCollection Points
    {
        get => GetValue<PointsCollection>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }
    
    public override Geometry Clone()
    {
        throw new System.NotImplementedException();
    }

    public override void RecalculateBounds()
    {
        if (Points == null || Points.Count == 0) return;
        
        var maxX = Points.Select(x=>x.X).Max();
        var maxY = Points.Select(y=>y.Y).Max();
        bounds = new Rect(new Vector2(0), new Vector2(maxX, maxY));
    }

    protected internal override void ProcessGeometryCore()
    {
        OutlineMesh.SetPoints(Points);
    }
}