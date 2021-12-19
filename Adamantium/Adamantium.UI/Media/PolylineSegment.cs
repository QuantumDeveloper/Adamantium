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
            if (segment.Points == null) return;
            
            segment.Points.CollectionChanged += segment.PointsOnCollectionChanged;
            segment.RaiseComponentUpdated();
        }
    }

    private void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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