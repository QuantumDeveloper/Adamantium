using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Rendering;

public interface IWindowRenderer
{
    public bool IsWindowResized { get; }
        
    public void SetWindow(IWindow window);
        
    public void Render();

    public void ResizePresenter(PresentationParameters parameters);
}