using System;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.UI.Rendering
{
    internal class UIComponentRenderer : DisposableObject
    {
        public Brush Brush { get; set; }
        public Buffer VertexBuffer { get; set; }
        
        public Buffer IndexBuffer { get; set; }
        
        public Type VertexType { get; set; }
        
        public PrimitiveType PrimitiveType { get; set; }
        
        public BitmapSource Bitmap { get; set; } 

        public void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
        {
            graphicsDevice.SetVertexBuffer(VertexBuffer);
            graphicsDevice.VertexType = VertexType;
            graphicsDevice.PrimitiveTopology = PrimitiveType;
            graphicsDevice.SetIndexBuffer(IndexBuffer);
            
            var world = Matrix4x4F.Translation((float)component.Location.X, (float)component.Location.Y, 5);
            graphicsDevice.BasicEffect.Parameters["wvp"].SetValue(world * projectionMatrix);
            var color = Brush as SolidColorBrush;
            graphicsDevice.BasicEffect.Parameters["meshColor"].SetValue(color.Color.ToVector4());
            graphicsDevice.BasicEffect.Parameters["transparency"].SetValue((float)Brush.Opacity);
            graphicsDevice.BasicEffect.Techniques["Basic"].Passes["Colored"].Apply();
            
            graphicsDevice.DrawIndexed(VertexBuffer, IndexBuffer);
        }

        public static UIComponentRenderer Create(GraphicsDevice device, Mesh mesh, Brush brush)
        {
            var renderer = new UIComponentRenderer();
            var vertices = mesh.ToMeshVertices();
            renderer.VertexBuffer = Buffer.Vertex.New(device, vertices);
            renderer.IndexBuffer = Buffer.Index.New(device, mesh.Indices);
            renderer.VertexType = typeof(MeshVertex);
            renderer.PrimitiveType = mesh.MeshTopology;
            renderer.Brush = brush;
            return renderer;
        }
    }
}