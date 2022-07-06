using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media;

public class PathGeometry : Geometry
{
    private readonly Dictionary<PathFigure, MeshContour> figureToPolygon;
    private Dictionary<PathFigure, Vector2[]> outlines;
    private Rect bounds;
    
    public PathGeometry()
    {
        figureToPolygon = new Dictionary<PathFigure, MeshContour>();
        outlines = new Dictionary<PathFigure, Vector2[]>();
        Figures = new PathFigureCollection();
    }

    public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
        typeof(FillRule), typeof(PathGeometry),
        new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty FiguresProperty =
        AdamantiumProperty.Register(nameof(Figures), typeof(PathFigureCollection), typeof(PathGeometry),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, FiguresChangedCallback));
    
    private static void FiguresChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is PathGeometry pathGeometry)
        {
            if (e.OldValue is PathFigureCollection collection1) collection1.CollectionChanged -= pathGeometry.FiguresOnCollectionChanged;
            
            if (e.NewValue is PathFigureCollection collection2) collection2.CollectionChanged += pathGeometry.FiguresOnCollectionChanged;
        }
    }

    private void FiguresOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
    }

    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }
    
    [Content]
    public PathFigureCollection Figures
    {
        get => GetValue<PathFigureCollection>(FiguresProperty);
        set => SetValue(FiguresProperty, value);
    }

    public override Rect Bounds => bounds;
    
    public override Geometry Clone()
    {
        throw new System.NotImplementedException();
    }

    public override void RecalculateBounds()
    {
        if (Figures == null) return;
        
        var points = new List<Vector2>();
        foreach (var figure in Figures)
        {
            figure.ProcessSegments();
            points.AddRange(figure.Points);
        }
        
        if (points.Count == 0) return;
        
        var maxX = points.Select(x=>x.X).Max();
        var maxY = points.Select(y=>y.Y).Max();
        var minX = points.Select(x => x.X).Min();
        var minY = points.Select(y => y.Y).Min();
        bounds = new Rect(new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    protected internal override void ProcessGeometryCore(GeometryType geometryType)
    {
        figureToPolygon.Clear();
        Mesh.ClearContours();
        
        foreach (var figure in Figures)
        {
            figure.ProcessSegments();
            var meshContour = new MeshContour(figure.Points);
            figureToPolygon[figure] = meshContour;
            var outlinePoints = figure.Points.ToArray();
            outlines[figure] = outlinePoints;
        }
        
        var polygon = new Polygon();
        foreach (var polygonItem in figureToPolygon)
        {
            if (!polygonItem.Key.IsFilled) continue;

            polygon.AddContour(polygonItem.Value);
        }

        polygon.FillRule = FillRule;
        var points = polygon.FillIndirect(geometryType != GeometryType.Outlined);

        foreach (var processedContour in polygon.ProcessedContours)
        {
            Mesh.AddContour(processedContour);
        }

        if (geometryType != GeometryType.Outlined) Mesh.SetPoints(points);
        
        foreach (var outline in outlines)
        {
            Mesh.AddContour(outline.Value, outline.Key.IsClosed);
        }
    }
}