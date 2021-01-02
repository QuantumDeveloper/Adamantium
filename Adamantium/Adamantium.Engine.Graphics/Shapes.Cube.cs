using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Cube
        {
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                float width = 1,
                float height = 1,
                float length = 1,
                int tessellation = 1,
                Matrix4x4F? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, width, height, length, tessellation, transform);
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float size,
                int tessellation = 1,
                Matrix4x4F? transform = null)
            {
                return GenerateGeometry(geometryType, size, size, size, tessellation, transform);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                float width = 1,
                float height = 1,
                float depth = 1,
                int tessellation = 1,
                Matrix4x4F? transform = null)
            {
                if (width <= 0)
                {
                    width = 0.01f;
                }

                if (height <= 0)
                {
                    height = 0.01f;
                }

                if (depth <= 0)
                {
                    depth = 0.01f;
                }

                if (tessellation < 1)
                {
                    tessellation = 1;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(width, height, depth, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(width, height, depth);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                float width = 1,
                float height = 1,
                float depth = 1,
                int tessellation = 1)
            {
                Vector2F uvFactor = Vector2F.One;
                var lineWidth = tessellation + 1;
                int quads = (tessellation * tessellation);
                var indices = new List<int>(quads * 36);

                var sizeX = width / 2;
                var sizeY = height / 2;
                var sizeZ = depth / 2;

                var deltaX = width / tessellation;
                var deltaY = height / tessellation;
                var deltaZ = depth / tessellation;

                List<Vector3F> front = new List<Vector3F>();
                List<Vector3F> right = new List<Vector3F>();
                List<Vector3F> back = new List<Vector3F>();
                List<Vector3F> left = new List<Vector3F>();
                List<Vector3F> top = new List<Vector3F>();
                List<Vector3F> bottom = new List<Vector3F>();

                List<Vector2F> frontUV = new List<Vector2F>();
                List<Vector2F> rightUV = new List<Vector2F>();
                List<Vector2F> backUV = new List<Vector2F>();
                List<Vector2F> leftUV = new List<Vector2F>();
                List<Vector2F> topUV = new List<Vector2F>();
                List<Vector2F> bottomUV = new List<Vector2F>();

                //Generate frame
                for (int y = 0; y < lineWidth; y++)
                {
                    for (int x = 0; x < lineWidth; x++)
                    {
                        Vector3F pos = new Vector3F(-sizeX + deltaX * x, -sizeY + deltaY * y, -sizeZ);
                        var uv = new Vector2F(1.0f - (1.0f * x / tessellation * uvFactor.X), 1.0f - (1.0f * y / tessellation * uvFactor.Y));
                        front.Add(pos);
                        frontUV.Add(uv);
                    }

                    for (int z = 0; z < lineWidth; z++)
                    {
                        Vector3F pos = new Vector3F(sizeX, -sizeY + deltaY * y, -sizeZ + deltaZ * z);
                        var uv = new Vector2F(1.0f - (1.0f * z / tessellation * uvFactor.X), 1.0f - (1.0f * y / tessellation * uvFactor.Y));
                        right.Add(pos);
                        rightUV.Add(uv);
                    }

                    for (int x = 0; x < lineWidth; x++)
                    {
                        Vector3F pos = new Vector3F(sizeX - deltaX * x, -sizeY + deltaY * y, sizeZ);
                        var uv = new Vector2F(1.0f - (1.0f * x / tessellation * uvFactor.X), 1.0f - (1.0f * y / tessellation * uvFactor.Y));
                        back.Add(pos);
                        backUV.Add(uv);
                    }

                    for (int z = 0; z < lineWidth; z++)
                    {
                        Vector3F pos = new Vector3F(-sizeX, -sizeY + deltaY * y, sizeZ - deltaZ * z);
                        var uv = new Vector2F(1.0f - (1.0f * z / tessellation * uvFactor.X), 1.0f - (1.0f * y / tessellation * uvFactor.Y));
                        left.Add(pos);
                        leftUV.Add(uv);
                    }
                }

                //Generate top cap
                for (int z = 0; z < lineWidth; z++)
                {
                    for (int x = 0; x < lineWidth; x++)
                    {
                        Vector3F pos = new Vector3F(-sizeX + deltaX * x, sizeY, -sizeZ + deltaZ * z);
                        var uv = new Vector2F( 1.0f - (1.0f * x / tessellation * uvFactor.X), 1.0f - (1.0f * z / tessellation * uvFactor.Y));
                        top.Add(pos);
                        topUV.Add(uv);
                    }
                }

                QuaternionF rot = QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(180));
                Matrix4x4F rotationMatrix = Matrix4x4F.RotationQuaternion(rot);
                //Generate bottom cap
                for (int z = 0; z < lineWidth; z++)
                {
                    for (int x = 0; x < lineWidth; x++)
                    {
                        Vector3F pos = new Vector3F(-sizeX + deltaX * x, sizeY, -sizeZ + deltaZ * z);
                        var uv = new Vector2F( 1.0f - (1.0f * x / tessellation * uvFactor.X), 1.0f - (1.0f * z / tessellation * uvFactor.Y));
                        pos = Vector3F.TransformCoordinate(pos, rotationMatrix);
                        bottom.Add(pos);
                        bottomUV.Add(uv);
                    }
                }

                var allVertices = new List<Vector3F>();

                allVertices.AddRange(front);
                allVertices.AddRange(right);
                allVertices.AddRange(back);
                allVertices.AddRange(left);
                allVertices.AddRange(top);
                allVertices.AddRange(bottom);

                var allUVS = new List<Vector2F>();
                allUVS.AddRange(frontUV);
                allUVS.AddRange(rightUV);
                allUVS.AddRange(backUV);
                allUVS.AddRange(leftUV);
                allUVS.AddRange(topUV);
                allUVS.AddRange(bottomUV);

                // Create indices
                /*
                *   0    1
                *
                *   2    3
                */

                var vertexStart = 0;
                for (int i = 0; i < 6; i++)
                {
                    for (int z = 0; z < tessellation; z++)
                    {
                        for (int x = 0; x < tessellation; x++)
                        {
                            // Six indices (two triangles) per face.
                            //1st triangle 
                            int vbase = lineWidth * z + x;
                            indices.Add(vbase + vertexStart);
                            indices.Add(vbase + 1 + vertexStart);
                            indices.Add(vbase + tessellation + 2 + vertexStart);

                            //2nd triangle
                            indices.Add(vbase + vertexStart);
                            indices.Add(vbase + tessellation + 2 + vertexStart);
                            indices.Add(vbase + tessellation + 1 + vertexStart);
                        }
                    }
                    vertexStart += lineWidth * lineWidth;
                }

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.TriangleList).
                    SetPositions(allVertices).
                    SetUVs(0, allUVS).
                    SetIndices(indices).
                    CalculateNormals();

                return mesh;
            }


            private static Mesh GenerateOutlinedGeometry(
                float width = 1,
                float height = 1,
                float depth = 1)
            {
                var startPositionX = -width / 2;
                var startPositionY = -height / 2;
                var startPositionZ = -depth / 2;

                var endPositionX = width / 2;
                var endPositionY = height / 2;
                var endPositionZ = depth / 2;

                var vertices = new List<Vector3F>();
                vertices.Add(new Vector3F(startPositionX, startPositionY, startPositionZ));
                vertices.Add(new Vector3F(startPositionX, endPositionY, startPositionZ));
                vertices.Add(new Vector3F(endPositionX, endPositionY, startPositionZ));
                vertices.Add(new Vector3F(endPositionX, startPositionY, startPositionZ));
                vertices.Add(new Vector3F(startPositionX, startPositionY, endPositionZ));
                vertices.Add(new Vector3F(startPositionX, endPositionY, endPositionZ));
                vertices.Add(new Vector3F(endPositionX, endPositionY, endPositionZ));
                vertices.Add(new Vector3F(endPositionX, startPositionY, endPositionZ));

                List<int> indices = new List<int>();

                for (int i = 0; i < 8; i++)
                {
                    indices.Add(i);
                }
                indices.Add(4);

                indices.Insert(4, 0);
                indices.Add(Shape.PrimitiveRestartValue);
                indices.Add(1);
                indices.Add(5);
                indices.Add(Shape.PrimitiveRestartValue);
                indices.Add(2);
                indices.Add(6);
                indices.Add(Shape.PrimitiveRestartValue);
                indices.Add(3);
                indices.Add(7);

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPositions(vertices).
                    SetIndices(indices);

                return mesh;
            }
        }
    }
}
