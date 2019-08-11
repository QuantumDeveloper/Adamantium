using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Rectangle
        {
            private const float rectSector = 90;

            public static Mesh GenerateGeometry(
                GeometryType type,
                float width,
                float height,
                float radiusX = 0,
                float radiusY = 0,
                int tessellation = 20,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var primitiveType = PrimitiveType.TriangleList;
                if (type == GeometryType.Outlined)
                {
                    primitiveType = PrimitiveType.LineStrip;
                }

                if (radiusX < 0)
                {
                    radiusX = 0;
                }
                if (radiusY < 0)
                {
                    radiusY = 0;
                }

                if (radiusX > width / 2)
                {
                    radiusX = width / 2;
                }

                if (radiusY > height / 2)
                {
                    radiusY = height / 2;
                }

                List<Vector3F> vertices = new List<Vector3F>();

                var halfWidth = width / 2;
                var halfHeight = height / 2;

                if (radiusX > 0 && radiusY > 0)
                {
                    Vector2F radius = new Vector2F(radiusX, radiusY);
                    var centerX = -halfWidth + radiusX;
                    var centerY = halfHeight - radiusY;
                    GenerateRoundCorner(tessellation, -180, new Vector2F(centerX, centerY), radius, vertices);

                    centerX = halfWidth - radiusX;
                    centerY = halfHeight - radiusY;
                    GenerateRoundCorner(tessellation, -270, new Vector2F(centerX, centerY), radius, vertices);

                    centerY = -halfHeight + radiusY;
                    GenerateRoundCorner(tessellation, 0, new Vector2F(centerX, centerY), radius, vertices);

                    centerX = -halfWidth + radiusX;
                    centerY = -halfHeight + radiusY;
                    GenerateRoundCorner(tessellation, -90, new Vector2F(centerX, centerY), radius, vertices);

                    if (type == GeometryType.Outlined)
                    {
                        vertices.Add(vertices[0]);
                    }
                }
                else
                {
                    vertices.Add(new Vector3F(-halfWidth, -halfHeight));
                    vertices.Add(new Vector3F(-halfWidth, halfHeight));
                    vertices.Add(new Vector3F(halfWidth, halfHeight));
                    vertices.Add(new Vector3F(halfWidth, -halfHeight));
                    if (type == GeometryType.Outlined)
                    {
                        vertices.Add(new Vector3F(-halfWidth, -halfHeight));
                    }
                }

                Mathematics.Polygon polygon = new Mathematics.Polygon();
                polygon.Polygons.Add(new PolygonItem(vertices));
                var points = polygon.Fill();

                Mesh mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                if (type == GeometryType.Solid)
                {
                    mesh.SetPositions(points);
                }
                else
                {
                    mesh.SetPositions(vertices);
                }
                mesh.GenerateBasicIndices();
                mesh.Optimize();
                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            private static void GenerateRoundCorner(
                int tessellation,
                float startAngle,
                Vector2F center,
                Vector2F radius,
                List<Vector3F> vertices)
            {
                var angleItem = -MathHelper.DegreesToRadians(rectSector / tessellation);
                var angle = MathHelper.DegreesToRadians(startAngle);
                for (int i = 0; i < tessellation; ++i)
                {
                    var x = center.X + (radius.X * (float) Math.Cos(angle));
                    var y = center.Y + (radius.Y * (float) Math.Sin(angle));
                    angle += angleItem;
                    vertices.Add(new Vector3F(x, y, 0));
                }
            }
        }
    }
}
