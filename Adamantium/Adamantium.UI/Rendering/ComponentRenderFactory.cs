using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal static class ComponentRenderFactory
{
    public static GeometryRenderer CreateGeometryRenderer(GraphicsDevice device, Geometry geometry, Brush brush)
    {
        return new GeometryRenderer(device, geometry, brush);
    }

    public static ImageRenderer CreateImageRenderer(GraphicsDevice device, Geometry geometry, Brush brush,
        ImageSource image)
    {
        return new ImageRenderer(device, geometry, brush, image);
    }
}