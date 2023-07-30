using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal static class ComponentRenderFactory
{
    public static GeometryRenderer CreateGeometryRenderer(GraphicsDevice device, Mesh mesh, Brush brush)
    {
        return new GeometryRenderer(device, mesh, brush);
    }

    public static ImageRenderer CreateImageRenderer(GraphicsDevice device, Mesh mesh, Brush brush,
        ImageSource image)
    {
        return new ImageRenderer(device, mesh, brush, image);
    }
}