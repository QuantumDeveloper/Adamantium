using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal abstract class ComponentRenderer : DisposableObject
{
    protected ComponentRenderer(Brush brush)
    {
        Brush = brush;
    }
    
    public Brush Brush { get; set; }

    public abstract bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix);

    public abstract void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix);
    
    public abstract void Draw(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix);
}