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

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }
    
    public void Close()
    {
        IsClosed = true;
    }

    // A virtual window is drawn INSIDE a host surface - it has no OS window, so there is no foreground to take and no
    // z-order to raise. Both are no-ops rather than throws: a drag crossing one must not blow up.
    public void Activate()
    {
    }

    public void BringToFront()
    {
    }

    // Nothing outside moves a virtual window: it is drawn inside a host surface, and its position is whatever the host
    // gave it. Assigning it here would be the window arguing with its own host.
    public void UpdatePositionFromPlatform(double left, double top)
    {
    }

    // Everything a view can see or bind is a registered property, exactly as on a real window - a virtual window is a
    // control in somebody's tree, and half of it being plain fields is a trap: the half that is plain silently refuses
    // bindings, styles and animation with no error to explain why.
    //
    // The two HANDLES stay plain fields on purpose. They are interop identity, read on the render and message paths,
    // and a property read takes the component lock and boxes - nothing binds to an HWND, so the cost would buy nothing.

    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsClosedProperty = AdamantiumProperty.Register(nameof(IsClosed),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ClientWidthProperty = AdamantiumProperty.Register(nameof(ClientWidth),
        typeof(double), typeof(VirtualWindow), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty ClientHeightProperty = AdamantiumProperty.Register(nameof(ClientHeight),
        typeof(double), typeof(VirtualWindow), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
        typeof(double), typeof(VirtualWindow), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
        typeof(double), typeof(VirtualWindow), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(string), typeof(VirtualWindow), new PropertyMetadata(string.Empty));

    public static readonly AdamantiumProperty MSAALevelProperty = AdamantiumProperty.Register(nameof(MSAALevel),
        typeof(MSAALevel), typeof(VirtualWindow), new PropertyMetadata(MSAALevel.None));

    public static readonly AdamantiumProperty AnalyticAntialiasingProperty = AdamantiumProperty.Register(
        nameof(AnalyticAntialiasing), typeof(bool), typeof(VirtualWindow), new PropertyMetadata(true));

    public static readonly AdamantiumProperty StateProperty = AdamantiumProperty.Register(nameof(State),
        typeof(WindowState), typeof(VirtualWindow), new PropertyMetadata(WindowState.Normal));

    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        protected set => SetValue(IsActiveProperty, value);
    }

    public IntPtr Handle { get; }

    public bool IsClosed
    {
        get => GetValue<bool>(IsClosedProperty);
        protected set => SetValue(IsClosedProperty, value);
    }

    public double ClientWidth
    {
        get => GetValue<double>(ClientWidthProperty);
        set => SetValue(ClientWidthProperty, value);
    }

    public double ClientHeight
    {
        get => GetValue<double>(ClientHeightProperty);
        set => SetValue(ClientHeightProperty, value);
    }

    /// <summary>The application context this window was attached to. A plain property on purpose: it is not window
    /// STATE that anything animates, binds to or styles - it is the ambient context, handed over once at attach.</summary>
    public IUIContext UIContext { get; private set; }

    public IntPtr SurfaceHandle { get; }

    public double Left
    {
        get => GetValue<double>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public double Top
    {
        get => GetValue<double>(TopProperty);
        set => SetValue(TopProperty, value);
    }

    public string Title
    {
        get => GetValue<string>(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public MSAALevel MSAALevel
    {
        get => GetValue<MSAALevel>(MSAALevelProperty);
        set => SetValue(MSAALevelProperty, value);
    }

    public bool AnalyticAntialiasing
    {
        get => GetValue<bool>(AnalyticAntialiasingProperty);
        set => SetValue(AnalyticAntialiasingProperty, value);
    }

    public WindowState State
    {
        get => GetValue<WindowState>(StateProperty);
        set => SetValue(StateProperty, value);
    }
    // A virtual (in-game/designer) window has no OS frame, so custom chrome is moot here.
    public bool UseCustomChrome => false;
    public WindowResizeMode ResizeMode => WindowResizeMode.NoResize;

    // Overlay traits. Registered properties, like everything else a view can bind, style or animate - a virtual window
    // is a control in somebody's tree, so these have to be reachable from markup. Nothing here ACTS on them: a virtual
    // window has no z-order among desktop windows, no focus of its own and nothing composing it.
    public static readonly AdamantiumProperty TopmostProperty = AdamantiumProperty.Register(nameof(Topmost),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty TransparentToInputProperty = AdamantiumProperty.Register(nameof(TransparentToInput),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ActivateOnShowProperty = AdamantiumProperty.Register(nameof(ActivateOnShow),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(true));

    public static readonly AdamantiumProperty UseTransparentCompositionProperty = AdamantiumProperty.Register(nameof(UseTransparentComposition),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ShowWindowBorderProperty = AdamantiumProperty.Register(nameof(ShowWindowBorder),
        typeof(bool), typeof(VirtualWindow), new PropertyMetadata(true));

    public bool ShowWindowBorder
    {
        get => GetValue<bool>(ShowWindowBorderProperty);
        set => SetValue(ShowWindowBorderProperty, value);
    }

    public static readonly AdamantiumProperty WindowOpacityProperty = AdamantiumProperty.Register(nameof(WindowOpacity),
        typeof(double), typeof(VirtualWindow), new PropertyMetadata(1.0));

    public bool Topmost
    {
        get => GetValue<bool>(TopmostProperty);
        set => SetValue(TopmostProperty, value);
    }

    public bool TransparentToInput
    {
        get => GetValue<bool>(TransparentToInputProperty);
        set => SetValue(TransparentToInputProperty, value);
    }

    public bool ActivateOnShow
    {
        get => GetValue<bool>(ActivateOnShowProperty);
        set => SetValue(ActivateOnShowProperty, value);
    }

    public bool UseTransparentComposition
    {
        get => GetValue<bool>(UseTransparentCompositionProperty);
        set => SetValue(UseTransparentCompositionProperty, value);
    }

    public double WindowOpacity
    {
        get => GetValue<double>(WindowOpacityProperty);
        set => SetValue(WindowOpacityProperty, value);
    }
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
