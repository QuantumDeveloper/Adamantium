using System;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.Events;

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