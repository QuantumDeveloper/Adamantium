using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Ellipse
        {
            public static Mesh GenerateGeometry(
               GeometryType geometryType,
               EllipseType ellipseType,
               Vector2F diameter,
               float startAngle = 0,
               float stopAngle = 360,
               bool isClockWise = true,
               int tessellation = 36,
               Matrix4x4F? transform = null,
               bool toRightHanded = false)
            {
                if (Math.Abs(stopAngle - startAngle) > 360)
                {
                    stopAngle = startAngle + 360;
                }

                Mesh mesh;

                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;

            }

            private static Mesh GenerateSolidGeometry(
                EllipseType ellipseType,
                Vector2F diameter,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                bool toRightHanded = false)
            {
                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                List<Vector2F> uvs = new List<Vector2F>();
                Vector3F center = Vector3F.Zero;
                float radiusX = diameter.X / 2;
                float radiusY = diameter.Y / 2;

                float range = stopAngle - startAngle;
                float angle = range / (tessellation - 1);

                float sign = 1;
                if (isClockWise)
                {
                    sign = -1;
                }

                float angleItem = MathHelper.DegreesToRadians(angle) * sign;
                startAngle = MathHelper.DegreesToRadians(startAngle);
                angle = startAngle * sign;

               if (ellipseType == EllipseType.Sector)
               {
                   float x1 = center.X + (radiusX * (float)Math.Cos(startAngle));
                   float y1 = center.Y + (radiusY * (float)Math.Sin(startAngle));
                    for (int i = 0; i < tessellation; ++i)
                    {
                        float x2 = center.X + (radiusX * (float)Math.Cos(angle));
                        float y2 = center.Y + (radiusY * (float)Math.Sin(angle));
                        
                        var vertex1 = new Vector3F(x1, y1, 0);
                        var vertex2 = new Vector3F(x2, y2, 0);

                        vertices.Add(Vector3F.Zero);
                        vertices.Add(vertex1);
                        vertices.Add(vertex2);

                        var uv1 = new Vector2D(0.5f + (x1 - center.X) / diameter.X, 0.5f - (y1 - center.Y) / diameter.Y);
                        var uv2 = new Vector2D(0.5f + (x2 - center.X) / diameter.X, 0.5f - (y2 - center.Y) / diameter.Y);

                        uvs.Add(new Vector2F(0.5f, 0.5f));
                        uvs.Add(uv1);
                        uvs.Add(uv2);

                        angle += angleItem;

                        x1 = x2;
                        y1 = y2;
                    }
                }
                else
                {
                    for (int i = 0; i <= tessellation; ++i)
                    {
                        float x = center.X + (radiusX * (float)Math.Cos(angle));
                        float y = center.Y + (radiusY * (float)Math.Sin(angle));
                        
                        var vertex = new Vector3F(x, y, 0);
                        vertices.Add(vertex);

                        var uv = new Vector2F(
                            0.5f - (center.X - x) / (2 * diameter.X),
                            0.5f - (center.Y - y) / (2 * diameter.Y));
                        uvs.Add(uv);

                        angle += angleItem;
                    }

                    int basicIndex = 0;

                    for (int i = 0; i < tessellation - 2; i++)
                    {
                        indices.Add(basicIndex);
                        indices.Add(i + 1);
                        indices.Add(i + 2);
                    }
                }

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.TriangleList;
                mesh.SetPositions(vertices);
                mesh.SetUVs(0, uvs);
                if (indices.Count > 0)
                {
                    mesh.SetIndices(indices);
                }
                else
                {
                    mesh.Optimize();
                }

                if (toRightHanded)
                {
                    mesh.ReverseWinding();
                }

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                EllipseType ellipseType,
                Vector2F diameter,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36)
            {
                List<Vector3F> vertices = new List<Vector3F>();
                Vector3F center = Vector3F.Zero;
                float radiusX = diameter.X / 2;
                float radiusY = diameter.Y / 2;

                float range = stopAngle - startAngle;
                float angle = range / tessellation;

                float sign = 1;
                if (isClockWise)
                {
                    sign = -1;
                }

                float angleItem = MathHelper.DegreesToRadians(angle) * sign;
                startAngle = MathHelper.DegreesToRadians(startAngle);
                angle = startAngle * sign;

                if (range < 360 && ellipseType == EllipseType.Sector)
                {
                    vertices.Add(center);
                }
                for (int i = 0; i <= tessellation; ++i)
                {
                    float x = center.X + (radiusX * (float)Math.Cos(angle));
                    float y = center.Y + (radiusY * (float)Math.Sin(angle));

                    var vertex = new Vector3F(x, y, 0);
                    vertices.Add(vertex);
                    angle += angleItem;
                }

                if (range < 360 && ellipseType == EllipseType.Sector)
                {
                    vertices.Add(center);
                }

                Mesh mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.LineStrip;
                mesh.SetPositions(vertices);
                mesh.GenerateBasicIndices();
                return mesh;
            }

            public static Shape New(
                GraphicsDevice graphicsDevice,
                GeometryType geometryType,
                EllipseType ellipseType,
                Vector2F diameter,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var mesh = GenerateGeometry(geometryType, ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation, transform, toRightHanded);
                return new Shape(graphicsDevice, mesh);
            }
        }
    }
}
