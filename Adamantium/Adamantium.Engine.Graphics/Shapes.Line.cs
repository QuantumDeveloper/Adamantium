﻿using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Line
        {
            public static Mesh GenerateGeometry(
                GeometryType geometryType, 
                Vector3 startPoint, 
                Vector3 endPoint, 
                double thickness, 
                Matrix4x4? transform = null)
            {
                Mesh mesh;
                if (geometryType == GeometryType.Solid)
                {
                    mesh = GenerateSolidGeometry(startPoint, endPoint, thickness);
                }
                else
                {
                    mesh = GenerateOutlinedGeometry(startPoint, endPoint);
                }

                mesh.ApplyTransform(transform);

                return mesh;
            }

            public static Mesh GenerateSolidGeometry(Vector3 startPoint, Vector3 endPoint, double thickness)
            {
                var primitiveType = PrimitiveType.TriangleStrip;

                var vertexArray = new List<Vector3>();
                var indicesArray = new List<int>();
                var interrupt = -1;
                var lastIndex = 0;

                var direction = endPoint - startPoint;
                var normal = new Vector3(-direction.Y, direction.X);
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
                uvs.Add(Vector2F.One);
                uvs.Add(Vector2F.UnitY);
                uvs.Add(Vector2F.UnitX);
                uvs.Add(Vector2F.Zero);
                
                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(lastIndex++);
                indicesArray.Add(interrupt);

                var mesh = new Mesh();
                mesh.SetTopology(primitiveType).
                    SetPoints(vertexArray).
                    SetUVs(0, uvs).
                    SetIndices(indicesArray).
                    CalculateNormals();

                return mesh;
            }

            public static Mesh GenerateOutlinedGeometry(Vector3 startPoint, Vector3 endPoint)
            {
                var primitiveType = PrimitiveType.LineStrip;

                var positions = new Vector3[2];
                positions[0] = startPoint;
                positions[1] = endPoint;

                int[] indices = {0, 1};

                Mesh mesh = new Mesh();
                mesh.SetTopology(primitiveType).SetPoints(positions).SetIndices(indices);

                return mesh;
            }

            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                Vector3 startPoint,
                Vector3 endPoint,
                float thickness,
                Matrix4x4? transform = null)
            {
                var geometry = GenerateGeometry(geometryType, startPoint, endPoint, thickness, transform);
                return new Shape(device, geometry);
            }
        }
    }
}
