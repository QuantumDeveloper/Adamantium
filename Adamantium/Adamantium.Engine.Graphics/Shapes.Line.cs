using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Line
        {
            public static Mesh GenerateGeometry(GeometryType geometryType, Vector3F startPoint, Vector3F endPoint, float thickness, Matrix4x4F? transform = null, bool toRightHanded = false)
            {
                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(startPoint, endPoint, thickness, toRightHanded);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(startPoint, endPoint);
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }

            public static Mesh GenerateSolidGeometry(Vector3F startPoint, Vector3F endPoint, float thickness, bool toRightHanded = false)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleStrip;

                var vertexArray = new List<Vector3F>();
                var indicesArray = new List<int>();
                var interrupt = -1;
                var lastIndex = 0;

                var direction = endPoint - startPoint;
                var normal = new Vector3F(-direction.Y, direction.X);
                normal.Normalize();
                var p0 = startPoint - normal * (thickness * 0.5f);
                var p1 = endPoint - normal * (thickness * 0.5f);
                var p2 = startPoint + normal * (thickness * 0.5f);
                var p3 = endPoint + normal * (thickness * 0.5f);

                //top left corner
                vertexArray.Add(p0);
                //top right corner
                vertexArray.Add(p1);
                //Bottom left corner
                vertexArray.Add(p2);
                //Bottom right corner
                vertexArray.Add(p3);

                List<Vector2F> uvs = new List<Vector2F>();
                uvs.Add(Vector2F.Zero);
                uvs.Add(Vector2F.UnitX);
                uvs.Add(Vector2F.UnitY);
                uvs.Add(Vector2F.One);

                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(interrupt);

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(vertexArray);
                mesh.SetUVs(0, uvs);
                mesh.SetIndices(indicesArray);
                mesh.CalculateNormals();

                return mesh;
            }

            public static Mesh GenerateOutlinedGeometry(Vector3F startPoint, Vector3F endPoint)
            {
                PrimitiveType primitiveType = PrimitiveType.LineStrip;

                Vector3F[] positions = new Vector3F[2];
                positions[0] = startPoint;
                positions[1] = endPoint;

                Mesh mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(positions);

                return mesh;
            }

            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                Vector3F startPoint,
                Vector3F endPoint,
                float thickness,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var geometry = GenerateGeometry(geometryType, startPoint, endPoint, thickness, transform, toRightHanded);
                return new Shape(device, geometry);
            }
        }
    }
}
