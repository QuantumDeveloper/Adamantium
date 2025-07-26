using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering;

public class RenderUnit : DisposableObject
{
    private GeometryRenderer geometryRenderer;
    private GeometryRenderer strokeRenderer;
    private TextRenderer textRenderer;

    public RenderUnit()
    {
    }
    
    public ImageSource Image { get; set; }

    public TextRenderer TextRenderer
    {
        get => textRenderer;
        set => textRenderer = ToDispose(value);
    }

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

    public void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        geometryRenderer?.Draw(graphicsDevice, component, Image, projectionMatrix);
        strokeRenderer?.Draw(graphicsDevice, component, projectionMatrix);
        textRenderer?.Draw(graphicsDevice, component, projectionMatrix);
    }
}