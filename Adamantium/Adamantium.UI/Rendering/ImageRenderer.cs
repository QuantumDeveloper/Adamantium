using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class ImageRenderer : GeometryRenderer
{
    public ImageRenderer(GraphicsDevice device, Mesh mesh, Brush brush, ImageSource image) : base(device, mesh, brush)
    {
        Image = image;
    }

    public ImageSource Image { get; set; }

    public override bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
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