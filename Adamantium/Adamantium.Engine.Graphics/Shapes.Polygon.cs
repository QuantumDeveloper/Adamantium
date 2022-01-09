using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Polygon
        {
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                Vector2 diameter,
                int tessellation = 36,
                Matrix4x4? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, diameter, tessellation, transform);
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                Vector2 diameter,
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
                    mesh = GenerateSolidGeometry(diameter, tessellation);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(diameter, tessellation);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            private static Mesh GenerateSolidGeometry(Vector2F diameter, int tessellation = 36)
            {
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2F>();
                var indices = new List<int>();
                var center = Vector3.Zero;

                for (int i = 0; i < tessellation; ++i)
                {
                    float angle = (float)Math.PI * 2 / tessellation * i;

                    var x = center.X + diameter.X * (float)Math.Cos(angle);
                    var y = center.Y + diameter.Y * (float)Math.Sin(angle);
                    vertices.Add(new Vector3(x, y, 0));
                    var uv = new Vector2F(
                       0.5f + (float)((center.X - x) / (2 * diameter.X)),
                       0.5f + (float)((center.Y - y) / (2 * diameter.Y)));
                    uvs.Add(uv);
                }

                int basicIndex = 0;

                for (int i = 0; i < tessellation-2; i++)
                {
                    indices.Add(basicIndex);
                    indices.Add(i + 1);
                    indices.Add(i + 2);
                }

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.TriangleList).
                    SetPoints(vertices).
                    SetUVs(0, uvs).
                    SetIndices(indices).
                    CalculateNormals();

                return mesh;
            }

            private static Mesh GenerateOutlinedGeometry(
                Vector2F diameter,
                int tessellation = 36)
            {
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                var center = Vector3.Zero;
                int lastIndex = 0;

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float)Math.PI * 2 / tessellation * i;

                    var x = center.X + diameter.X * (float)Math.Cos(angle);
                    var y = center.Y + diameter.Y * (float)Math.Sin(angle);
                    vertices.Add(new Vector3(x, y, 0));
                    indices.Add(lastIndex++);
                }

                var mesh = new Mesh();
                mesh.SetTopology(PrimitiveType.LineStrip).
                    SetPoints(vertices).
                    SetIndices(indices);

                return mesh;
            }

        }

    }
}
