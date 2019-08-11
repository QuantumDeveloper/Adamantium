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
            private static Vector3F GetCircleVector(
                int i,
                int tessellation)
            {
                var angle = (float) (i * 2.0 * Math.PI / tessellation);
                var dx = (float) Math.Sin(angle);
                var dz = (float) Math.Cos(angle);

                return new Vector3F(dx, 0, dz);
            }

            // Helper creates a triangle fan to close the end of a cylinder.
            private static void CreateCylinderCap(
                List<Vector3F> vertices,
                List<Vector2F> uvs,
                List<int> indices,
                int tessellation,
                float radius,
                float height,
                bool isTop)
            {
                // Create cap indices.
                for (int i = 0; i < tessellation - 2; i++)
                {
                    int i1 = (i + 1) % tessellation;
                    int i2 = (i + 2) % tessellation;

                    if (isTop)
                    {
                        Utilities.Swap(ref i1, ref i2);
                    }

                    int vbase = vertices.Count;
                    indices.Add(vbase);
                    indices.Add(vbase + i2);
                    indices.Add(vbase + i1);
                }

                // Which end of the cylinder is this?
                var normal = Vector3F.UnitY;
                var textureScale = new Vector2F(-0.5f);

                if (!isTop)
                {
                    normal = -normal;
                    textureScale.X = -textureScale.X;
                }

                // Create cap vertices.
                for (int i = 0; i < tessellation; i++)
                {
                    var circleVector = GetCircleVector(i, tessellation);
                    var position = (circleVector * radius) + (normal * height);
                    var textureCoordinate = new Vector2F(
                        circleVector.X * textureScale.X + 0.5f,
                        circleVector.Z * textureScale.Y + 0.5f);

                    vertices.Add(position);
                    uvs.Add(textureCoordinate);
                }
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float height = 1.0f,
                float diameter = 1.0f,
                int tessellation = 32,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                if (tessellation < 3)
                    tessellation = 3;

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(height, diameter, tessellation, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(height, diameter, tessellation);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            public static Mesh GenerateSolidGeometry(
                float height = 1.0f,
                float diameter = 1.0f,
                int tessellation = 32,
                bool toRightHanded = false)
            {
                var vertices = new List<Vector3F>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();

                height /= 2;

                var topOffset = Vector3F.UnitY * height;

                float radius = diameter / 2;
                int stride = tessellation + 1;

                // Create a ring of triangles around the outside of the cylinder.
                for (int i = 0; i <= tessellation; i++)
                {
                    var normal = GetCircleVector(i, tessellation);

                    var sideOffset = normal * radius;

                    var uv = new Vector2F((float) i / tessellation, 0);
                    uv.X = 1.0f - uv.X;

                    vertices.Add(sideOffset + topOffset);
                    uvs.Add(uv);
                    vertices.Add(sideOffset - topOffset);
                    uvs.Add(uv + Vector2F.UnitY);

                    indices.Add(i * 2);
                    indices.Add(i * 2 + 1);
                    indices.Add((i * 2 + 2) % (stride * 2));

                    indices.Add(i * 2 + 1);
                    indices.Add((i * 2 + 3) % (stride * 2));
                    indices.Add((i * 2 + 2) % (stride * 2));
                }

                // Create flat triangle fan caps to seal the top and bottom.
                CreateCylinderCap(vertices, uvs, indices, tessellation, radius, height, true);
                CreateCylinderCap(vertices, uvs, indices, tessellation, radius, height, false);

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.TriangleList;
                mesh.SetPositions(vertices);
                mesh.SetIndices(indices);
                mesh.SetUVs(0, uvs);
                if (toRightHanded)
                {
                    mesh.ReverseWinding();
                }
                mesh.CalculateNormals();

                return mesh;
            }

            public static Mesh GenerateOutlinedGeometry(
                float height = 1.0f,
                float diameter = 1.0f,
                int tessellation = 32)
            {
                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                int lastIndex = 0;
                float radius = diameter / 2;
                var topOffset = Vector3F.UnitY * height / 2;

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

                indices.Add(Shape.StripSeparatorValue);

                for (int i = 0; i <= tessellation; ++i)
                {
                    var normal = GetCircleVector(i, tessellation);
                    var sideOffset = normal * radius;
                    vertices.Add(sideOffset - topOffset);
                    vertices.Add(sideOffset + topOffset);
                    indices.Add(lastIndex++);
                    indices.Add(lastIndex++);
                    indices.Add(Shape.StripSeparatorValue);
                }

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.LineStrip;
                mesh.SetPositions(vertices);
                mesh.SetIndices(indices);

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
            /// <param name="toRightHanded">if set to <c>true</c> vertices and indices will be transformed to left handed. Default is true.</param>
            /// <returns>A cylinder primitive.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">tessellation;tessellation must be &gt;= 3</exception>
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float height = 1.0f,
                float diameter = 1.0f,
                int tessellation = 32,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var geometry = GenerateGeometry(geometryType, height, diameter, tessellation, transform, toRightHanded);
                return new Shape(device, geometry);
            }
        }
    }
}
