using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Ellipse
        {
            public static Shape New(
                GraphicsDevice graphicsDevice,
                GeometryType geometryType,
                EllipseType ellipseType,
                Vector2 diameter,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                Matrix4x4? transform = null)
            {
                var mesh = GenerateGeometry(geometryType, ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation, transform);
                return new Shape(graphicsDevice, mesh);
            }
            
            public static Mesh GenerateGeometry(
               GeometryType geometryType,
               EllipseType ellipseType,
               Vector2 diameter,
               double startAngle = 0,
               double stopAngle = 360,
               bool isClockWise = true,
               int tessellation = 36,
               Matrix4x4? transform = null)
            {
                if (Math.Abs(stopAngle - startAngle) > 360)
                {
                    stopAngle = startAngle + 360;
                }

                Mesh mesh;

                switch (geometryType)
                {
                    case GeometryType.Solid:
                        mesh = GenerateSolidGeometry(ellipseType, diameter, startAngle, stopAngle, isClockWise, tessellation, transform);
                        break;
                    case GeometryType.Both:
                    {
                        var contour = GenerateOutlinedGeometry(ellipseType, diameter, startAngle, stopAngle,
                            isClockWise, tessellation, transform);
                        mesh = new Mesh();
                        mesh.AddContour(contour, true);
                        var vertices = Triangulate(contour);
                        mesh.SetPoints(vertices).SetTopology(PrimitiveType.LineStrip).GenerateBasicIndices();
                        break;
                    }
                    default:
                    {
                        var vertices = GenerateOutlinedGeometry(ellipseType, diameter, startAngle, stopAngle,
                            isClockWise, tessellation, transform);
                        mesh = new Mesh();
                        mesh.SetPoints(vertices).SetTopology(PrimitiveType.LineStrip).GenerateBasicIndices();
                        break;
                    }
                }

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                EllipseType ellipseType,
                Vector2 diameter,
                double startAngle = 0,
                double stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                Matrix4x4? transform = null)
            {
                if (startAngle < 0) startAngle = 0;
                if (stopAngle > 360) stopAngle = 360;
                
                var vertices = new List<Vector2>();
                List<Vector2F> uvs = new List<Vector2F>();
                var center = Vector3.Zero;
                var radiusX = diameter.X / 2;
                var radiusY = diameter.Y / 2;

                var range = stopAngle - startAngle;
                var angle = range / (tessellation - 1);
                
                float sign = -1;
                if (isClockWise)
                {
                    sign = 1;
                }
                
                startAngle = MathHelper.DegreesToRadians(startAngle);
                var currentAngle = startAngle;

                if (ellipseType == EllipseType.Sector && range < 360)
                {
                    vertices.Add(Vector2.Zero);
                }

                for (int i = 0; i < tessellation; ++i)
                {
                    var angleItem = MathHelper.DegreesToRadians(currentAngle * sign);
                    var x = center.X + (radiusX * Math.Cos(angleItem));
                    var y = center.Y + (radiusY * Math.Sin(angleItem));
                    
                    x = Math.Round(x, 4);
                    y = Math.Round(y, 4);
                    
                    var vertex = new Vector2(x, y);
                        
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
                polygon.AddItem(new MeshContour(vertices));
                var points = polygon.Fill();

                for (int i = 0; i < points.Count; ++i)
                {
                    var point = points[i];
                    var uv = new Vector2F(
                        1.0f - (float)(0.5 + (point.X - center.X) / diameter.X),
                        1.0f - (float)(0.5 + (point.Y - center.Y) / diameter.Y));
                    uvs.Add(uv);
                }
                
                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.TriangleList).
                    SetPoints(points).
                    SetUVs(0, uvs).
                    Optimize().
                    ApplyTransform(transform);
                
                return mesh;
            }

            private static List<Vector3> GenerateOutlinedGeometry(
                EllipseType ellipseType,
                Vector2 diameter,
                double startAngle = 0,
                double stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                Matrix4x4? transform = null)
            {
                var vertices = new List<Vector3>();
                var center = Vector3.Zero;
                var radiusX = diameter.X / 2;
                var radiusY = diameter.Y / 2;

                var range = Math.Abs(stopAngle) - Math.Abs(startAngle);
                var angle = range / tessellation;

                float sign = 1;
                if (isClockWise)
                {
                    sign = -1;
                }

                float angleItem = MathHelper.DegreesToRadians(angle) * sign;
                startAngle = MathHelper.DegreesToRadians(startAngle);
                angle = startAngle * sign;

                for (int i = 0; i < tessellation; ++i)
                {
                    double x = center.X + (radiusX * Math.Cos(angle));
                    double y = center.Y + (radiusY * Math.Sin(angle));

                    x = Math.Round(x, 4, MidpointRounding.AwayFromZero);
                    y = Math.Round(y, 4, MidpointRounding.AwayFromZero);

                    var vertex = new Vector3(x, y, 0);
                    vertices.Add(vertex);
                    angle += angleItem;
                }
                
                if (range < 360 && ellipseType == EllipseType.Sector)
                {
                    vertices.Add(center);
                }

                if (transform is { IsIdentity: false })
                {
                    vertices = Mesh.ApplyTransform(vertices, transform.Value).ToList();
                }

                return vertices;
            }
            
            private static List<Vector3> Triangulate(List<Vector3> vertices)
            {
                var polygon = new Mathematics.Polygon();
                polygon.AddItem(new MeshContour(vertices));
                var points = polygon.Fill();
                return points;
            }
        }
    }
}
