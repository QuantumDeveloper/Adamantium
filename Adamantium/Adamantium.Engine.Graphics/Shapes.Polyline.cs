using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        // TODO: this class should be improved to make seamless transitions between points
        // TODO: integrate StrokeGeometry in Graphics layer
        public class Polyline
        {
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                Vector2[] points,
                float thickness,
                Matrix4x4? transform = null
            )
            {
                var mesh = GenerateGeometry(points, thickness, transform);
                return new Shape(device, mesh);
            }
            
            public static Mesh GenerateGeometry(Vector2[] points, float thickness, Matrix4x4? transform = null)
            {
                var primitiveType = PrimitiveType.TriangleStrip;
                var vertexArray = new List<Vector3>();
                var indicesArray = new List<int>();
                var interrupt = -1;
                var lastIndex = 0;

                List<Vector2F> uvs = new List<Vector2F>();

                for (int i = 0; i < points.Length - 1; ++i)
                {
                    var endPoint = points[i + 1];
                    var startPoint = points[i];

                    var direction = endPoint - startPoint;
                    var normal = new Vector2(-direction.Y, direction.X);
                    normal.Normalize();
                    // if p0 = point1 - normal, then rectangle will be drawn above the line
                    //else if p0 = point1 + normal, then rectangle will be drawn under the line
                    var p0 = startPoint - normal * thickness;
                    var p1 = endPoint - normal * thickness;

                    //top left corner
                    vertexArray.Add(new Vector3(startPoint, 0));
                    //top right corner
                    vertexArray.Add(new Vector3(endPoint, 0));
                    //Bottom left corner
                    vertexArray.Add(new Vector3(p0, 0));
                    //Bottom right corner
                    vertexArray.Add(new Vector3(p1, 0));

                    uvs.Add(Vector2F.UnitX);
                    uvs.Add(Vector2F.Zero);
                    
                    uvs.Add(Vector2F.UnitY);
                    uvs.Add(Vector2F.One);

                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(lastIndex++);
                    indicesArray.Add(interrupt);
                }

                var mesh = new Mesh();
                mesh.SetTopology(primitiveType).
                    SetPoints(vertexArray).
                    SetUVs(0, uvs).
                    SetIndices(indicesArray).
                    ApplyTransform(transform);

                return mesh;

            }
        }
    }
}