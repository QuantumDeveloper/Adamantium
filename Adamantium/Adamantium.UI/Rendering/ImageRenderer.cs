using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal class ImageRenderer : GeometryRenderer
{
    public ImageRenderer(GraphicsDevice device, Mesh mesh, Brush brush, ImageSource[] images) : base(device, mesh, brush)
    {
        Images = images;
    }

    public ImageSource[] Images { get; set; }

    public override bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
    {
        return false;
    }
}