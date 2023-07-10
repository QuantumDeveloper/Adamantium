using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal class TextRenderer : ComponentRenderer
{
    public TextRenderer(GraphicsDevice device, FormattedText formattedText, Brush brush) : base(brush)
    {
        FormattedText = formattedText;
    }
    
    public FormattedText FormattedText { get; set; }

    public override bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
    {
        throw new NotImplementedException();
    }

    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        throw new NotImplementedException();
    }

    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        throw new NotImplementedException();
    }
}