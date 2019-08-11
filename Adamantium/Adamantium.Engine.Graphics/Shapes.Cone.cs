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
        public class Cone
        {
            //Computes a point on a unit circle, aligned to the x/z plane and centered on the origin.
            private static Vector3F GetCircleVector(
                int i,
                int tessellation)
            {
                var angle = (float) (i * 2.0 * Math.PI / tessellation);
                var dx = (float) Math.Sin(angle);
                var dz = (float) Math.Cos(angle);

                return new Vector3F(dx, 0, dz);
            }

            // Creates a triangle fan to close the end of a cylinder.
            private static void CreateConeCap(
                List<Vector3F> positions,
                List<Vector2F> uvs,
                List<int> indices,
                int tessellation,
                float height,
                float radius,
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

                    int vbase = positions.Count;
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
                    var uv = new Vector2F(
                        1.0f - (circleVector.X * textureScale.X + 0.5f),
                        circleVector.Z * textureScale.Y + 0.5f);

                    positions.Add(position);
                    uvs.Add(uv);
                }
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float height = 1.0f,
                float topDiameter = 0.0f,
                float bottomDiameter = 1f,
                int tessellation = 40,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                if (tessellation < 3)
                {
                    tessellation = 3;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(height, topDiameter, bottomDiameter, tessellation, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(height, bottomDiameter, tessellation);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                float height = 1.0f,
                float topDiameter = 0.0f,
                float bottomDiameter = 1f,
                int tessellation = 32,
                bool toRightHanded = false)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleList;

                var positions = new List<Vector3F>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();

                height /= 2;

                var topRadius = topDiameter / 2;
                var bottomRadius = bottomDiameter / 2;

                var topOffset = Vector3F.UnitY * height;

                int stride = tessellation + 1;

                // Create a ring of triangles around the outside of the cylinder.
                for (int i = 0; i <= tessellation; i++)
                {
                    var normal = GetCircleVector(i, tessellation);

                    var sideOffsetTop = normal * topRadius;
                    var sideOffsetBottom = normal * bottomRadius;

                    var uv = new Vector2F((float) i / tessellation, 0);
                    uv.X = 1.0f - uv.X;

                    positions.Add(sideOffsetTop + topOffset);
                    uvs.Add(uv);
                    positions.Add(sideOffsetBottom - topOffset);
                    uvs.Add(uv + Vector2F.UnitY);

                    indices.Add(i * 2);
                    indices.Add(i * 2 + 1);
                    indices.Add((i * 2 + 2) % (stride * 2));

                    indices.Add(i * 2 + 1);
                    indices.Add((i * 2 + 3) % (stride * 2));
                    indices.Add((i * 2 + 2) % (stride * 2));
                }

                // Create flat triangle fan caps to seal the top and bottom.
                CreateConeCap(positions, uvs, indices, tessellation, height, topRadius, true);
                CreateConeCap(positions, uvs, indices, tessellation, height, bottomRadius, false);

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(positions);
                mesh.SetIndices(indices);
                if (toRightHanded)
                {
                    mesh.ReverseWinding();
                }

                mesh.SetUVs(0, uvs);
                mesh.Optimize();
                mesh.CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                float height = 1.0f,
                float bottomDiameter = 1f,
                int tessellation = 40)
            {
                PrimitiveType primitiveType = PrimitiveType.LineStrip;

                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                Vector3F center = Vector3F.Zero;
                int lastIndex = 0;
                float bottomRadius = bottomDiameter / 2;
                var topOffset = Vector3F.UnitY * height / 2;

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + bottomRadius * (float) Math.Cos(angle);
                    float y = center.Y + bottomRadius * (float) Math.Sin(angle);
                    var position = new Vector3F(x, -topOffset.Y, y);
                    vertices.Add(position);
                    indices.Add(lastIndex++);
                }

                //Add top vertex
                vertices.Add(topOffset);

                var vertexCount = tessellation + 1;
                var indexQuantity = indices.Count - 1;
                var quaterIndex = vertexCount / 4;
                var halfIndex = vertexCount / 2;

                //Add index for top position
                indices.Add(++indexQuantity);

                //Add index for left position
                indices.Add(indices[halfIndex]);
                indices.Add(Shape.StripSeparatorValue);
                indices.Add(indices[quaterIndex]);
                indices.Add(lastIndex);
                indices.Add(indices[quaterIndex + halfIndex]);

                var mesh = new Mesh();
                mesh.SetPositions(vertices);
                mesh.SetIndices(indices);
                mesh.MeshTopology = primitiveType;

                return mesh;
            }

            /// <summary>
            /// Creates a cylinder primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="height">The height.</param>
            /// <param name="topDiameter">Diameter of the top side</param>
            /// <param name="bottomDiameter">Diameter of the bottom side</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="toRightHanded">if set to <c>true</c> vertices and indices will be transformed to right handed. Default is false.</param>
            /// <returns>A cone/cylinder/frustum/pyramid primitive.</returns>
            /// <remarks>Cone: top diameter == 0.05f, bottom diameter > top diameter </remarks>
            /// <remarks>Frustum: top diameter > 0.05f, bottom diameter > top diameter </remarks>
            /// <remarks>Cylinder: top diameter == bottom diameter </remarks>
            /// <remarks>Pyramid: top diameter == 0, tesselation = 4 </remarks>
            /// <exception cref="ArgumentOutOfRangeException">tessellation;tessellation must be &gt;= 3</exception>
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float height = 1.0f,
                float topDiameter = 0.05f,
                float bottomDiameter = 1f,
                int tessellation = 32,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var geometry = GenerateGeometry(geometryType, height, topDiameter, bottomDiameter, tessellation, transform, toRightHanded);
                // Create the primitive object.
                return new Shape(device, geometry);
            }
        }
    }
}
