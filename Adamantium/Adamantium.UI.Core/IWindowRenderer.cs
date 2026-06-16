using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Core;

public interface IWindowRenderer : IDisposable
{
    public IDrawingContext DrawingContext { get; }
    public bool IsRendererUpToDate { get; }
    public bool FirstFrameProcessed { get; }
        
    public void SetWindow(IWindow window);
        
    public void Render(AppTime appTime);

    /// <summary>Records out-of-render-pass work (shared-surface latch copies) before BeginRendering.</summary>
    public void PreRender();

    public void PrepareData();
    
    public GraphicsPresenter Presenter { get; }

    public void OnFrameEnded();

    public void Present();

    public void ResizePresenter(PresentationParameters parameters);
    public void ResizePresenter(uint width, uint height);
}