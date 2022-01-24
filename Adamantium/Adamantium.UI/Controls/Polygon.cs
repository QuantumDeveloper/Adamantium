using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public class Polygon : Shape
{
    public Polygon()
    {
        geometry = new StreamGeometry();
    }
        
    private StreamGeometry geometry { get; set; }
        
    public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
        typeof(PointsCollection), typeof(Polygon),
        new PropertyMetadata(null,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender, PointsChangedCallback));

    public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
        typeof(FillRule), typeof(Polygon),
        new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

    private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Polygon polygon && e.NewValue != null)
        {
            if (e.OldValue is TrackingCollection<Vector2> oldCollection)
            {
                polygon.UnsubscribeFromPointEvents(oldCollection);
            }

            polygon.SubscribeToPointEvents();
            polygon.InvalidateMeasure();
        }
    }

    private void UnsubscribeFromPointEvents(TrackingCollection<Vector2> collection)
    {
        collection.CollectionChanged -= PointsOnCollectionChanged;
    }

    private void SubscribeToPointEvents()
    {
        Points.CollectionChanged += PointsOnCollectionChanged;
    }

    private  void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
    }

    public PointsCollection Points
    {
        get => GetValue<PointsCollection>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }
        
    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Rect = Rect.FromPoints(Points);
        return base.MeasureOverride(availableSize);
    }

    protected override void OnRender(DrawingContext context)
    {
        if (Points == null || Points.Count < 2) return;
        
        base.OnRender(context);

        var streamContext = geometry.Open();
        streamContext.BeginFigure(Points[0], true, true).PolylineLineTo(Points.Skip(1), true);
        
        context.BeginDraw(this);
        geometry.FillRule = FillRule;
        context.DrawGeometry(Fill, geometry, GetPen());
        context.EndDraw(this);
    }
}