using System;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using AdamantiumVulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering;

public class GeometryRenderer : ComponentRenderer
{
    public GeometryRenderer(IGraphicsDevice device, Geometry geometry, Brush background, Brush foreground,
        BasicEffect basicEffect, Texture texture = null) : base(background, foreground, basicEffect)
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

        Texture = texture;
    }

    public Geometry Geometry { get; }
    
    public Buffer VertexBuffer { get; set; }
        
    public Buffer IndexBuffer { get; set; }
        
    public Type VertexType { get; set; }
        
    public PrimitiveType PrimitiveType { get; set; }
    
    public ITexture Texture { get; set; }

    public override bool PrepareFrame(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        return true;
    }
    
    public override void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        Draw(graphicsDevice, component, null, projectionMatrix);
    }

    public override void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        if (VertexBuffer == null) return;
        
        graphicsDevice.SetVertexBuffer(VertexBuffer);
        graphicsDevice.VertexType = VertexType;
        graphicsDevice.PrimitiveTopology = PrimitiveType;
        graphicsDevice.ColorBlendEquation = ColorBlendEquations.AlphaBlend;

        //var world = Matrix4x4F.Translation((float)component.Location.X, (float)component.Location.Y, 5);
        var world = component.WorldTransform;

        var effect = BasicEffect;
        effect.Wvp.SetValue(world * projectionMatrix);
        var color = Background as SolidColorBrush;
        effect.MeshColor.SetValue(color.Color.ToVector4());
        effect.Transparency.SetValue((float)Background.Opacity);
        
        if (Texture == null)
        {
            effect.BasicColoredPass.Apply();
        }
        else
        {
            if (Texture.ImageLayout != ImageLayout.ShaderReadOnlyOptimal) return;
        
            effect.SampleType.SetResource(graphicsDevice.SamplerStates.Default);
            effect.ShaderTexture.SetResource(Texture);
            effect.BasicTexturedPass.Apply();
        }
        
        if (IndexBuffer != null)
        {
            graphicsDevice.SetIndexBuffer(IndexBuffer);
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