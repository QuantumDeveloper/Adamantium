using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class DirectionalLightVisualTemplate : LightVisualTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            var ellipse = Shapes.Ellipse.GenerateGeometry(GeometryType.Outlined, EllipseType.EdgeToEdge, new Vector2F(2), 0, 360, true, 40);

            var indices = new List<int>();
            List<Vector3F> directions = new List<Vector3F>();
            int lastIndex = 0;
            for (int i = 0; i < ellipse.Points.Length - 1; i++)
            {
                if (i % 2 != 0)
                    continue;

                var lineStart = ellipse.Points[i];
                var lineEnd = lineStart + Vector3F.ForwardRH * 3;
                directions.Add(lineStart);
                directions.Add(lineEnd);
                indices.Add(lastIndex++);
                indices.Add(lastIndex++);
                indices.Add(-1);
            }
            var directionsMesh = new Mesh();
            directionsMesh.SetPoints(directions);
            directionsMesh.SetIndices(indices);
            MergeInstance instance = new MergeInstance(directionsMesh, Matrix4x4F.Identity, false);
            ellipse.Merge(new[] { instance });

            return BuildSubEntity(owner, name, Colors.Yellow, ellipse, BoundingVolume.OrientedBox);
        }

    }
}
