using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Torus
        {
            public static readonly PrimitiveType Topology = PrimitiveType.TriangleList;

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float diameter = 1.0f,
                float thickness = 0.33333f,
                int tessellation = 32,
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
                    mesh = GenerateSolidGeometry(diameter, thickness, tessellation, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(diameter, thickness, tessellation);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }


            private static Mesh GenerateSolidGeometry(
                float diameter = 1.0f,
                float thickness = 0.33333f,
                int tessellation = 32,
                bool toRightHanded = false)
            {
                var vertices = new List<Vector3F>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();

                int stride = tessellation + 1;

                // First we loop around the main ring of the torus.
                for (int i = 0; i <= tessellation; i++)
                {
                    float u = (float)i / tessellation;

                    float outerAngle = i * MathHelper.TwoPi / tessellation - MathHelper.PiOverTwo;

                    //Create a transform matrix that will align geometry to
                    //slice perperndicularly though the current ring position
                    var matrix = Matrix4x4F.Translation(diameter / 2, 0, 0) * Matrix4x4F.RotationY(outerAngle);

                    for (int j = 0; j <= tessellation; j++)
                    {
                        float v = 1 - (float)j / tessellation;

                        float innerAngle = j * MathHelper.TwoPi / tessellation + MathHelper.Pi;
                        float dx = (float)Math.Cos(innerAngle),
                            dy = (float)Math.Sin(innerAngle);

                        //Create a vertex
                        var normal = new Vector3F(dx, dy, 0);
                        var position = normal * thickness / 2;
                        var uv = new Vector2F(u, 1.0f - v);

                        Vector3F.TransformCoordinate(ref position, ref matrix, out position);
                        //Vector3F.TransformNormal(ref normal, ref matrix, out normal);

                        vertices.Add(position);
                        uvs.Add(uv);

                        //Create indices for 2 triangles
                        int nextI = (i + 1) % stride;
                        int nextJ = (j + 1) % stride;

                        indices.Add(i * stride + j);
                        indices.Add(nextI * stride + j);
                        indices.Add(i * stride + nextJ);

                        indices.Add(i * stride + nextJ);
                        indices.Add(nextI * stride + j);
                        indices.Add(nextI * stride + nextJ);

                    }
                }

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.TriangleList;
                mesh.SetPositions(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetIndices(indices);
                if (toRightHanded)
                {
                    mesh.ReverseWinding();
                }
                mesh.CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                float diameter = 1.0f,
                float thickness = 0.33333f,
                int tessellation = 32)
            {
                //var vertices = new List<Vector3F>();
                var radius = diameter / 2;
                var meshes = new List<Mesh>();
                for (int i = 0; i <= tessellation; i++)
                {
                    var meshItem = Polygon.GenerateGeometry(GeometryType.Outlined, new Vector2F(thickness/2), tessellation);
                    var angle = (float)(i * 2.0 * Math.PI / tessellation);
                    var normal = GetCircleVector(i, tessellation);
                    var matrix = Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90)) * Matrix4x4F.RotationY(angle) * Matrix4x4F.Translation(normal*radius);
                    meshItem.ApplyTransform(matrix);
                    meshes.Add(meshItem);
                }

                var vertices = new List<Vector3F>();
                var indices = new List<int>();
                int lastIndex = 0;
                for (int i = 0; i <= tessellation; ++i)
                {
                    for (int j = 0; j < meshes.Count; ++j)
                    {
                        vertices.Add(meshes[j].Positions[i]);
                        indices.Add(lastIndex++);
                    }
                    indices.Add(Shape.StripSeparatorValue);
                }
                var m = new Mesh();
                m.SetPositions(vertices);
                m.SetIndices(indices);
                meshes.Add(m);

                var mesh = new Mesh();
                mesh.MeshTopology = PrimitiveType.LineStrip;
                mesh.Merge(meshes.ToArray());
                
                return mesh;
            }

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


            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float diameter = 1.0f,
                float thickness = 0.33333f,
                int tessellation = 32,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var geometry = GenerateGeometry(geometryType, diameter, thickness, tessellation, transform, toRightHanded);
                return new Shape(device, geometry);
            }
        }
    }
}
