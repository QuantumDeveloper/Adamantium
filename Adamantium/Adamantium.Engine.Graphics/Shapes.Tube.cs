using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Tube
        {
            // Helper computes a point on a unit circle, aligned to the x/z plane and centered on the origin.
            private static Vector3F GetCircleVector(
                int i,
                int tessellation)
            {
                var angle = (float)(i * 2.0 * Math.PI / tessellation);
                var dx = (float)Math.Sin(angle);
                var dz = (float)Math.Cos(angle);

                return new Vector3F(dx, 0, dz);
            }

            private static void CreateCylinder(
                List<Vector3F> vertices,
                List<Vector2F> uvs,
                List<int> indices,
                float radius,
                float height,
                int tessellation,
                bool isInnerCylinder
            )
            {
                height /= 2;
                var topOffset = Vector3F.UnitY * height;
                int stride = tessellation + 1;
                int vbase = vertices.Count;
                // Create a ring of triangles around the outside of the cylinder.
                for (int i = 0; i <= tessellation; i++)
                {
                    var normal = GetCircleVector(i, tessellation);

                    var sideOffset = normal * radius;

                    var uv = new Vector2F((float)i / tessellation, 0);

                    vertices.Add(sideOffset + topOffset);
                    uvs.Add(uv);
                    vertices.Add(sideOffset - topOffset);
                    uvs.Add(uv + Vector2F.UnitY);

                    int index = 0;
                    if (isInnerCylinder)
                    {
                        indices.Add(i * 2 + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(i * 2 + 1 + vbase);
                        indices.Add(index + vbase);

                        indices.Add(i * 2 + 1 + vbase);
                        index = (i * 2 + 3) % (stride * 2);
                        indices.Add(index + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                    }
                    else
                    {
                        indices.Add(i * 2 + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                        indices.Add(i * 2 + 1 + vbase);

                        indices.Add(i * 2 + 1 + vbase);
                        
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                        
                        index = (i * 2 + 3) % (stride * 2);
                        indices.Add(index + vbase);
                    }
                }
            }

            private static void CreateTubeCap(
                List<Vector3F> vertices,
                List<Vector2F> uvs,
                List<int> indices,
                float radius,
                float height,
                float thickness,
                int tessellation,
                bool isTop
            )
            {
                height /= 2;
                int vbase = vertices.Count;
                var stride = tessellation + 1;

                // Which end of the cylinder is this?
                var normal = Vector3F.UnitY;
                var textureScale = new Vector2F(-0.5f);

                if (!isTop)
                {
                    normal = -normal;
                    textureScale.X = -textureScale.X;
                }

                // Create cap vertices.
                for (int i = 0; i <= tessellation; i++)
                {
                    var circleVector = GetCircleVector(i, tessellation);

                    var position = (circleVector * (radius + thickness)) + (normal * height);
                    var textureCoordinate = new Vector2F(
                        circleVector.X * textureScale.X + 0.5f,
                        circleVector.Z * textureScale.Y + 0.5f);

                    vertices.Add(position);
                    uvs.Add(textureCoordinate);

                    position = (circleVector * radius) + (normal * height);
                    textureCoordinate = new Vector2F(
                        circleVector.X * textureScale.X + 0.5f - (1 / radius),
                        circleVector.Z * textureScale.Y + 0.5f - (1 / radius));

                    vertices.Add(position);
                    uvs.Add(textureCoordinate);

                    int index = 0;
                    if (isTop)
                    {
                        indices.Add(i * 2 + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(i * 2 + 1 + vbase);
                        indices.Add(index + vbase);

                        indices.Add(i * 2 + 1 + vbase);
                        index = (i * 2 + 3) % (stride * 2);
                        indices.Add(index + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                    }
                    else
                    {
                        indices.Add(i * 2 + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                        indices.Add(i * 2 + 1 + vbase);

                        indices.Add(i * 2 + 1 + vbase);
                        index = (i * 2 + 2) % (stride * 2);
                        indices.Add(index + vbase);
                        index = (i * 2 + 3) % (stride * 2);
                        indices.Add(index + vbase);
                    }
                }
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float diameter,
                float height,
                float thickness,
                int tessellation = 36,
                Matrix4x4F? transform = null)
            {
                if (tessellation < 3)
                {
                    tessellation = 3;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(diameter, height, thickness, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(diameter, height, thickness, tessellation);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                float diameter,
                float height,
                float thickness,
                int tessellation = 40)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleList;

                float radius = diameter / 2;

                var vertices = new List<Vector3F>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();
                CreateCylinder(vertices, uvs, indices, radius, height, tessellation, true);
                CreateCylinder(vertices, uvs, indices, radius + thickness, height, tessellation, false);

                // Create flat triangle fan caps to seal the top and bottom.
                CreateTubeCap(vertices, uvs, indices, radius, height, thickness, tessellation, true);
                CreateTubeCap(vertices, uvs, indices, radius, height, thickness, tessellation, false);

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPoints(vertices);
                mesh.SetIndices(indices);
                mesh.SetUVs(0, uvs);
                mesh.CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                float diameter,
                float height,
                float thickness,
                int tessellation = 40)
            {
                float radius = diameter / 2;

                var vertices = new List<Vector3F>();
                var indices = new List<int>();

                GenerateOutlinedCylinder(vertices, indices, radius, height, tessellation);
                GenerateOutlinedCylinder(vertices, indices, radius + thickness, height, tessellation);
                GenerateOutlinedCaps(vertices, indices, radius, height, thickness, tessellation, true);
                GenerateOutlinedCaps(vertices, indices, radius, height, thickness, tessellation, false);

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPoints(vertices).
                    SetIndices(indices);

                return mesh;
            }

            private static void GenerateOutlinedCylinder(
                List<Vector3F> vertices,
                List<int> indices,
                float radius,
                float height,
                int tessellation = 40)
            {
                int lastIndex = vertices.Count;
                var topOffset = Vector3F.UnitY * height / 2;

                for (int i = 0; i <= tessellation; ++i)
                {
                    var normal = GetCircleVector(i, tessellation);
                    var sideOffset = normal * radius;

                    vertices.Add(sideOffset - topOffset);
                    indices.Add(lastIndex++);
                }

                indices.Add(Shape.PrimitiveRestartValue);

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
            }

            private static void GenerateOutlinedCaps(
                List<Vector3F> vertices,
                List<int> indices,
                float radius,
                float height,
                float thickness,
                int tessellation,
                bool isTop)
            {
                height /= 2;
                int lastIndex = vertices.Count;
                var normal = Vector3F.UnitY;

                if (!isTop)
                {
                    normal = -normal;
                }

                for (int i = 0; i <= tessellation; ++i)
                {
                    var circleVector = GetCircleVector(i, tessellation);
                    var position = (circleVector * (radius + thickness)) + (normal * height);
                    vertices.Add(position);
                    indices.Add(lastIndex++);

                    position = (circleVector * radius) + (normal * height);
                    vertices.Add(position);
                    indices.Add(lastIndex++);

                    indices.Add(Shape.PrimitiveRestartValue);
                }
            }

            /// <summary>
            /// Creates a cylinder primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="geometryType"></param>
            /// <param name="height">The height.</param>
            /// <param name="diameter">Diameter of the top side</param>
            /// <param name="thickness">Thickness of the tube</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="transform"></param>
            /// <returns>A tube primitive.</returns>
            /// <exception cref="ArgumentOutOfRangeException">tessellation;tessellation must be &gt;= 3</exception>
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float diameter,
                float height,
                float thickness,
                int tessellation = 36,
                Matrix4x4F? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, diameter, height, thickness, tessellation, transform);
                // Create the primitive object.
                return new Shape(device, geometry);
            }
        }
    }
}
