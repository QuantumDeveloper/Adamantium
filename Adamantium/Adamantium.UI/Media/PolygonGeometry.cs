using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
    public class PolygonGeometry : Geometry
    {
        public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
            typeof(FillRule), typeof(PolygonGeometry),
            new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

        public FillRule FillRule
        {
            get => GetValue<FillRule>(FillRuleProperty);
            set => SetValue(FillRuleProperty, value);
        }
        
        public override Rect Bounds { get; }
        public override Geometry Clone()
        {
            throw new System.NotImplementedException();
        }

        public PolygonGeometry(IEnumerable<Vector2D> points, FillRule fillRule)
        {
            FillRule = fillRule;
            GenerateGeometry(points);
        }

        internal void GenerateGeometry(IEnumerable<Vector2D> points)
        {
            var polygonItem = new PolygonItem(points);
            var polygon = new Polygon();
            polygon.AddItem(polygonItem);
            polygon.FillRule = FillRule;
            var result = polygon.Fill();
            Mesh = new Mesh();
            Mesh.SetPositions(result).GenerateBasicIndices().Optimize();
            StrokeMesh = new Mesh();
            var lst = new List<Vector3F>();
            foreach (var point in points)
            {
                lst.Add((Vector3F)point);
            }

            StrokeMesh.SetPositions(lst);
        }
    }
}