using System;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics.Effects;

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
            var vertices = mesh.ToMeshVertices();

            VertexBuffer = ToDispose(Buffer.Vertex.New(device, vertices));
            IndexBuffer = ToDispose(Buffer.Index.New(device, mesh.Indices));

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
