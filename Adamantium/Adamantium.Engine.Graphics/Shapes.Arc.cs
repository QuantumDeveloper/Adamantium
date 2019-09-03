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
            public static Mesh GenerateGeometry(
               GeometryType shapeType,
               Vector2F diameter,
               float thickness,
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

                PrimitiveType primitiveType = PrimitiveType.TriangleStrip;
                if (shapeType == GeometryType.Outlined)
                {
                    primitiveType = PrimitiveType.LineStrip;
                }

                List<Vector2F> uvs = new List<Vector2F>();
                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                Vector2F center = Vector2F.Zero;
                float radiusX = diameter.X / 2;
                float radiusY = diameter.Y / 2;

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

                Vector3F vertex = Vector3F.Zero;

                Mesh mesh = new Mesh();
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

                    mesh.SetPositions(vertices);
                    mesh.GenerateBasicIndices();
                    mesh.Optimize();

                    for (int i = 0; i < mesh.Positions.Length; ++i)
                    {
                        var position = mesh.Positions[i];
                        var uv2 = new Vector2D(
                           0.5f + (position.X - center.X) / (2 * radiusX),
                           0.5f - (position.Y - center.Y) / (2 * radiusY));
                        uvs.Add(uv2);
                    }

                    mesh.SetUVs(0, uvs);

                    if (toRightHanded)
                    {
                        mesh.ReverseWinding();
                    }
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
                        indices.Add(Shape.StripSeparatorValue);
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

                    mesh.SetPositions(vertices);
                    mesh.SetIndices(indices);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            private static Vector3F CreatePoint(Vector2F center, float radiusX, float radiusY, float thickness, float angle)
            {
                float x = center.X + ((radiusX - thickness) * (float)Math.Cos(angle));
                float y = center.Y + ((radiusY - thickness) * (float)Math.Sin(angle));
                if (Math.Abs(x) < Mathematics.Polygon.Epsilon)
                {
                    x = 0;
                }
                if (Math.Abs(y) < Mathematics.Polygon.Epsilon)
                {
                    y = 0;
                }
                var vertex = new Vector3F(x, y, 0);
                return vertex;
            }
        }
    }
}