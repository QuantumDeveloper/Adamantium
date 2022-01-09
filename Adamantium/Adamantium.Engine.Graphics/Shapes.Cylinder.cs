using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Cylinder
        {
            // Helper computes a point on a unit circle, aligned to the x/z plane and centered on the origin.
            private static Vector3 GetCircleVector(
                int i,
                int tessellation)
            {
                var angle = (float) (i * 2.0 * Math.PI / tessellation);
                var dx = (float) Math.Sin(angle);
                var dz = (float) Math.Cos(angle);

                return new Vector3(dx, 0, dz);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                double height = 1.0,
                double diameter = 1.0,
                int tessellation = 32,
                Matrix4x4? transform = null)
            {
                if (tessellation < 3)
                    tessellation = 3;

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(height, diameter, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(height, diameter, tessellation);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            public static Mesh GenerateSolidGeometry(
                double height = 1.0,
                double diameter = 1.0,
                int tessellation = 32
                )
            {
                return Cone.GenerateGeometry(GeometryType.Solid, height, diameter, diameter, tessellation);
            }

            public static Mesh GenerateOutlinedGeometry(
                double height = 1.0,
                double diameter = 1.0,
                int tessellation = 32)
            {
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                int lastIndex = 0;
                var radius = diameter / 2;
                var topOffset = Vector3.Down * height / 2;

                for (int i = 0; i <= tessellation; ++i)
                {
                    var normal = GetCircleVector(i, tessellation);
                    var sideOffset = normal * radius;
                    vertices.Add(sideOffset - topOffset);
                    indices.Add(lastIndex++);
                }

                for (int i = 0; i <= tessellation; ++i)
                {
                    var normal = GetCircleVector(i, tessellation);
                    var sideOffset = normal * radius;
                    vertices.Add(sideOffset + topOffset);
                    indices.Add(lastIndex++);
                }

                indices.Add(Shape.PrimitiveRestartValue);

                for (int i = 0; i <= tessellation; ++i)
                {
                    var normal = GetCircleVector(i, tessellation);
                    var sideOffset = normal * radius;
                    vertices.Add(sideOffset - topOffset);
                    vertices.Add(sideOffset + topOffset);
                    indices.Add(lastIndex++);
                    indices.Add(lastIndex++);
                    indices.Add(Shape.PrimitiveRestartValue);
                }

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPoints(vertices).
                    SetIndices(indices);

                return mesh;
            }

            /// <summary>
            /// Creates a cylinder primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="geometryType"></param>
            /// <param name="height">The height.</param>
            /// <param name="diameter">The diameter.</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="transform">Transform matrix</param>
            /// <returns>A cylinder primitive.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">tessellation;tessellation must be &gt;= 3</exception>
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float height = 1.0f,
                float diameter = 1.0f,
                int tessellation = 32,
                Matrix4x4? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, height, diameter, tessellation, transform);
                return new Shape(device, geometry);
            }
        }
    }
}
