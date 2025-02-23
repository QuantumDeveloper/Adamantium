using System;
using Adamantium.Core;
using Adamantium.ECS.ComponentsBasics;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using AdamantiumVulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.ECS.Components
{
    [RequiredComponent(typeof(MeshData))]
    public abstract class MeshRendererBase : RenderComponent
    {
        private bool isWireFrame;

        protected IBuffer VertexBuffer { get; set; }

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

        protected virtual void UpdateCore(IGraphicsDevice graphicsContext)
        {
            if (Update(graphicsContext))
            {
                MeshDataChanged = false;
            }
        }

        protected abstract bool Update(IGraphicsDevice graphicsContext);

        public override void Draw(IGraphicsDevice graphicsContext, AppTime gameTime)
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
            graphicsContext.PolygonMode = PolygonMode.Fill;
            graphicsContext.CullMode = CullModeFlagBits.None;

            if (MeshData.Mesh.HasIndices)
            {
                graphicsContext.SetIndexBuffer(IndexBuffer);
            }

            var prevRasterState = graphicsContext.PolygonMode;

            if (IsWireFrame)
            {
                graphicsContext.PolygonMode = PolygonMode.Line;
            }

            if (MeshData.Mesh.HasIndices)
            {
                graphicsContext.DrawIndexed(VertexBuffer, IndexBuffer);
            }
            else
            {
                graphicsContext.Draw(VertexBuffer.ElementCount, 1);
            }

            graphicsContext.PolygonMode = prevRasterState;
        }
        
        protected bool UpdateBuffers<T>(IGraphicsDevice graphicsContext, T[] vertices) where T : struct
        {
            if (vertices == null)
            {
                return false;
            }

            if (VertexBuffer != null && (ulong)vertices.Length <= VertexBuffer.ElementCount)
            {
                VertexBuffer.SetData(vertices);
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
