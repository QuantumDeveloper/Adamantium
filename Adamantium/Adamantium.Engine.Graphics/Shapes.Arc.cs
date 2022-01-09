﻿using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Arc
        {
            public static Shape New(
                GraphicsDevice graphicsDevice,
                GeometryType shapeType,
                Vector2 diameter,
                float thickness,
                float startAngle = 0,
                float stopAngle = 360,
                bool isClockWise = true,
                int tessellation = 36,
                Matrix4x4? transform = null)
            {
                var mesh = GenerateGeometry(
                    shapeType, 
                    diameter, 
                    thickness, 
                    startAngle, 
                    stopAngle, 
                    isClockWise,
                    tessellation, 
                    transform);
                
                return new Shape(graphicsDevice, mesh);
            }

        public static Mesh GenerateGeometry(
               GeometryType shapeType,
               Vector2 diameter,
               double thickness,
               float startAngle = 0,
               float stopAngle = 360,
               bool isClockWise = true,
               int tessellation = 36,
               Matrix4x4? transform = null)
            {
                if (Math.Abs(stopAngle - startAngle) > 360)
                {
                    stopAngle = startAngle + 360;
                }

                PrimitiveType primitiveType = PrimitiveType.TriangleStrip;
                if (shapeType == GeometryType.Outlined)
                {
                    primitiveType = PrimitiveType.LineStrip;
                }

                var uvs = new List<Vector2F>();
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                var center = Vector2.Zero;
                double radiusX = diameter.X / 2;
                double radiusY = diameter.Y / 2;

                if (thickness >= radiusX)
                {
                    thickness = radiusX;
                }

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

                var vertex = Vector3.Zero;

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;

                if (shapeType == GeometryType.Solid)
                {
                    for (int i = 0; i <= tessellation; ++i)
                    {
                        vertex = CreatePoint(center, radiusX, radiusY, 0, angle);
                        vertices.Add(vertex);
                        vertex = CreatePoint(center, radiusX, radiusY, thickness, angle);
                        vertices.Add(vertex);
                        angle += angleItem;
                    }

                    mesh.SetPoints(vertices).
                        GenerateBasicIndices().
                        Optimize();

                    for (int i = 0; i < mesh.Points.Length; ++i)
                    {
                        var position = mesh.Points[i];
                        var uv = new Vector2(
                           0.5f + (position.X - center.X) / (2 * radiusX),
                           0.5f - (position.Y - center.Y) / (2 * radiusY));
                        uvs.Add(uv);
                    }

                    mesh.SetUVs(0, uvs);
                }
                else
                {
                    int lastIndex = 0;
                    for (int i = 0; i <= tessellation; ++i)
                    {
                        vertex = CreatePoint(center, radiusX, radiusY, 0, angle);
                        vertices.Add(vertex);
                        indices.Add(lastIndex++);
                        angle += angleItem;
                    }

                    angle -= angleItem;

                    if (range < 360)
                    {
                        vertex = CreatePoint(center, radiusX, radiusY, thickness, angle);
                        vertices.Add(vertex);
                        indices.Add(lastIndex++);
                    }
                    else
                    {
                        indices.Add(Shape.PrimitiveRestartValue);
                    }

                    for (int i = tessellation; i >= 0; --i)
                    {
                        vertex = CreatePoint(center, radiusX, radiusY, thickness, angle);
                        vertices.Add(vertex);
                        indices.Add(lastIndex++);
                        angle -= angleItem;
                    }

                    if (range < 360)
                    {
                        indices.Add(0);
                    }

                    mesh.SetPoints(vertices).
                        SetIndices(indices);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            private static Vector3 CreatePoint(Vector2 center, double radiusX, double radiusY, double thickness, float angle)
            {
                var x = center.X + ((radiusX - thickness) * (float)Math.Cos(angle));
                var y = center.Y + ((radiusY - thickness) * (float)Math.Sin(angle));
                if (Math.Abs(x) < Mathematics.Polygon.Epsilon)
                {
                    x = 0;
                }
                if (Math.Abs(y) < Mathematics.Polygon.Epsilon)
                {
                    y = 0;
                }
                var vertex = new Vector3(x, y, 0);
                return vertex;
            }
        }
    }
}
