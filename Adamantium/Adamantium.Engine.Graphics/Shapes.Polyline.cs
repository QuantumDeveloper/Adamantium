using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Polyline
        {
            public static Mesh GenerateGeometry(Vector2F[] points, float length, float thickness, Matrix4x4F? transform = null)
            {
                PrimitiveType primitiveType = PrimitiveType.TriangleStrip;
                var vertexArray = new List<Vector3F>();
                var indicesArray = new List<int>();
                var interrupt = -1;
                var lastIndex = 0;

                List<Vector2F> uvs = new List<Vector2F>();

                for (int i = 0; i < points.Length - 1; ++i)
                {
                    var endPoint = points[i + 1];
                    var startPoint = points[i];

                    var direction = endPoint - startPoint;
                    var normal = new Vector2F(-direction.Y, direction.X);
                    normal.Normalize();
                    // if p0 = point 1 - normal, then rectangle will be drawn above the line
                    //else if p0 = point 1 + normal, then rectangle will be drawn under the line
                    var p0 = startPoint - normal * thickness;
                    var p1 = endPoint - normal * thickness;

                    //top left corner
                    vertexArray.Add(new Vector3F(startPoint, 0));
                    //top right corner
                    vertexArray.Add(new Vector3F(endPoint, 0));
                    //Bottom left corner
                    vertexArray.Add(new Vector3F(p0, 0));
                    //Bottom right corner
                    vertexArray.Add(new Vector3F(p1, 0));

                    uvs.Add(Vector2F.Zero);
                    uvs.Add(Vector2F.UnitX);
                    uvs.Add(Vector2F.UnitY);
                    uvs.Add(Vector2F.One);

                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(interrupt);

                }

                var mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(vertexArray);
                mesh.SetUVs(0, uvs);
                mesh.SetIndices(indicesArray);

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;

            }
        }
    }
}