using Adamantium.UI.Controls;

namespace Adamantium.UI
{
    public interface IWindowRenderer
    {
        public void SetWindow(IWindow window);
        
        public void Render();
    }
}