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

            public static Shape New(
                GraphicsDevice device,
                GeometryType type,
                float width,
                float height,
                float radiusX = 0,
                float radiusY = 0,
                int tessellation = 20,
                Matrix4x4F? transform = null
            )
            {
                var geometry = GenerateGeometry(
                    type,
                    width,
                    height,
                    radiusX,
                    radiusY,
                    tessellation,
                    transform);
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType type,
                float width,
                float height,
                float radiusX = 0,
                float radiusY = 0,
                int tessellation = 20,
                Matrix4x4F? transform = null)
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

                var vertices = new List<Vector2D>();

                var halfWidth = width / 2;
                var halfHeight = height / 2;

                if (radiusX > 0 && radiusY > 0)
                {
                    var radius = new Vector2D(radiusX, radiusY);
                    var centerX = -halfWidth + radiusX;
                    var centerY = halfHeight - radiusY;
                    GenerateRoundCorner(tessellation, -180, new Vector2D(centerX, centerY), radius, vertices);

                    centerX = halfWidth - radiusX;
                    centerY = halfHeight - radiusY;
                    GenerateRoundCorner(tessellation, -270, new Vector2D(centerX, centerY), radius, vertices);

                    centerY = -halfHeight + radiusY;
                    GenerateRoundCorner(tessellation, 0, new Vector2D(centerX, centerY), radius, vertices);

                    centerX = -halfWidth + radiusX;
                    centerY = -halfHeight + radiusY;
                    GenerateRoundCorner(tessellation, -90, new Vector2D(centerX, centerY), radius, vertices);

                    if (type == GeometryType.Outlined)
                    {
                        vertices.Add(vertices[0]);
                    }
                }
                else
                {
                    vertices.Add(new Vector2D(-halfWidth, -halfHeight));
                    vertices.Add(new Vector2D(halfWidth, -halfHeight));
                    vertices.Add(new Vector2D(halfWidth, halfHeight));
                    vertices.Add(new Vector2D(-halfWidth, halfHeight));
                    if (type == GeometryType.Outlined)
                    {
                        vertices.Add(new Vector2D(-halfWidth, -halfHeight));
                    }
                }

                var polygon = new Mathematics.Polygon();
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
                mesh.GenerateBasicIndices().Optimize();

                var uvs = new List<Vector2F>();
                foreach (var vertex in mesh.Positions)
                {
                    var u = vertex.X / width;
                    var v = vertex.Y / height;
                    uvs.Add(new Vector2F(u, v));
                }
                
                mesh.SetUVs(0, uvs).ApplyTransform(transform);

                return mesh;
            }

            private static void GenerateRoundCorner(
                int tessellation,
                float startAngle,
                Vector2D center,
                Vector2D radius,
                List<Vector2D> vertices)
            {
                var angleItem = -MathHelper.DegreesToRadians(rectSector / tessellation);
                var angle = MathHelper.DegreesToRadians(startAngle);
                for (int i = 0; i < tessellation; ++i)
                {
                    var x = center.X + (radius.X * (float) Math.Cos(angle));
                    var y = center.Y + (radius.Y * (float) Math.Sin(angle));
                    angle += angleItem;
                    vertices.Add(new Vector2D(x, y));
                }
            }
        }
    }
}
