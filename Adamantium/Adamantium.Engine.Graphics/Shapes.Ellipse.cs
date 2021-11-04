using System;
using System.Collections.Generic;
using System.Diagnostics;
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
               Matrix4x4F? transform = null)
            {
                if (Math.Abs(stopAngle - startAngle) > 360)
                {
                    stopAngle = startAngle + 360;
                }

                Mesh mesh;

                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation);
                }

                mesh.ApplyTransform(transform);

                return mesh;

            }

            private static Mesh GenerateSolidGeometry(
                EllipseType ellipseType,
                Vector2F diameter,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36)
            {
                if (startAngle < 0) startAngle = 0;
                if (stopAngle > 360) stopAngle = 360;
                
                var vertices = new List<Vector2D>();
                List<Vector2F> uvs = new List<Vector2F>();
                Vector3F center = Vector3F.Zero;
                float radiusX = diameter.X / 2;
                float radiusY = diameter.Y / 2;

                float range = stopAngle - startAngle;
                float angle = range / (tessellation - 1);
                
                float sign = -1;
                if (isClockWise)
                {
                    sign = 1;
                }
                
                startAngle = MathHelper.DegreesToRadians(startAngle);
                float currentAngle = startAngle;

                if (ellipseType == EllipseType.Sector && range < 360)
                {
                    vertices.Add(Vector2D.Zero);
                }

                for (int i = 0; i < tessellation; ++i)
                {
                    var angleItem = MathHelper.DegreesToRadians(currentAngle * sign);
                    double x = center.X + (radiusX * Math.Cos(angleItem));
                    Double y = center.Y + (radiusY * Math.Sin(angleItem));
                    
                    x = Math.Round(x, 4);
                    y = Math.Round(y, 4);
                    
                    var vertex = new Vector2D(x, y);
                        
                    vertices.Add(vertex);

                    currentAngle += angle;
                    if (currentAngle > stopAngle)
                    {
                        currentAngle = stopAngle;

                        if (currentAngle == 360)
                        {
                            currentAngle = 0;
                        }
                    }
                }

                var polygon = new Mathematics.Polygon();
                polygon.Polygons.Add(new PolygonItem(vertices));
                var points = polygon.Fill();

                for (int i = 0; i < points.Count; ++i)
                {
                    var point = points[i];
                    var uv = new Vector2F(
                        1.0f - (0.5f + (point.X - center.X) / diameter.X),
                        1.0f - (0.5f + (point.Y - center.Y) / diameter.Y));
                    uvs.Add(uv);
                }
                
                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.TriangleList).
                    SetPositions(points).
                    SetUVs(0, uvs).
                    Optimize();
                
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
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPositions(vertices).
                    GenerateBasicIndices();
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
                Matrix4x4F? transform = null)
            {
                var mesh = GenerateGeometry(geometryType, ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation, transform);
                return new Shape(graphicsDevice, mesh);
            }
        }
    }
}
