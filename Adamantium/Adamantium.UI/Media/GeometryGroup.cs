using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media;

public class GeometryGroup : Geometry
{
    private Rect bounds;
    
    public static readonly AdamantiumProperty ChildrenProperty =
        AdamantiumProperty.Register(nameof(Children), typeof(GeometryCollection), typeof(GeometryGroup),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, PropertyChangedCallback));
    
    public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
        typeof(FillRule), typeof(GeometryGroup),
        new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

    private static void PropertyChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is GeometryGroup group)
        {
            if (e.OldValue is GeometryCollection oldGroup)
            {
                oldGroup.CollectionChanged -= group.ChildrenOnCollectionChanged;
            }

            if (e.NewValue is GeometryCollection newCollection)
            {
                newCollection.CollectionChanged += group.ChildrenOnCollectionChanged;
            }
        }
    }

    public GeometryGroup()
    {
        
    }

    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }

    private void ChildrenOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateGeometry();
    }

    [Content]
    public GeometryCollection Children
    {
        get => GetValue<GeometryCollection>(ChildrenProperty);
        set => SetValue(ChildrenProperty, value);
    }

    public override Rect Bounds => bounds;
    
    public override Geometry Clone()
    {
        throw new System.NotImplementedException();
    }

    public override void RecalculateBounds()
    {
        var points = new List<Vector2>();
        
        foreach (var child in Children)
        {
            if (child is CombinedGeometry { IsProcessed: false } combined)
            {
                combined.RecalculateBounds();
            }
            
            foreach (var meshContour in child.Mesh.Contours)
            {
                points.AddRange(meshContour.Points);
            }
        }
        
        bounds = Rect.FromPoints(points);
    }

    protected internal override void ProcessGeometryCore()
    {
        if (Children.Count == 0) return;

        var polygon = new Polygon(FillRule);
        foreach (var child in Children)
        {
            if (child is CombinedGeometry { IsProcessed: false } combined)
            {
                combined.ProcessGeometry();
            }
            
            foreach (var meshContour in child.Mesh.Contours)
            {
                polygon.AddItem(meshContour);
                Mesh.AddContour(meshContour.Points, meshContour.IsGeometryClosed);
            }
        }

        var points = polygon.Fill();
        Mesh.SetPoints(points);
    }
}