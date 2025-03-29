using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;

namespace Adamantium.UI.Rendering;

public interface IWindowRenderer : IDisposable
{
    public DrawingContext DrawingContext { get; }
    public bool IsRendererUpToDate { get; }
        
    public void SetWindow(IWindow window);
        
    public void Render(AppTime appTime);
    
    public GraphicsPresenter Presenter { get; }

    public void Present();

    public void ResizePresenter(PresentationParameters parameters);
    public void ResizePresenter(uint width, uint height);
}