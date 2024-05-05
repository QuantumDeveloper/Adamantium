using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Fonts;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal static class ComponentRenderFactory
{
    public static GeometryRenderer CreateGeometryRenderer(GraphicsDevice device, Geometry geometry, Brush background, Brush foreground, Texture texture = null)
    {
        return new GeometryRenderer(device, geometry, background, foreground, texture);
    }

    public static ImageRenderer CreateImageRenderer(GraphicsDevice device, Geometry geometry, Brush background,
        ImageSource image)
    {
        return new ImageRenderer(device, geometry, background, image);
    }
    
    public static TextRenderer CreateTextRenderer(GraphicsDevice device, Geometry geometry, TextLayout layout, TextRenderingParameters renderingParameters, Brush brush, Brush background)
    {
        return new TextRenderer(device, geometry, layout, renderingParameters, brush, background);
    }
}