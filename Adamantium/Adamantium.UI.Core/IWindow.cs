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

    /// <summary>Toggles the GPU analytic anti-aliasing (coverage fringe on fills + feathered strokes). True = on.
    /// Independent of <see cref="MSAALevel"/>, so the two can be compared in any combination.</summary>
    bool AnalyticAntialiasing { get; set; }

    WindowState State { get; set; }

    /// <summary>When true the OS frame is removed (WM_NCCALCSIZE reclaims the non-client area) and the window draws its
    /// own chrome (title bar) - the modern borderless look. WS_THICKFRAME is kept so native resize borders, Aero Snap,
    /// the drop shadow and maximize-to-work-area still work. Read by the platform worker.</summary>
    bool UseCustomChrome { get; }

    /// <summary>How the user may resize the window (honoured by the custom-chrome hit-test). Default CanResize.</summary>
    WindowResizeMode ResizeMode { get; }

    /// <summary>Height (client DIP) of the draggable caption strip at the top of the window; 0 = none. A TitleBar sets
    /// this on layout. Read (thread-safely, geometrically) by the platform worker's hit-test.</summary>
    double CaptionHeight { get; set; }

    /// <summary>Width (client DIP) reserved on the RIGHT of the caption strip for the window buttons - that band is NOT
    /// draggable (the buttons need clicks). A TitleBar sets this on layout. The platform worker reads both metrics
    /// (geometrically, from the OS message thread) to return HTCAPTION for the drag strip.</summary>
    double CaptionRightInset { get; set; }

    /// <summary>Per-window DPI scale (device pixels per DIP), separate X/Y (usually equal on desktop). 1,1 = 96 DPI /
    /// 100%. Set by the platform on create and on WM_DPICHANGED; drives the render scale and the DIP&lt;-&gt;physical map.</summary>
    Vector2 DpiScale { get; set; }

    IWindowRenderer DefaultRenderer { get; set; }

    IWindowRenderer Renderer { get; set; }

    /// <summary>Framework tooling overlays (selection frames etc.) drawn ON TOP of the window's content in the same
    /// frame by the renderer's adorner stage. Empty by default; the WPF-equivalent of an AdornerLayer's adorners.</summary>
    IReadOnlyList<IUIComponent> Adorners { get; }

    /// <summary>The laid-out children of the open popups, drawn ON TOP of the content (and adorners) within the window by
    /// the renderer's popup stage. Empty by default. <see cref="LayoutPopups"/> refreshes their positions each frame.</summary>
    IReadOnlyList<IUIComponent> PopupRoots { get; }

    /// <summary>Re-evaluates every open popup's position from its target's current location and lays its child out,
    /// clamped inside the window. Called by the popup render stage each frame so a popup follows a moving target.</summary>
    void LayoutPopups();
    
    bool ShouldDisplayWindow { get; }
    
    IDrawingContext GetDrawingContext();

    Vector2 ScreenToClient(Vector2 p);

    Vector2 ClientToScreen(Vector2 p);
    
    event SizeChangedEventHandler ClientSizeChanged;
    event EventHandler<WindowClosingEventArgs> Closing;
    event MSAALeveChangedHandler MSAALevelChanged;
    event StateChangedHandler StateChanged;

    /// <summary>Raised after <see cref="DpiScale"/> changes (the window moved to a monitor with a different scale).</summary>
    event EventHandler<EventArgs> DpiChanged;

    event EventHandler<EventArgs> Closed;

    event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    
    event EventHandler<EventArgs> SourceInitialized;
}