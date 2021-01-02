using System;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class Shape : DisposableObject
    {
        public static readonly int PrimitiveRestartValue = -1;

        public Buffer<MeshVertex> VertexBuffer { get; private set; }

        public Buffer<Int32> IndexBuffer { get; private set; }

        public static Type VertexType { get; }

        public PrimitiveType PrimitiveType { get; private set; }
        
        public Mesh Mesh { get; }

        protected readonly GraphicsDevice GraphicsDevice;

        static Shape()
        {
            VertexType = typeof(MeshVertex);
        }

        public Shape(GraphicsDevice device, Mesh mesh)
        {
            Mesh = mesh;
            GraphicsDevice = device;

            int length = mesh.Positions.Length;

            var vertices = new MeshVertex[length];

            for (int i = 0; i < length; i++)
            {
                var vertex = new MeshVertex() { Position = mesh.Positions[i] };
                if (mesh.IsNormalsPresent)
                {
                    vertex.Normal = mesh.Normals[i];
                }
                if (mesh.IsUv0Present)
                {
                    vertex.UV0 = mesh.UV0[i];
                }
                if (mesh.IsUv1Present)
                {
                    vertex.UV1 = mesh.UV1[i];
                }
                if (mesh.IsUv2Present)
                {
                    vertex.UV2 = mesh.UV2[i];
                }
                if (mesh.IsUv3Present)
                {
                    vertex.UV3 = mesh.UV3[i];
                }
                if (mesh.IsColorPresent)
                {
                    vertex.Color = mesh.Colors[i].ToVector4();
                }
                if (mesh.IsTangetBinormalsPresent)
                {
                    vertex.Tangent = mesh.Tangents[i];
                    vertex.BiTangent = mesh.BiTangents[i];
                }
                
                vertices[i] = vertex;
            }

            VertexBuffer = ToDispose(Buffer.Vertex.New(device.MainDevice, vertices));
            IndexBuffer = ToDispose(Buffer.Index.New(device.MainDevice, mesh.Indices));

            PrimitiveType = mesh.MeshTopology;
        }

        public virtual void Draw(EffectPass pass = null)
        {
            pass?.Apply();
            
            GraphicsDevice.VertexType = VertexType;
            GraphicsDevice.PrimitiveTopology = PrimitiveType;
            GraphicsDevice.DrawIndexed(VertexBuffer, IndexBuffer);
        }
    }
}
