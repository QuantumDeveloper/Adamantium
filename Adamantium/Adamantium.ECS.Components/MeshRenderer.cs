using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Vertices;

namespace Adamantium.ECS.Components
{
    public class MeshRenderer : MeshRendererBase
    {
        public MeshRenderer()
        {
            VertexType = typeof(MeshVertex);
        }

        protected override bool Update(IGraphicsDevice graphicsContext)
        {
            if (MeshData == null)
            {
                Initialize();
            }

            if (MeshData == null || (!MeshData.Mesh.IsModified && !MeshDataChanged)) return true;
            
            var vertices = MeshData.Mesh.ToMeshVertices();

            return UpdateBuffers(graphicsContext, vertices);
        }
    }
}
