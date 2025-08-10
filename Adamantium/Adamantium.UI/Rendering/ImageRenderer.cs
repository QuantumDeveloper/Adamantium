using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class ImageRenderer : GeometryRenderer
{
    public ImageRenderer(IGraphicsDevice device, Geometry geometry, Brush background, ImageSource image, BasicEffect basicEffect) : base(
        device, geometry, background, null, basicEffect)
    {
        Image = image;
    }

    public ImageSource Image { get; set; }

    public override bool PrepareFrame(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
    {
        if (VertexBuffer == null) return false;

        graphicsDevice.SetVertexBuffer(VertexBuffer);
        if (IndexBuffer != null)
        {
            graphicsDevice.SetIndexBuffer(IndexBuffer);
        }
        graphicsDevice.VertexType = VertexType;
        graphicsDevice.PrimitiveTopology = PrimitiveType;

        //var world = Matrix4x4F.Translation((float)component.Location.X, (float)component.Location.Y, 5);
        var world = component.WorldTransform;
        
        var effect = BasicEffect;
        effect.Wvp.SetValue(world * projectionMatrix);
        var color = Foreground as SolidColorBrush;
        effect.MeshColor.SetValue(color.Color.ToVector4());
        effect.Transparency.SetValue((float)Foreground.Opacity);
        
        var texture = ((BitmapSource)Image)?.Texture;

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
}