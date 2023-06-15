using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Triangulation;

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
                Matrix4x4? transform = null
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
                double width,
                double height,
                CornerRadius corners,
                int tessellation = 20,
                Matrix4x4? transform = null)
            {
                var primitiveType = PrimitiveType.TriangleList;
                if (type == GeometryType.Outlined)
                {
                    primitiveType = PrimitiveType.LineStrip;
                }

                var min = Math.Min(width, height);
                ValidateCorners(ref corners, min);

                var vertices = new List<Vector3>();

                var halfWidth = width / 2;
                var halfHeight = height / 2;

                if (corners.TopLeft > 0)
                {
                    var radius = new Vector2(corners.TopLeft);
                    var centerX = -halfWidth + radius.X;
                    var centerY = -halfHeight + radius.Y;
                    GenerateRoundCorner(tessellation, -180, new Vector2(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector3(-halfWidth, -halfHeight));
                }

                if (corners.TopRight > 0)
                {
                    var radius = new Vector2(corners.TopRight);
                    var centerX = halfWidth - radius.X;
                    var centerY = -halfHeight + radius.Y;
                    GenerateRoundCorner(tessellation, 90, new Vector2(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector3(halfWidth, -halfHeight));
                }

                if (corners.BottomRight > 0)
                {
                    var radius = new Vector2(corners.BottomRight);
                    var centerX = halfWidth - radius.X;
                    var centerY = halfHeight - radius.Y;
                    GenerateRoundCorner(tessellation, 0, new Vector2(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector3(halfWidth, halfHeight));
                }

                if (corners.BottomLeft > 0)
                {
                    var radius = new Vector2(corners.BottomLeft);
                    var centerX = -halfWidth + radius.X;
                    var centerY = halfHeight - radius.Y;
                    GenerateRoundCorner(tessellation, -90, new Vector2(centerX, centerY), radius, vertices);
                }
                else
                {
                    vertices.Add(new Vector3(-halfWidth, halfHeight));
                }

                if (transform is { IsIdentity: false })
                {
                    vertices = Mesh.ApplyTransform(vertices, transform.Value).ToList();
                }

                Mesh mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                switch (type)
                {
                    case GeometryType.Solid:
                    {
                        var points = Triangulate(vertices);
                        mesh.SetPoints(points);
                        break;
                    }
                    case GeometryType.Both:
                    {
                        mesh.AddContour(vertices, true);
                        var points = Triangulate(vertices);
                        mesh.SetPoints(points);
                        break;
                    }
                    case GeometryType.Outlined:
                    {
                        mesh.AddContour(vertices, true);
                        break;
                    }
                }
                mesh.GenerateBasicIndices();

                var uvs = new List<Vector2F>();
                foreach (var vertex in mesh.Points)
                {
                    var u = (float)(vertex.X / width);
                    var v = (float)(vertex.Y / height);
                    uvs.Add(new Vector2F(u, v));
                }
                
                mesh.SetUVs(0, uvs);

                return mesh;
            }

            private static void GenerateRoundCorner(
                int tessellation,
                float startAngle,
                Vector2 center,
                Vector2 radius,
                List<Vector3> vertices)
            {
                var angleItem = -MathHelper.DegreesToRadians(rectangleSector / tessellation);
                var angle = MathHelper.DegreesToRadians(startAngle);
                for (int i = 0; i <= tessellation; ++i)
                {
                    var x = center.X + (radius.X * Math.Cos(angle));
                    var y = center.Y - (radius.Y * Math.Sin(angle));
                    angle += angleItem;
                    vertices.Add( (Vector3)Vector2.Round(x, y, 3));
                }
            }
            
            private static void ValidateCorners(ref CornerRadius corners, double side)
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

            private static List<Vector3> Triangulate(List<Vector3> vertices)
            {
                var polygon = new Mathematics.Triangulation.Polygon();
                polygon.AddContour(new MeshContour(vertices));
                var points = polygon.FillIndirect();
                return points;
            }
        }
    }
}
