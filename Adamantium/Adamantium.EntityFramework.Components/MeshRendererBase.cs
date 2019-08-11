using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
//using Buffer = Adamantium.Engine.Graphics.Buffer;
//using RasterizerState = Adamantium.Engine.Graphics.RasterizerState;

namespace Adamantium.EntityFramework.Components
{
    [RequiredComponets(typeof(MeshData))]
    public abstract class MeshRendererBase : RenderableComponent
    {
        private bool _isWireFrame;

        //protected Buffer VertexBuffer { get; set; }
        protected MeshData MeshData { get; set; }

        protected bool MeshDataChanged { get; set; }

        public bool IsWireFrame
        {
            get => _isWireFrame;
            set => SetProperty(ref _isWireFrame, value);
        }

        public override void Initialize()
        {
            var meshData = GetComponent<MeshData>();
            if (meshData != null)
            {
                MeshData = meshData;
                MeshDataChanged = true;
                MeshData.MeshDataChanged += MeshData_MeshDataChanged;
                base.Initialize();
            }
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

        public override void Draw(GraphicsDevice graphicsContext, IGameTime gameTime)
        {
            if (!IsEnabled)
            {
                return;
            }

            UpdateCore(graphicsContext);

            //if (VertexBuffer == null || VertexBuffer.IsDisposed)
            //{
            //    return;
            //}

            //graphicsContext.SetVertexBuffer(VertexBuffer);
            //graphicsContext.VertexInputLayout = InputLayout;

            //if (MeshData.Mesh.Indices.Length > 0)
            //{
            //    graphicsContext.SetIndexBuffer(IndexBuffer);
            //}

            //RasterizerState prevRasterState = null;

            //if (IsWireFrame)
            //{
            //    prevRasterState = graphicsContext.RasterizerState;
            //    graphicsContext.RasterizerState = graphicsContext.RasterizerStates.WireFrameCullNoneClipEnabled;
            //}

            //if (MeshData.Mesh.Indices.Length > 0)
            //{
            //    graphicsContext.DrawIndexed(MeshData.Mesh.MeshTopology, MeshData.Mesh.Indices.Length);
            //}
            //else
            //{
            //    graphicsContext.Draw(MeshData.Mesh.MeshTopology, MeshData.Mesh.Positions.Length);
            //}

            //if (prevRasterState != null)
            //{
            //    graphicsContext.RasterizerState = prevRasterState;
            //}
        }
    }
}
