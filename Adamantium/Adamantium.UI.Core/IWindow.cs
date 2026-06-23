using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public interface IWindow : IRootVisualComponent, IContentControl
{
    void Show();
    void Close();
    void Hide();
        
    bool IsActive { get; }

    IntPtr Handle { get; }
    
    IntPtr SurfaceHandle { get; }
    
    bool IsClosed { get; }
        
    MSAALevel MSAALevel { get; set; }
        
    WindowState State { get; set; }
    
    IWindowRenderer DefaultRenderer { get; set; }

    IWindowRenderer Renderer { get; set; }

    /// <summary>Framework tooling overlays (selection frames etc.) drawn ON TOP of the window's content in the same
    /// frame by the renderer's adorner stage. Empty by default; the WPF-equivalent of an AdornerLayer's adorners.</summary>
    IReadOnlyList<IUIComponent> Adorners { get; }
    
    bool ShouldDisplayWindow { get; }
    
    IDrawingContext GetDrawingContext();

    Vector2 ScreenToClient(Vector2 p);

    Vector2 ClientToScreen(Vector2 p);
    
    event SizeChangedEventHandler ClientSizeChanged;
    event EventHandler<WindowClosingEventArgs> Closing;
    event MSAALeveChangedHandler MSAALevelChanged;
    event StateChangedHandler StateChanged;
    
    event EventHandler<EventArgs> Closed;

    event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    
    event EventHandler<EventArgs> SourceInitialized;
}