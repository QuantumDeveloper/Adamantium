using System;
using Adamantium.Engine.Graphics;
using Buffer = Adamantium.Engine.Graphics.Buffer;


namespace Adamantium.EntityFramework.Components
{
    public class SkinnedMeshRenderer : MeshRendererBase
    {
        public SkinnedMeshRenderer()
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InputLayout = VertexInputLayout.New<SkinnedMeshVertex>(0);
        }

        protected override unsafe bool Update(D3DGraphicsDevice graphicsContext)
        {
            if (MeshData != null && (MeshData.Mesh.IsModified || MeshDataChanged))
            {
                var vertices = ToSkinnedMeshVertices(MeshData.Mesh);
                if (vertices == null)
                {
                    return false;
                }

                if (VertexBuffer != null && vertices.Length <= VertexBuffer.ElementCount)
                {
                    //GCHandle handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                    //UpdateVertexBuffer(graphicsContext, handle.AddrOfPinnedObject(), Utilities.SizeOf<MeshVertex>());
                    //handle.Free();
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
                    VertexBuffer = ToDispose(Buffer.Vertex.New<SkinnedMeshVertex>(graphicsContext, vertices, ResourceUsage.Dynamic));
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
