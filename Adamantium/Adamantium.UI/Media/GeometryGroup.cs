using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.Engine.Graphics;
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
        Children = new GeometryCollection();
    }

    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }

    private void ChildrenOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
            
            child.ProcessGeometry(GeometryType.Both);
            foreach (var meshContour in child.Mesh.Contours)
            {
                points.AddRange(meshContour.Points);
            }
        }
        
        bounds = Rect.FromPoints(points);
    }

    protected internal override void ProcessGeometryCore(GeometryType geometryType)
    {
        if (Children.Count == 0) return;

        var polygon = new Polygon(FillRule);

        if (FillRule == FillRule.EvenOdd)
        {
            foreach (var child in Children)
            {
                child.ProcessGeometry(GeometryType.Outlined);
            
                foreach (var meshContour in child.Mesh.Contours)
                {
                    if (meshContour.IsGeometryClosed) polygon.AddContour(meshContour);
                    Mesh.AddContour(meshContour.Points, meshContour.IsGeometryClosed);
                }
            }
        }
        else
        {
            foreach (var child in Children)
            {
                child.ProcessGeometry(GeometryType.Outlined);

                var newContainer = true;
            
                foreach (var meshContour in child.Mesh.Contours)
                {
                    if (meshContour.IsGeometryClosed)
                    {
                        polygon.AddContour(meshContour, newContainer);
                        newContainer = false;
                    }
                    Mesh.AddContour(meshContour.Points, meshContour.IsGeometryClosed);
                }
            }
        }

        var points = polygon.FillIndirect();
        Mesh.SetPoints(points);
    }
}