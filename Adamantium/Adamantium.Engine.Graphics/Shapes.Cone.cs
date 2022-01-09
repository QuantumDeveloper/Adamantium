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
            private static Vector3 GetCircleVector(
                int i,
                int tessellation)
            {
                var angle = (float) (i * 2.0 * Math.PI / tessellation);
                var dx = (float) Math.Sin(angle);
                var dz = (float) Math.Cos(angle);

                return new Vector3(dx, 0, dz);
            }

            // Creates a triangle fan to close the end of a cylinder.
            private static void CreateConeCap(
                List<Vector3> positions,
                List<Vector2F> uvs,
                List<int> indices,
                int tessellation,
                double height,
                double radius,
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
                    indices.Add(vbase + i1);
                    indices.Add(vbase + i2);
                }

                // Which end of the cylinder is this?
                var normal = Vector3.UnitY;
                var textureScale = new Vector2F(-0.5f);

                if (!isTop)
                {
                    normal = -normal;
                    textureScale.X = -textureScale.X;
                }
                
                var diameter = new Vector2F((float)radius * 2);
                // Create cap vertices.
                for (int i = 0; i < tessellation; i++)
                {
                    var circleVector = GetCircleVector(i, tessellation);
                    var position = (circleVector * radius) + (normal * height);
                    var uv = new Vector2F(
                        0.5f + (float)(position.X / diameter.X),
                        0.5f - (float)(position.Z / diameter.Y));

                    positions.Add(position);
                    uvs.Add(uv);
                }
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                double height = 1.0,
                double topDiameter = 0.0,
                double bottomDiameter = 1,
                int tessellation = 40,
                Matrix4x4? transform = null)
            {
                if (tessellation < 3)
                {
                    tessellation = 3;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(height, topDiameter, bottomDiameter, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(height, bottomDiameter, tessellation);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                double height = 1.0,
                double topDiameter = 0.0,
                double bottomDiameter = 1,
                int tessellation = 32)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleList;

                var positions = new List<Vector3>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();

                height /= 2;

                var topRadius = topDiameter / 2;
                var bottomRadius = bottomDiameter / 2;

                var topOffset = Vector3.UnitY * height;

                int stride = tessellation + 1;

                // Create a ring of triangles around the outside of the cylinder.
                for (int i = 0; i <= tessellation; i++)
                {
                    var normal = GetCircleVector(i, tessellation);

                    var sideOffsetTop = normal * topRadius;
                    var sideOffsetBottom = normal * bottomRadius;

                    var uv = new Vector2F(i / (float)tessellation, 0);

                    positions.Add(sideOffsetTop + topOffset);
                    uvs.Add(uv);
                    positions.Add(sideOffsetBottom - topOffset);
                    uvs.Add(uv + Vector2F.UnitY);

                    indices.Add(i * 2);
                    indices.Add((i * 2 + 2) % (stride * 2));
                    indices.Add(i * 2 + 1);

                    indices.Add(i * 2 + 1);
                    indices.Add((i * 2 + 2) % (stride * 2));
                    indices.Add((i * 2 + 3) % (stride * 2));
                }

                // Create flat triangle fan caps to seal the top and bottom.
                CreateConeCap(positions, uvs, indices, tessellation, height, topRadius, true);
                CreateConeCap(positions, uvs, indices, tessellation, height, bottomRadius, false);

                var mesh = new Mesh();
                mesh.SetTopology(primitiveType).
                    SetPoints(positions).
                    SetIndices(indices).
                    SetUVs(0, uvs).
                    Optimize().
                    CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                double height = 1.0,
                double bottomDiameter = 1,
                int tessellation = 40)
            {
                var primitiveType = PrimitiveType.LineStrip;

                var vertices = new List<Vector3>();
                var indices = new List<int>();
                var center = Vector3.Zero;
                int lastIndex = 0;
                var bottomRadius = bottomDiameter / 2;
                var topOffset = Vector3.UnitY * height / 2;
                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    double x = center.X + bottomRadius * (float) Math.Cos(angle);
                    double y = center.Y + bottomRadius * (float) Math.Sin(angle);
                    var position = new Vector3(x, -topOffset.Y, y);
                    vertices.Add(position);
                    indices.Add(lastIndex++);
                }

                //Add top vertex
                vertices.Add(topOffset);

                var vertexCount = tessellation + 1;
                var indexQuantity = indices.Count - 1;
                var quarterIndex = vertexCount / 4;
                var halfIndex = vertexCount / 2;

                //Add index for top position
                indices.Add(++indexQuantity);

                //Add index for left position
                indices.Add(indices[halfIndex]);
                indices.Add(Shape.PrimitiveRestartValue);
                indices.Add(indices[quarterIndex]);
                indices.Add(lastIndex);
                indices.Add(indices[quarterIndex + halfIndex]);

                var mesh = new Mesh();
                mesh.SetPoints(vertices).
                    SetIndices(indices).
                    SetTopology(primitiveType);

                return mesh;
            }

            /// <summary>
            /// Creates a cylinder primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="geometryType">Defines geometry type (solid or outline)</param>
            /// <param name="height">The height.</param>
            /// <param name="topDiameter">Diameter of the top side</param>
            /// <param name="bottomDiameter">Diameter of the bottom side</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="transform">Transform matrix</param>
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
                Matrix4x4? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, height, topDiameter, bottomDiameter, tessellation, transform);
                // Create the primitive object.
                return new Shape(device, geometry);
            }
        }
    }
}
