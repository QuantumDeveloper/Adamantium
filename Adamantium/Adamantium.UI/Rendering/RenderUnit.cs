using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal class RenderUnit : DisposableObject
{
    private GeometryRenderer geometryRenderer;
    private GeometryRenderer strokeRenderer;

    public RenderUnit()
    {
    }
    
    public ImageSource Image { get; set; }

    public GeometryRenderer GeometryRenderer
    {
        get => geometryRenderer;
        set => geometryRenderer = ToDispose(value);
    }

    public GeometryRenderer StrokeRenderer
    {
        get => strokeRenderer;
        set => strokeRenderer = ToDispose(value);
    }
    
    public int GeometryParametersHash { get; set; }
    
    public int StrokeParametersHash { get; set; }
    
    protected override void Dispose(bool disposeManagedResources)
    {
        base.Dispose(disposeManagedResources);
            
        GeometryRenderer?.Dispose();
        StrokeRenderer?.Dispose();
    }

    public void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        geometryRenderer?.Draw(graphicsDevice, component, Image, projectionMatrix);
        strokeRenderer?.Draw(graphicsDevice, component, projectionMatrix);
    }
}