using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal static class ComponentRenderFactory
{
    public static GeometryRenderer CreateGeometryRenderer(IGraphicsDevice device, Geometry geometry, Brush background, Brush foreground, BasicEffect basicEffect, Texture texture = null)
    {
        return new GeometryRenderer(device, geometry, background, foreground, basicEffect, texture);
    }

    public static ImageRenderer CreateImageRenderer(IGraphicsDevice device, Geometry geometry, Brush background,
        ImageSource image, BasicEffect basicEffect)
    {
        return new ImageRenderer(device, geometry, background, image, basicEffect);
    }

    public static TextRenderer CreateTextRenderer(IGraphicsDevice device, 
        Geometry geometry, 
        TextLayout layout,
        TextRenderingParameters renderingParameters, Brush brush, Brush background, BasicEffect basicEffect)
    {
        return new TextRenderer(device, geometry, layout, renderingParameters, brush, background, basicEffect);
    }
}