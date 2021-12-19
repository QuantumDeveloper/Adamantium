using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media
{
    public class PolygonGeometry : Geometry
    {
        private Rect bounds;
        
        public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
            typeof(FillRule), typeof(PolygonGeometry),
            new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));
        
        public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(FillRule),
            typeof(PointsCollection), typeof(PolygonGeometry),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

        public FillRule FillRule
        {
            get => GetValue<FillRule>(FillRuleProperty);
            set => SetValue(FillRuleProperty, value);
        }

        public PointsCollection Points
        {
            get => GetValue<PointsCollection>(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public override Rect Bounds => bounds;
        
        public override Geometry Clone()
        {
            throw new System.NotImplementedException();
        }

        public override void RecalculateBounds()
        {
            if (Points.Count == 0)
            {
                bounds = Rect.Empty;
                return;
            }
            
            var maxX = Points.Select(x=>x.X).Max();
            var maxY = Points.Select(y=>y.Y).Max();
            var minX = Points.Select(x => x.X).Min();
            var minY = Points.Select(y => y.Y).Min();
            bounds = new Rect(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        protected internal override void ProcessGeometry()
        {
            GenerateGeometry(Points);
        }

        public PolygonGeometry()
        {
            
        }

        public PolygonGeometry(IEnumerable<Vector2> points, FillRule fillRule)
        {
            FillRule = fillRule;
            Points = new PointsCollection(points);
        }

        internal void GenerateGeometry(IEnumerable<Vector2> points)
        {
            var geometryPoints = points as Vector2[] ?? points.ToArray();
            var polygonItem = new PolygonItem(geometryPoints);
            var polygon = new Polygon();
            polygon.AddItem(polygonItem);
            polygon.FillRule = FillRule;
            var result = polygon.Fill();
            
            Mesh = new Mesh();
            Mesh.SetPoints(result).GenerateBasicIndices().Optimize();
            var strokePoints = new List<Vector3F>();
            if (FillRule == FillRule.NonZero)
            {
                polygonItem = new PolygonItem(geometryPoints);
                polygonItem.SplitOnSegments();
                polygonItem.CheckForSelfIntersection(FillRule.NonZero);
                var orderedSegments = PolygonHelper.OrderSegments(polygonItem.Segments);

                for (int i = 0; i < orderedSegments.Count; ++i)
                {
                    strokePoints.Add((Vector3F)orderedSegments[i].Start);
                }
            }
            else
            {
                foreach (var point in geometryPoints)
                {
                    strokePoints.Add((Vector3F)point);
                }
            }
            
            OutlineMesh.SetPoints(strokePoints);
        }
    }
}