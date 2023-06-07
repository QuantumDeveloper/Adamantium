using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Rendering;

public interface IWindowRenderer : IDisposable
{
    public bool IsRendererUpToDate { get; }
        
    public void SetWindow(IWindow window);
        
    public void Render(AppTime appTime);

    public void ResizePresenter(PresentationParameters parameters);
}