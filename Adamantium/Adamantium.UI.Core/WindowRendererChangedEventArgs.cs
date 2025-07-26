namespace Adamantium.UI.Core;

public class WindowRendererChangedEventArgs : EventArgs
{
    public IWindowRenderer OldRenderer { get; }
    
    public IWindowRenderer NewRenderer { get; }
    
    public WindowRendererChangedEventArgs(IWindowRenderer oldRenderer, IWindowRenderer newRenderer)
    {
        OldRenderer = oldRenderer;
        NewRenderer = newRenderer;
    }
}