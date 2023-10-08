using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using AdamantiumVulkan.Core;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.UI.Rendering;

internal class GeometryRenderer : ComponentRenderer
{
    public GeometryRenderer(GraphicsDevice device, Geometry geometry, Brush brush) : base(brush)
    {
        Geometry = geometry;
        var mesh = geometry.Mesh;
        var vertices = mesh?.ToMeshVertices();
        if (vertices != null)
        {
            VertexBuffer = Buffer.Vertex.New(device, vertices);
        }

        if (mesh is { HasIndices: true })
        {
            IndexBuffer = Buffer.Index.New(device, mesh.Indices);
        }

        VertexType = typeof(MeshVertex);
        if (mesh != null) PrimitiveType = mesh.MeshTopology;
        Brush = brush;
    }
    
    public Geometry Geometry { get; }
    
    public Buffer VertexBuffer { get; set; }
        
    public Buffer IndexBuffer { get; set; }
        
    public Type VertexType { get; set; }
        
    public PrimitiveType PrimitiveType { get; set; }

    public override bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        if (VertexBuffer == null) return false;

        graphicsDevice.SetVertexBuffer(VertexBuffer);
        graphicsDevice.VertexType = VertexType;
        graphicsDevice.PrimitiveTopology = PrimitiveType;

        var world = Matrix4x4F.Translation((float)component.Location.X, (float)component.Location.Y, 5);

        var effect = graphicsDevice.BasicEffect;
        effect.Wvp.SetValue(world * projectionMatrix);
        var color = Brush as SolidColorBrush;
        effect.MeshColor.SetValue(color.Color.ToVector4());
        effect.Transparency.SetValue((float)Brush.Opacity);
        
        var texture = ((BitmapSource)image)?.Texture;

        if (texture == null)
        {
            effect.BasicColoredPass.Apply();
        }
        else
        {
            if (texture.ImageLayout != ImageLayout.ShaderReadOnlyOptimal) return false;
        
            effect.SampleType.SetResource(graphicsDevice.SamplerStates.AnisotropicClampToEdge);
            effect.ShaderTexture.SetResource(texture);
            effect.BasicTexturedPass.Apply();
        }

        return true;
    }
    
    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        Draw(graphicsDevice, component, null, projectionMatrix);
    }

    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        if (!PrepareFrame(graphicsDevice, component, image, projectionMatrix)) return;
        
        if (IndexBuffer != null)
        {
            graphicsDevice.DrawIndexed(VertexBuffer, IndexBuffer);
        }
        else
        {
            graphicsDevice.Draw(VertexBuffer.ElementCount, 1);
        }
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        base.Dispose(disposeManagedResources);
            
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
    }
}