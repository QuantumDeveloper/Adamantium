using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Engine.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media;

public class PathGeometry : Geometry
{
    private readonly Dictionary<PathFigure, PolygonItem> figureToPolygon;
    private Dictionary<PathFigure, Vector2[]> outlines;
    private Rect bounds;
    private Pen pen;
    
    public PathGeometry(Pen pen)
    {
        figureToPolygon = new Dictionary<PathFigure, PolygonItem>();
        outlines = new Dictionary<PathFigure, Vector2[]>();
        this.pen = pen;
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

    private void FiguresOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseComponentUpdated();
    }

    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }
    
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

    protected internal override void ProcessGeometry()
    {
        figureToPolygon.Clear();
        OutlineMesh.ClearLayers();
        
        foreach (var figure in Figures)
        {
            figure.ProcessSegments();
            var polygonItem = new PolygonItem(figure.Points);
            figureToPolygon[figure] = polygonItem;
            var outlinePoints = figure.Points.ToArray();
            if (figure.IsClosed)
            {
                outlines[figure] = outlinePoints;
            }
            else
            {
                outlines[figure] = outlinePoints[0..^1];
            }
        }
        
        var polygon = new Polygon();
        foreach (var polygonItem in figureToPolygon)
        {
            polygon.AddItem(polygonItem.Value);
        }

        polygon.FillRule = FillRule;
        Mesh.SetPoints(polygon.Fill());

        foreach (var outline in outlines)
        {
            OutlineMesh.AddLayer(outline.Value);
        }
    }
}