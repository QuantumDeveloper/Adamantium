using Adamantium.Graphics;
using Adamantium.Graphics.Core;

namespace Adamantium.ECS.Components
{
    public class SkinnedMeshRenderer : MeshRendererBase
    {
        public SkinnedMeshRenderer()
        {
            VertexType = typeof(SkinnedMeshVertex);
        }

        protected override bool Update(IGraphicsDevice graphicsContext)
        {
            if (MeshData == null)
            {
                Initialize();
            }

            if (MeshData == null || (!MeshData.Mesh.IsModified && !MeshDataChanged)) return true;
            
            var vertices = MeshData.Mesh.ToSkinnedMeshVertices();
            
            return UpdateBuffers(graphicsContext, vertices);
        }
    }
}
