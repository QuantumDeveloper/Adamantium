using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Events;
using Adamantium.UI.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public interface IWindow : IRootVisualComponent, IContentControl
{
    void Show();
    void Close();
    void Hide();
        
    bool IsActive { get; }

    IntPtr Handle { get; }
    bool IsClosed { get; }

    Double ClientWidth { get; set; }

    Double ClientHeight { get; set; }

    IntPtr SurfaceHandle { get; }
        
    double Left { get; set; }
        
    double Top { get; set; }
        
    string Title { get; set; }
        
    MSAALevel MSAALevel { get; set; }
        
    WindowState State { get; set; }
    
    internal IWindowRenderer DefaultRenderer { get; set; }

    IWindowRenderer Renderer { get; set; }

    DrawingContext GetDrawingContext();
    
    event SizeChangedEventHandler ClientSizeChanged;
    event EventHandler<WindowClosingEventArgs> Closing;
    event MSAALeveChangedHandler MSAALevelChanged;
    event StateChangedHandler StateChanged;
    
    event EventHandler<EventArgs> Closed;

    event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    
    event EventHandler<EventArgs> SourceInitialized;
}