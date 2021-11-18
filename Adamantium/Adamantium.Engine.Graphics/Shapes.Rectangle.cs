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
            private const float rectangleSector = 90;

            public static Shape New(
                GraphicsDevice device,
                GeometryType type,
                float width,
                float height,
                CornerRadius corners,
                int tessellation = 20,
                Matrix4x4F? transform = null
            )
            {
                var geometry = GenerateGeometry(
                    type,
                    width,
                    height,
                    corners,
                    tessellation,
                    transform);
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType type,
                float width,
                float height,
                CornerRadius corners,
                int tessellation = 20,
                Matrix4x4F? transform = null)
            {
                var primitiveType = PrimitiveType.TriangleList;
                if (type == GeometryType.Outlined)
                {
                    primitiveType = PrimitiveType.LineStrip;
                }

                var min = Math.Min(width, height);
                ValidateCorners(ref corners, min);

                var vertices = new List<Vector2D>();

                var halfWidth = width / 2;
                var halfHeight = height / 2;

                if (corners.TopLeft > 0)
                {
                    var radius = new Vector2D(corners.TopLeft);
                    var centerX = -halfWidth + radius.X;
                    var centerY = -halfHeight + radius.Y;
                    GenerateRoundCorner(tessellation, -180, new Vector2D(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector2D(-halfWidth, -halfHeight));
                }

                if (corners.TopRight > 0)
                {
                    var radius = new Vector2D(corners.TopRight);
                    var centerX = halfWidth - radius.X;
                    var centerY = -halfHeight + radius.Y;
                    GenerateRoundCorner(tessellation, 90, new Vector2D(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector2D(halfWidth, -halfHeight));
                }

                if (corners.BottomRight > 0)
                {
                    var radius = new Vector2D(corners.BottomRight);
                    var centerX = halfWidth - radius.X;
                    var centerY = halfHeight - radius.Y;
                    GenerateRoundCorner(tessellation, 0, new Vector2D(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector2D(halfWidth, halfHeight));
                }

                if (corners.BottomLeft > 0)
                {
                    var radius = new Vector2D(corners.BottomLeft);
                    var centerX = -halfWidth + radius.X;
                    var centerY = halfHeight - radius.Y;
                    GenerateRoundCorner(tessellation, -90, new Vector2D(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector2D(-halfWidth, halfHeight));
                }

                Mesh mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                if (type == GeometryType.Solid)
                {
                    var polygon = new Mathematics.Polygon();
                    polygon.Polygons.Add(new PolygonItem(vertices));
                    var points = polygon.Fill();
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

            private static void ValidateCorners(ref CornerRadius corners, float side)
            {
                if (corners.TopLeft < 0)
                {
                    corners.TopLeft = 0;
                }
                if (corners.TopRight < 0)
                {
                    corners.TopRight = 0;
                }
                if (corners.BottomRight < 0)
                {
                    corners.BottomRight = 0;
                }
                if (corners.BottomLeft < 0)
                {
                    corners.BottomLeft = 0;
                }

                var halfSize = side / 2;

                if (corners.TopLeft > halfSize)
                {
                    corners.TopLeft = halfSize;
                }
                
                if (corners.TopRight > halfSize)
                {
                    corners.TopRight = halfSize;
                }
                
                if (corners.BottomRight > halfSize)
                {
                    corners.BottomRight = halfSize;
                }
                
                if (corners.BottomLeft > halfSize)
                {
                    corners.BottomLeft = halfSize;
                }
            }

            private static void GenerateRoundCorner(
                int tessellation,
                float startAngle,
                Vector2D center,
                Vector2D radius,
                List<Vector2D> vertices)
            {
                var angleItem = -MathHelper.DegreesToRadians(rectangleSector / tessellation);
                var angle = MathHelper.DegreesToRadians(startAngle);
                for (int i = 0; i <= tessellation; ++i)
                {
                    var x = center.X + (radius.X * (float) Math.Cos(angle));
                    var y = center.Y - (radius.Y * (float) Math.Sin(angle));
                    angle += angleItem;
                    x = Math.Round(x, 3);
                    y = Math.Round(y, 3);
                    vertices.Add(new Vector2D(x, y));
                }
            }
        }
    }
}
