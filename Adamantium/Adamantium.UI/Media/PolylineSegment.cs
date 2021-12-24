using System.Collections.Specialized;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public class PolylineSegment : PathSegment
{
    public static readonly AdamantiumProperty PointsProperty =
        AdamantiumProperty.Register(nameof(Points), typeof(PointsCollection), typeof(PolylineSegment),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, PointsChangedCallback));

    private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is PolylineSegment segment)
        {
            if (e.OldValue is PointsCollection collection1) collection1.CollectionChanged -= segment.PointsOnCollectionChanged;
            
            if (e.NewValue is PointsCollection collection2) collection2.CollectionChanged += segment.PointsOnCollectionChanged;
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

    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        return Points.ToArray();
    }
}