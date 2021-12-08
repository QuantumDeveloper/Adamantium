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
                Vector2F diameter,
                int tessellation = 36,
                Matrix4x4F? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, diameter, tessellation, transform);
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                Vector2F diameter,
                int tessellation = 40,
                Matrix4x4F? transform = null)
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
                List<Vector3F> vertices = new List<Vector3F>();
                List<Vector2F> uvs = new List<Vector2F>();
                List<int> indices = new List<int>();
                Vector3F center = Vector3F.Zero;

                for (int i = 0; i < tessellation; ++i)
                {
                    float angle = (float)Math.PI * 2 / tessellation * i;

                    float x = center.X + diameter.X * (float)Math.Cos(angle);
                    float y = center.Y + diameter.Y * (float)Math.Sin(angle);
                    vertices.Add(new Vector3F(x, y, 0));
                    var uv = new Vector2F(
                       0.5f + (center.X - x) / (2 * diameter.X),
                       0.5f + (center.Y - y) / (2 * diameter.Y));
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

                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                Vector3F center = Vector3F.Zero;
                int lastIndex = 0;

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float)Math.PI * 2 / tessellation * i;

                    float x = center.X + diameter.X * (float)Math.Cos(angle);
                    float y = center.Y + diameter.Y * (float)Math.Sin(angle);
                    vertices.Add(new Vector3F(x, y, 0));
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
