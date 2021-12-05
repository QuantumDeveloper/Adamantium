using System.Collections.Generic;
using Adamantium.Core.Collections;
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
        
        public TrackingCollection<Vector2> Points { get; set; }

        public override Rect Bounds { get; }
        public override Geometry Clone()
        {
            throw new System.NotImplementedException();
        }

        protected internal override void ProcessGeometry()
        {
            GenerateGeometry(Points);
        }

        public PolygonGeometry(IEnumerable<Vector2> points, FillRule fillRule)
        {
            FillRule = fillRule;
            Points = new TrackingCollection<Vector2>(points);
        }

        internal void GenerateGeometry(IEnumerable<Vector2> points)
        {
            var polygonItem = new PolygonItem(points);
            var polygon = new Polygon();
            polygon.AddItem(polygonItem);
            polygon.FillRule = FillRule;
            var result = polygon.Fill();
            Mesh = new Mesh();
            Mesh.SetPoints(result).GenerateBasicIndices().Optimize();
            StrokeMesh = new Mesh();
            var lst = new List<Vector3F>();
            foreach (var point in points)
            {
                lst.Add((Vector3F)point);
            }

            StrokeMesh.SetPoints(lst);
        }
    }
}