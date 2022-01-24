using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public sealed class PathFigure : AdamantiumComponent
{
    public static readonly AdamantiumProperty IsClosedProperty = AdamantiumProperty.Register(nameof(IsClosed),
        typeof(bool), typeof(PathFigure), new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsFilledProperty = AdamantiumProperty.Register(nameof(IsFilled),
        typeof(bool), typeof(PathFigure), new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty StartPointProperty = AdamantiumProperty.Register(nameof(StartPoint),
        typeof(Vector2), typeof(PathFigure),
        new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty SegmentsProperty = AdamantiumProperty.Register(nameof(Segments),
        typeof(PathSegmentCollection), typeof(PathFigure),
        new PropertyMetadata(null,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsParentArrange, SegmentsChangedCallback));
    
    internal List<Vector2> Points { get; }

    public PathFigure()
    {
        Points = new List<Vector2>();
    }

    private static void SegmentsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not PathFigure figure) return;
        
        if (e.OldValue is PathSegmentCollection collection1) collection1.CollectionChanged -= figure.OnCollectionChanged;

        if (e.NewValue is PathSegmentCollection collection2) collection2.CollectionChanged += figure.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
        isSegmentProcessed = false;
    }

    private bool isSegmentProcessed;

    public void ProcessSegments()
    {
        if (isSegmentProcessed) return;
        
        Points.Clear();
        var currentPoint = StartPoint;
        Points.Add(currentPoint);
        foreach (var segment in Segments)
        {
            var pts = segment.ProcessSegment(currentPoint);
            if (pts.Length > 0)
            {
                currentPoint = pts[^1];
                Points.AddRange(pts);
            }
        }

        isSegmentProcessed = true;
    }

    public bool IsClosed
    {
        get => GetValue<bool>(IsClosedProperty);
        set => SetValue(IsClosedProperty, value);
    }
    
    public bool IsFilled
    {
        get => GetValue<bool>(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    public Vector2 StartPoint
    {
        get => GetValue<Vector2>(StartPointProperty);
        set => SetValue(StartPointProperty, value);
    }

    public PathSegmentCollection Segments
    {
        get => GetValue<PathSegmentCollection>(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        isSegmentProcessed = false;
    }
}