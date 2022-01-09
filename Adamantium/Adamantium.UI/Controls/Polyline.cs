using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public class Polyline : Shape
{
    protected SplineGeometry SplineGeometry { get; }
        
    public Polyline()
    {
        SplineGeometry = new SplineGeometry();
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

    protected virtual void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
        Rect = new Rect(new Vector2(0), new Vector2(maxX, maxY));
        return base.MeasureOverride(availableSize);
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        SplineGeometry.Points = new PointsCollection(Points);
        context.BeginDraw(this);
        context.DrawGeometry(Stroke, SplineGeometry, GetPen());
        context.EndDraw(this);
    }
}