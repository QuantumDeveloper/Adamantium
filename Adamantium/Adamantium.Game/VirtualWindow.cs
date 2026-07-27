using System;
using System.Collections.Generic;
using Adamantium.Game.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Events;
using Adamantium.UI.Rendering;

namespace Adamantium.Game;

public class VirtualWindow : ContentControl, IVirtualWindow, IAdornerHost, IPopupHost
{
    public Vector2 PointToClient(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public Vector2 PointToScreen(Vector2 point)
    {
        throw new NotImplementedException();
    }

    public void AttachContextAndInitialize(IUIContext context)
    {
        UIContext = context;
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        IsClosed = true;
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    // A virtual window is drawn INSIDE a host surface - it has no OS window, so there is no foreground to take and no
    // z-order to raise. Both are no-ops rather than throws: a drag crossing one must not blow up.
    public void Activate()
    {
    }

    public void BringToFront()
    {
    }

    public bool IsActive { get; }
    public IntPtr Handle { get; }
    public bool IsClosed { get; protected set; }
    public double ClientWidth { get; set; }
    public double ClientHeight { get; set; }
    public IUIContext UIContext { get; private set; }
    public IntPtr SurfaceHandle { get; }
    public double Left { get; set; }
    public double Top { get; set; }
    public string Title { get; set; }
    public MSAALevel MSAALevel { get; set; }
    public bool AnalyticAntialiasing { get; set; } = true;
    public WindowState State { get; set; }
    // A virtual (in-game/designer) window has no OS frame, so custom chrome is moot here.
    public bool UseCustomChrome => false;
    public WindowResizeMode ResizeMode => WindowResizeMode.NoResize;
    public Rect CaptionDragRect { get; set; }
    public Rect ResizeGripRect { get; set; }
    public IWindowRenderer DefaultRenderer { get; set; }
    public IWindowRenderer Renderer { get; set; }

    // Framework tooling overlays (selection frames etc.). The designer drives this via AdornerLayer.SetSelection; the
    // render service's adorner processor draws Adorners on top of the content. Mirrors WindowBase.
    public AdornerLayer AdornerLayer { get; } = new AdornerLayer();
    public IReadOnlyList<IUIComponent> Adorners => AdornerLayer.Adorners;

    // In-window popup overlay (tooltips, popups). Mirrors WindowBase; the render service's popup processor draws these.
    public PopupLayer PopupLayer { get; } = new PopupLayer();
    public IReadOnlyList<IUIComponent> PopupRoots => PopupLayer.Roots;
    public void LayoutPopups() => PopupLayer.UpdateLayout(new Size(ClientWidth, ClientHeight));

    public GameOutput RootWindow { get; set; }

    public bool ShouldDisplayWindow { get; }

    public IDrawingContext GetDrawingContext()
    {
        if (Renderer != null)
        {
            return Renderer.DrawingContext;
        }

        if (DefaultRenderer != null)
            return DefaultRenderer.DrawingContext;

        throw new ArgumentException("Window does not contain renderer and could not return DrawingContext");
    }

    public Vector2 ScreenToClient(Vector2 p)
    {
        throw new NotImplementedException();
    }

    public Vector2 ClientToScreen(Vector2 p)
    {
        throw new NotImplementedException();
    }

    public event SizeChangedEventHandler ClientSizeChanged;
    public event EventHandler<WindowClosingEventArgs> Closing;
    public event MSAALeveChangedHandler MSAALevelChanged;
    public event StateChangedHandler StateChanged;
    public event EventHandler ResizeModeChanged;   // never raised: a virtual window has no custom chrome / native worker
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<WindowRendererChangedEventArgs> RendererChanged;
    public event EventHandler<EventArgs> SourceInitialized;

    // Virtual window has no OS monitor; stays 1,1 unless a host drives it (e.g. designer scale). Setter fires DpiChanged.
    private Vector2 _dpiScale = new Vector2(1, 1);
    public Vector2 DpiScale
    {
        get => _dpiScale;
        set
        {
            if (_dpiScale == value) return;
            _dpiScale = value;
            DpiChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler<EventArgs> DpiChanged;
}