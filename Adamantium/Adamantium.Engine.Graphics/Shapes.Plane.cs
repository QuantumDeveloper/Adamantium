using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;


namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Plane
        {
            public static readonly PrimitiveType PrimitiveType = PrimitiveType.TriangleList;

            public static Mesh GenerateGeometry(
                GeometryType geometryType, 
                float width, 
                float length, 
                int tessellation = 1, 
                Matrix4x4F? transform = null)
            {
                return GenerateGeometry(geometryType, width, length, tessellation, Vector2F.One, transform);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType, 
                float width, 
                float length, 
                int tessellation, 
                Vector2F uvFactor, 
                Matrix4x4F? transform = null)
            {
                if (width <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(width), "width parameter must be more than 0");
                }

                if (length <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(length), "length parameter must be more than 0");
                }

                if (tessellation < 1)
                {
                    tessellation = 1;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(width, length, tessellation, uvFactor);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(width, length);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(
                float width,
                float length,
                int tessellation,
                Vector2F uvFactor)
            {
                var lineWidth = tessellation + 1;
                var vertices = new Vector3F[lineWidth * lineWidth];
                var uvs = new Vector2F[lineWidth * lineWidth];

                var sizeX = width / 2;
                var sizeZ = length / 2;

                var deltaX = width / tessellation;
                var deltaZ = length / tessellation;

                int vertexCount = 0;

                for (int z = 0; z < lineWidth; ++z)
                {
                    for (int x = 0; x < lineWidth; ++x)
                    {
                        Vector3F pos = new Vector3F(-sizeX + deltaX * x, 0, -sizeZ + deltaZ * z);
                        var uv = new Vector2F(1.0f - (1.0f * x / tessellation * uvFactor.X), 1.0f * z / tessellation * uvFactor.Y);
                        vertices[vertexCount] = pos;
                        uvs[vertexCount] = uv;
                        vertexCount++;
                    }
                }

                // Create indices
                /*
                   *   1    2
                   *
                   *   0    3
                */
                int indexCount = 0;
                var indices = new int[tessellation * tessellation * 6];
                //LeftHanded
                for (int z = 0; z < tessellation; z++)
                {
                    for (int x = 0; x < tessellation; x++)
                    {
                        // Six indices (two triangles) per face.
                        //1st triangle 
                        int vbase = lineWidth * z + x;
                        indices[indexCount++] = vbase;
                        indices[indexCount++] = vbase + lineWidth;
                        indices[indexCount++] = vbase + lineWidth + 1;

                        //2nd triangle
                        indices[indexCount++] = vbase;
                        indices[indexCount++] = vbase + lineWidth + 1;
                        indices[indexCount++] = vbase + 1;
                    }
                }

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType).
                    SetPoints(vertices).
                    SetUVs(0, uvs).
                    SetIndices(indices).
                    CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                float width,
                float length)
            {
                var startPositionX = width / 2;
                var startPositionY = length / 2;

                var vertices = new List<Vector3F>();
                vertices.Add(new Vector3F(-startPositionX, -startPositionY, 0));
                vertices.Add(new Vector3F(startPositionX, -startPositionY, 0));
                vertices.Add(new Vector3F(startPositionX, startPositionY, 0));
                vertices.Add(new Vector3F(-startPositionX, startPositionY, 0));

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPoints(vertices).
                    GenerateBasicIndices();
                
                return mesh;
            }

            public static Shape New(
                GraphicsDevice device, 
                GeometryType geometryType, 
                float width, 
                float length, 
                int tessellation, 
                Vector2F uvFactor, 
                Matrix4x4F? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, width, length, tessellation, uvFactor, transform);
                return new Shape(device, geometry);
            }

        }
    }
}

