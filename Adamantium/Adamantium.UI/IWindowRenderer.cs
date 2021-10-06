using Adamantium.UI.Controls;

namespace Adamantium.UI
{
    public interface IWindowRenderer
    {
        public void Render();
    }

    internal class WindowRenderer : IWindowRenderer
    {
        private IWindow window;
        
        public WindowRenderer(IWindow window)
        {
            this.window = window;
        }

        public void Render()
        {
            
        }
    }
}