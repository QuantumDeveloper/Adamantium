﻿using Adamantium.Engine.Graphics;

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
            
            var vertices = MeshData.Mesh.ToSkinnedMeshVertices();
            
            return UpdateBuffers(graphicsContext, vertices);
        }
    }
}
