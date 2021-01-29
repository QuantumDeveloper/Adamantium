using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public class MeshRenderer : MeshRendererBase
    {
        public MeshRenderer()
        {
            VertexType = typeof(MeshVertex);
        }

        protected override bool Update(GraphicsDevice graphicsContext)
        {
            if (MeshData == null)
            {
                Initialize();
            }

            if (MeshData == null || (!MeshData.Mesh.IsModified && !MeshDataChanged)) return true;
            
            var vertices = ToMeshVertices(MeshData.Mesh);

            return UpdateBuffers(graphicsContext, vertices);
        }
    }
}
