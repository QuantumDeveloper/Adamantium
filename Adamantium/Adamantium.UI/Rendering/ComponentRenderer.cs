using Adamantium.Core;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal abstract class ComponentRenderer : DisposableObject
{
    protected ComponentRenderer(Brush background, Brush foreground, BasicEffect basicEffect)
    {
        Background = background;
        Foreground = foreground;
        BasicEffect = basicEffect;
    }
    
    protected BasicEffect BasicEffect { get; set; }
    
    public Brush Background { get; set; }
    
    public Brush Foreground { get; set; }

    public abstract bool PrepareFrame(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix);

    public abstract void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix);
    
    public abstract void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix);
}