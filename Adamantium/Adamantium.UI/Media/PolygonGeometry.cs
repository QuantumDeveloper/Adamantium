using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
    public class PolygonGeometry : Geometry
    {
        public override Rect Bounds { get; }
        public override Geometry Clone()
        {
            throw new System.NotImplementedException();
        }

        public PolygonGeometry(IEnumerable<Vector2D> points, FillRule fillRule)
        {
            GenerateGeometry(points, fillRule);
        }

        internal void GenerateGeometry(IEnumerable<Vector2D> points, FillRule fillRule)
        {
            var polygonItem = new PolygonItem(points);
            var polygon = new Polygon();
            polygon.Polygons.Add(polygonItem);
            polygon.FillRule = fillRule;
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