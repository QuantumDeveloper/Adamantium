using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public class SkinnedMeshRenderer : MeshRendererBase
    {
        public SkinnedMeshRenderer()
        {
            VertexType = typeof(SkinnedMeshVertex);
        }

        protected override bool Update(GraphicsDevice graphicsContext)
        {
            if (MeshData == null)
            {
                Initialize();
            }

            if (MeshData == null || (!MeshData.Mesh.IsModified && !MeshDataChanged)) return true;
            
            var vertices = ToSkinnedMeshVertices(MeshData.Mesh);
            
            return UpdateBuffers(graphicsContext, vertices);
        }
    }
}
