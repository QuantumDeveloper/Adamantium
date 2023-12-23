using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    [RequiredComponets(typeof(MeshData))]
    public abstract class MeshRendererBase : RenderComponent
    {
        private bool isWireFrame;

        protected Buffer VertexBuffer { get; set; }

        protected MeshData MeshData { get; set; }

        protected bool MeshDataChanged { get; set; }

        public bool IsWireFrame
        {
            get => isWireFrame;
            set => SetProperty(ref isWireFrame, value);
        }

        public override void Initialize()
        {
            var meshData = GetComponent<MeshData>();
            if (meshData == null) return;
            
            MeshData = meshData;
            MeshDataChanged = true;
            MeshData.MeshDataChanged += MeshData_MeshDataChanged;
            base.Initialize();
        }

        private void MeshData_MeshDataChanged(object sender, EventArgs e)
        {
            MeshDataChanged = true;
        }

        protected virtual void UpdateCore(GraphicsDevice graphicsContext)
        {
            if (Update(graphicsContext))
            {
                MeshDataChanged = false;
            }
        }

        protected abstract bool Update(GraphicsDevice graphicsContext);

        public override void Draw(GraphicsDevice graphicsContext, AppTime gameTime)
        {
            if (!IsEnabled)
            {
                return;
            }

            UpdateCore(graphicsContext);

            if (VertexBuffer == null || VertexBuffer.IsDisposed)
            {
                return;
            }

            graphicsContext.SetVertexBuffer(VertexBuffer);
            graphicsContext.VertexType = VertexType;
            graphicsContext.PrimitiveTopology = MeshData.Mesh.MeshTopology;

            if (MeshData.Mesh.HasIndices)
            {
                graphicsContext.SetIndexBuffer(IndexBuffer);
            }

            RasterizerState prevRasterState = null;

            if (IsWireFrame)
            {
                prevRasterState = graphicsContext.RasterizerState;
                graphicsContext.RasterizerState = GraphicsDevice.RasterizerStates.WireFrameCullNoneClipDisabled;
            }

            if (MeshData.Mesh.HasIndices)
            {
                graphicsContext.DrawIndexed(VertexBuffer, IndexBuffer);
            }
            else
            {
                graphicsContext.Draw(VertexBuffer.ElementCount, 1);
            }

            if (prevRasterState != null)
            {
                graphicsContext.RasterizerState = prevRasterState;
            }
        }
        
        protected bool UpdateBuffers<T>(GraphicsDevice graphicsContext, T[] vertices) where T : struct
        {
            if (vertices == null)
            {
                return false;
            }

            if (VertexBuffer != null && vertices.Length <= VertexBuffer.ElementCount)
            {
                VertexBuffer.SetData(graphicsContext, vertices);
            }
            else
            {
                VertexBuffer?.Dispose();
                VertexBuffer = ToDispose(Buffer.Vertex.New(graphicsContext, vertices));
            }

            if (MeshData.Mesh.HasIndices)
            {
                IndexBuffer?.Dispose();
                IndexBuffer = ToDispose(Buffer.Index.New(graphicsContext, MeshData.Mesh.Indices));
            }

            MeshData.Mesh.AcceptChanges();
            return true;
        }
    }
}
