using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Capsule
        {
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

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float height,
                float diameter,
                int tessellation = 40,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                if (tessellation < 8)
                {
                    tessellation = 8;
                }
                var radius = diameter / 2;
                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(height, radius, tessellation, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(height, radius, tessellation);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                float height,
                float radius,
                int tessellation = 40,
                bool toRightHanded = false)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleList;
                List<Vector3F> vertices = new List<Vector3F>();
                List<Vector2F> uvs = new List<Vector2F>();
                List<int> indices = new List<int>();
                if (tessellation < 3)
                    tessellation = 3;

                float uScale = 1;
                float vScale = 1;

                int verticalSegments = 2 * tessellation;
                int horizontalSegments = 4 * tessellation;

                // Create rings of vertices at progressively higher latitudes.
                for (int i = 0; i < verticalSegments; i++)
                {
                    float v;
                    float deltaY;
                    float latitude;
                    if (i < verticalSegments / 2)
                    {
                        deltaY = -height / 2;
                        v = 1.0f - (0.25f * i / (tessellation - 1));
                        latitude = (float) (i * Math.PI / (verticalSegments - 2) - Math.PI / 2.0);
                    }
                    else
                    {
                        deltaY = height / 2;
                        v = 0.5f - 0.25f * (i - 1) / (tessellation - 1);
                        latitude = (float) ((i - 1) * Math.PI / (verticalSegments - 2) - Math.PI / 2.0);
                    }

                    var dy = (float) Math.Sin(latitude);
                    var dxz = (float) Math.Cos(latitude);


                    // Create a single ring of vertices at this latitude.
                    for (int j = 0; j <= horizontalSegments; j++)
                    {
                        float u = (float) j / horizontalSegments;

                        var longitude = (float) (j * 2.0 * Math.PI / horizontalSegments);
                        var dx = (float) Math.Sin(longitude);
                        var dz = (float) Math.Cos(longitude);

                        dx *= dxz;
                        dz *= dxz;

                        var normal = new Vector3F(dx, dy, dz);
                        var uv = new Vector2F(u * uScale, v * vScale);
                        var position = radius * normal + new Vector3F(0, deltaY, 0);
                        vertices.Add(position);
                        uvs.Add(uv);
                    }
                }

                // Fill the index buffer with triangles joining each pair of latitude rings.
                int stride = horizontalSegments + 1;

                for (int i = 0; i < verticalSegments - 1; i++)
                {
                    for (int j = 0; j <= horizontalSegments; j++)
                    {
                        int nextI = i + 1;
                        int nextJ = (j + 1) % stride;

                        indices.Add(i * stride + j);
                        indices.Add(i * stride + nextJ);
                        indices.Add(nextI * stride + j);

                        indices.Add(i * stride + nextJ);
                        indices.Add(nextI * stride + nextJ);
                        indices.Add(nextI * stride + j);
                    }
                }

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetIndices(indices);
                mesh.Optimize();

                if (toRightHanded)
                {
                    mesh.ReverseWinding();
                }

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                float height,
                float radius,
                int tessellation = 40)
            {
                PrimitiveType primitiveType = PrimitiveType.LineStrip;
                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                Vector3F center = Vector3F.Zero;
                int lastIndex = 0;
                var topOffset = Vector3F.UnitY * height / 2;

                //draw top hemicircle
                for (int i = 0; i <= tessellation / 2; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = topOffset.X + radius * (float) Math.Cos(angle);
                    float y = topOffset.Y + radius * (float) Math.Sin(angle);
                    vertices.Add(new Vector3F(x, y, 0));
                    indices.Add(lastIndex++);
                }

                //draw bottom hemicircle
                for (int i = tessellation / 2; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = -topOffset.X + radius * (float) Math.Cos(angle);
                    float y = -topOffset.Y + radius * (float) Math.Sin(angle);
                    vertices.Add(new Vector3F(x, y, 0));
                    indices.Add(lastIndex++);
                }

                indices.Add(0);
                indices.Add(Shape.StripSeparatorValue);
                var startPos = vertices.Count;

                QuaternionF rot = QuaternionF.RotationAxis(Vector3F.UnitY, MathHelper.DegreesToRadians(90));
                Matrix4x4F rotMatrix = Matrix4x4F.RotationQuaternion(rot);
                var secondPart = new List<Vector3F>();
                for (int i = 0; i < vertices.Count; i++)
                {
                    var pos = Vector3F.TransformCoordinate(vertices[i], rotMatrix);
                    secondPart.Add(pos);
                    indices.Add(lastIndex++);
                }

                vertices.AddRange(secondPart);

                indices.Add(startPos);
                indices.Add(Shape.StripSeparatorValue);


                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + radius * (float) Math.Cos(angle);
                    float y = center.Y + radius * (float) Math.Sin(angle);

                    vertices.Add(new Vector3F(x, -topOffset.Y, y));
                    indices.Add(lastIndex++);
                }

                indices.Add(Shape.StripSeparatorValue);

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + radius * (float) Math.Cos(angle);
                    float y = center.Y + radius * (float) Math.Sin(angle);

                    vertices.Add(new Vector3F(x, topOffset.Y, y));
                    indices.Add(lastIndex++);
                }

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(vertices);
                mesh.SetIndices(indices);

                return mesh;
            }
        }
    }
}
