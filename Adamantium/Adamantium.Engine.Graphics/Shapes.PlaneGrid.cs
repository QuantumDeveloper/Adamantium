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
                double width,
                double length,
                int tessellation = 1,
                Matrix4x4? transform = null)
            {
                var primitiveType = PrimitiveType.LineList;

                var lineList = new List<Vector3>();
                var lines = tessellation + 1;
                var halfSizeX = width / 2;
                var halfSizeZ = length / 2;

                var deltaX = width / tessellation;
                var deltaZ = length / tessellation;

                for (int i = 0; i < lines; i++)
                {
                    var lineStart = new Vector3(-halfSizeX + deltaX*i, 0, -halfSizeZ);
                    var lineEnd = new Vector3(-halfSizeX + deltaX*i, 0, halfSizeZ);
                    lineList.Add(lineStart);
                    lineList.Add(lineEnd);
                }

                for (int i = 0; i < lines; i++)
                {
                    var lineStart = new Vector3(-halfSizeX, 0, -halfSizeZ + deltaZ*i);
                    var lineEnd = new Vector3(halfSizeX, 0, -halfSizeZ + deltaZ*i);
                    lineList.Add(lineStart);
                    lineList.Add(lineEnd);
                }

                Mesh mesh = new Mesh();
                mesh.SetTopology(primitiveType).SetPoints(lineList).ApplyTransform(transform);
                return mesh;
            }
        }
    }
}
