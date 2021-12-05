using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class PlaneGrid
        {
            public static Mesh GenerateGeometry(
                float width,
                float length,
                int tessellation = 1,
                Matrix4x4F? transform = null)
            {
                PrimitiveType primitiveType = PrimitiveType.LineList;

                List<Vector3F> lineList = new List<Vector3F>();
                var lines = tessellation + 1;
                var halfSizeX = width / 2;
                var halfSizeZ = length / 2;

                var deltaX = width / tessellation;
                var deltaZ = length / tessellation;

                for (int i = 0; i < lines; i++)
                {
                    Vector3F lineStart = new Vector3F(-halfSizeX + deltaX*i, 0, -halfSizeZ);
                    Vector3F lineEnd = new Vector3F(-halfSizeX + deltaX*i, 0, halfSizeZ);
                    lineList.Add(lineStart);
                    lineList.Add(lineEnd);
                }

                for (int i = 0; i < lines; i++)
                {
                    Vector3F lineStart = new Vector3F(-halfSizeX, 0, -halfSizeZ + deltaZ*i);
                    Vector3F lineEnd = new Vector3F(halfSizeX, 0, -halfSizeZ + deltaZ*i);
                    lineList.Add(lineStart);
                    lineList.Add(lineEnd);
                }

                Mesh mesh = new Mesh();
                mesh.SetTopology(primitiveType).SetPoints(lineList);

                mesh.ApplyTransform(transform);

                return mesh;
            }
        }
    }
}
