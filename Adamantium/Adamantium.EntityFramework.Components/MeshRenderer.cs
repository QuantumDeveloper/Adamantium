using System;
using Adamantium.Engine.Graphics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public class MeshRenderer : MeshRendererBase
    {
        public MeshRenderer()
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InputLayout = VertexInputLayout.New<MeshVertex>(0);
        }

        protected override unsafe bool Update(GraphicsDevice graphicsContext)
        {
            if (MeshData == null)
            {
                Initialize();
            }

            if (MeshData != null && (MeshData.Mesh.IsModified || MeshDataChanged))
            {
                var vertices = ToMeshVertices(MeshData.Mesh);

                if (vertices == null)
                {
                    return false;
                }

                if (VertexBuffer != null && vertices.Length <= VertexBuffer.ElementCount)
                {
                    if (IntPtr.Size == 8)
                    {
                        // Perform the update of all vertices on a temporary buffer
                        fixed (void* fromPtr = vertices)
                        {
                            // Then copy this buffer in one shot
                            VertexBuffer.SetData(graphicsContext, new DataPointer((IntPtr)fromPtr, vertices.Length * Utilities.SizeOf<MeshVertex>()));
                        }
                    }
                    else
                    {
                        VertexBuffer.SetData(graphicsContext, vertices);
                    }
                }
                else
                {
                    VertexBuffer?.Dispose();
                    VertexBuffer = ToDispose(Buffer.Vertex.New<MeshVertex>(graphicsContext, vertices, ResourceUsage.Dynamic));
                }

                if (MeshData.Mesh.Indices.Length > 0)
                {
                    IndexBuffer?.Dispose();
                    IndexBuffer = ToDispose(Buffer.Index.New(graphicsContext, MeshData.Mesh.Indices, ResourceUsage.Dynamic));
                }

                MeshData.Mesh.AcceptChanges();
            }
            return true;
        }
    }
}
