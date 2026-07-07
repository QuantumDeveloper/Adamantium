using Adamantium.Graphics.Core;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI.Controls;

public abstract class WindowBase : ContentControl, IWindow, IWindowInternals, IAdornerHost, IPopupHost
{
    private IWindowRenderer _renderer;
    protected IWindowWorkerService WindowWorkerService { get; private set; }

    public IWindowRenderer DefaultRenderer { get; set; }

    public IWindowRenderer Renderer
    {
        get => _renderer;
        set
        {
            if (value != _renderer)
            {
                OnRendererChanged(_renderer, value);
                _renderer = value;
            }
        }
    }

    private void OnRendererChanged(IWindowRenderer oldRenderer, IWindowRenderer newRenderer)
    {
        var args = new WindowRendererChangedEventArgs(oldRenderer, newRenderer);
        RendererChanged?.Invoke(this, args);
    }

    /// <summary>The window's adorner layer: tooling overlays (selection frames etc.) the renderer draws on top of the
    /// content. The WPF analog of the per-window AdornerLayer; add adorners here to have them rendered.</summary>
    public AdornerLayer AdornerLayer { get; } = new AdornerLayer();

    public IReadOnlyList<IUIComponent> Adorners => AdornerLayer.Adorners;

    /// <summary>The window's popup layer: open <see cref="Popup"/>s' children the renderer draws on top of the content,
    /// within the window. A popup registers itself here (via <see cref="IPopupHost"/>) while open.</summary>
    public PopupLayer PopupLayer { get; } = new PopupLayer();

    public IReadOnlyList<IUIComponent> PopupRoots => PopupLayer.Roots;

    public void LayoutPopups() => PopupLayer.UpdateLayout(new Size(ClientWidth, ClientHeight));

    public WindowBase()
    {
        // Default/cancel routing: the window is the root, so unhandled Enter/Esc bubble up here from the focused element.
        AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnWindowKeyDown));
    }

    // Enter activates the IsDefault button, Escape the IsCancel button - unless the focused element already handled the
    // key (handled keys don't reach this handler). WPF Window default/cancel behaviour.
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var target = e.Key switch
        {
            Key.Enter => FindButton(this, b => b.IsDefault),
            Key.Escape => FindButton(this, b => b.IsCancel),
            _ => null
        };
        if (target != null)
        {
            target.PerformClick();
            e.Handled = true;
        }
    }

    private static Button FindButton(IUIComponent root, Func<Button, bool> match)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is Button button && match(button)) return button;
            var found = FindButton(child, match);
            if (found != null) return found;
        }
        return null;
    }

    public static readonly RoutedEvent ClientSizeChangedEvent = EventManager.RegisterRoutedEvent("ClientSizeChanged",
        RoutingStrategy.Direct, typeof(SizeChangedEventHandler), typeof(WindowBase));

    public static readonly RoutedEvent MSAALevelChangedEvent = EventManager.RegisterRoutedEvent("MSAALevelChanged",
        RoutingStrategy.Direct, typeof(MSAALeveChangedHandler), typeof(WindowBase));
        
    public static readonly RoutedEvent StateChangedEvent = EventManager.RegisterRoutedEvent("StateChanged",
        RoutingStrategy.Direct, typeof(StateChangedHandler), typeof(WindowBase));


    public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
    public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d));
        
    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(String), typeof(WindowBase), new PropertyMetadata(String.Empty, TitleChangedCallback));

    public static readonly AdamantiumProperty ClientWidthProperty = AdamantiumProperty.Register(nameof(ClientWidth),
        typeof(Double), typeof(WindowBase),
        new PropertyMetadata(Double.NaN,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender, ClientWidthChangedCallBack));

    public static readonly AdamantiumProperty ClientHeightProperty = AdamantiumProperty.Register(nameof(ClientHeight),
        typeof(Double), typeof(WindowBase),
        new PropertyMetadata(Double.NaN,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender, ClientHeightChangedCallBack));
        
    public static readonly AdamantiumProperty MSAALevelProperty = AdamantiumProperty.Register(nameof(MSAALevel),
        typeof(MSAALevel), typeof(WindowBase),
        new PropertyMetadata(MSAALevel.None, PropertyMetadataOptions.AffectsRender, MSAALevelChangedCallback));

    // Live toggle for the GPU analytic AA (fill coverage fringe + feathered strokes), independent of MSAALevel so both
    // can be A/B-compared. AffectsRender re-renders on change; the render path reads it each frame (no rebuild needed).
    public static readonly AdamantiumProperty AnalyticAntialiasingProperty = AdamantiumProperty.Register(nameof(AnalyticAntialiasing),
        typeof(bool), typeof(WindowBase),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty StateProperty = AdamantiumProperty.Register(nameof(State),
        typeof(WindowState), typeof(WindowBase),
        new PropertyMetadata(WindowState.Normal, PropertyMetadataOptions.AffectsRender, StateChangedCallback));

    // Modern borderless chrome ON by default: the OS frame is removed and the window draws its own title bar (see the
    // Window ControlTemplate). Read once by the platform worker at create time (the native frame styles are fixed then),
    // so flip it in markup/ctor before the window is shown.
    public static readonly AdamantiumProperty UseCustomChromeProperty = AdamantiumProperty.Register(nameof(UseCustomChrome),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ResizeModeProperty = AdamantiumProperty.Register(nameof(ResizeMode),
        typeof(WindowResizeMode), typeof(WindowBase), new PropertyMetadata(WindowResizeMode.CanResize));

    // MahApps-style caption command bars, forwarded to the TitleBar by the default Window template. Bind a view-model's
    // collection of WindowCommand items to show quick actions in the title bar (left of it / right, before the buttons).
    public static readonly AdamantiumProperty LeftWindowCommandsProperty = AdamantiumProperty.Register(nameof(LeftWindowCommands),
        typeof(System.Collections.IEnumerable), typeof(WindowBase), new PropertyMetadata(null));

    public static readonly AdamantiumProperty RightWindowCommandsProperty = AdamantiumProperty.Register(nameof(RightWindowCommands),
        typeof(System.Collections.IEnumerable), typeof(WindowBase), new PropertyMetadata(null));

    // Window icon/logo shown at the left of the custom title bar (forwarded to the TitleBar by the default template).
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(WindowBase), new PropertyMetadata(null));

    private static void TitleChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(a is WindowBase component)) return;

        if (component.WindowWorkerService != null)
        {
            var title = (string)e.NewValue;
            component.WindowWorkerService.SetTitle(title);
        }
    }

    private static void StateChangedCallback(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is WindowBase component)) return;

        var args = new StateChangedEventArgs((WindowState)e.NewValue);
        args.RoutedEvent = StateChangedEvent;
        component.RaiseEvent(args);
    }
        
    private static void MSAALevelChangedCallback(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is WindowBase component)) return;

        var args = new MSAALevelChangedEventArgs((MSAALevel)e.NewValue);
        args.RoutedEvent = MSAALevelChangedEvent;
        component.RaiseEvent(args);
    }

    private static void ClientWidthChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumAdamantiumComponent is WindowBase component)) return;
        Size old = default;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        old.Width = (double) e.OldValue;
        old.Height = component.Height;
            
        var newSize = new Size((double)e.NewValue, component.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = ClientSizeChangedEvent;
        component.RaiseEvent(args);
    }
        
    private static void ClientHeightChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumAdamantiumComponent is WindowBase component)) return;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        var old = new Size(component.Width, (double)e.OldValue);
        var newSize = new Size(component.Width, (double)e.NewValue);
        var args = new SizeChangedEventArgs(old, newSize, false, true);
        args.RoutedEvent = ClientSizeChangedEvent;
        component?.RaiseEvent(args);
    }

    public Double Left
    {
        get => GetValue<Double>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }
        
    public Double Top
    {
        get => GetValue<Double>(TopProperty);
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

    public bool UseCustomChrome
    {
        get => GetValue<bool>(UseCustomChromeProperty);
        set => SetValue(UseCustomChromeProperty, value);
    }

    public WindowResizeMode ResizeMode
    {
        get => GetValue<WindowResizeMode>(ResizeModeProperty);
        set => SetValue(ResizeModeProperty, value);
    }

    public System.Collections.IEnumerable LeftWindowCommands
    {
        get => GetValue<System.Collections.IEnumerable>(LeftWindowCommandsProperty);
        set => SetValue(LeftWindowCommandsProperty, value);
    }

    public System.Collections.IEnumerable RightWindowCommands
    {
        get => GetValue<System.Collections.IEnumerable>(RightWindowCommandsProperty);
        set => SetValue(RightWindowCommandsProperty, value);
    }

    /// <summary>Icon/logo content shown at the left of the custom title bar.</summary>
    public object Icon
    {
        get => GetValue<object>(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Begins an OS-driven move of the window (custom-chrome caption drag). Wired from a title bar's press.</summary>
    public void DragMove() => WindowWorkerService?.BeginMoveDrag();

    /// <summary>Minimizes the window (title bar minimize button).</summary>
    public void Minimize() => State = WindowState.Minimized;

    /// <summary>Maximizes the window (title bar maximize button).</summary>
    public void Maximize() => State = WindowState.Maximized;

    /// <summary>Restores a maximized/minimized window to its normal size (title bar restore button).</summary>
    public void RestoreDown() => State = WindowState.Normal;

    /// <summary>Toggles between maximized and normal - the caption double-click / maximize button behaviour.</summary>
    public void ToggleMaximizeRestore() =>
        State = State == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Caption metrics published by a hosted TitleBar on its arrange (loop thread). Read geometrically by the worker's
    // hit-test (OS message thread) - plain doubles, so a torn read is at worst a one-frame-off hit, never a crash. This
    // MUST stay geometric: the earlier visual-tree walk (GetVisualsAt) from WM_NCHITTEST raced the layout thread's
    // VisualChildren mutation during a state-change relayout and spun the UI thread (every caption button froze the app).
    public Rect CaptionDragRect { get; set; }

    public Double ClientWidth
    {
        get => GetValue<Double>(ClientWidthProperty);
        set => SetValue(ClientWidthProperty, value);
    }

    public Double ClientHeight
    {
        get => GetValue<Double>(ClientHeightProperty);
        set => SetValue(ClientHeightProperty, value);
    }
        
    // Pointer to the surface for rendering on this window
    public abstract IntPtr SurfaceHandle { get; internal set; }
        
    public abstract IntPtr Handle { get; internal set; }
    public bool IsClosed { get; protected set; }

    public abstract Vector2 PointToClient(Vector2 point);
    public abstract Vector2 PointToScreen(Vector2 point);
    public void AttachContextAndInitialize(IUIContext context)
    {
        UIContext = context;
        InitializeComponent();
        // The root window has no logical parent, so OnAttachedToLogicalTree never fires for it - resolve its
        // x:ViewModel here, once its tree is built and the context is set. Nested views self-resolve on attach.
        ApplyViewModel();
        WindowWorkerService = UIAppContext.PlatformService.GetWindowWorker(context);
        WindowWorkerService.SetWindow(this);
    }

    protected virtual void InitializeComponent()
    {
        
    }

    public Vector2 ScreenToClient(Vector2 p)
    {
        var point = new NativePoint((int)p.X, (int)p.Y);
        Win32Interop.ScreenToClient(Handle, ref point);
        // Win32 returns PHYSICAL client px; the framework works in logical DIP -> divide by the DPI scale (identity at 100%).
        return new Vector2(point.X / DpiScale.X, point.Y / DpiScale.Y);
    }

    public Vector2 ClientToScreen(Vector2 p)
    {
        // p is logical DIP -> back to physical client px before handing to Win32; the returned screen coords stay physical.
        var point = new NativePoint((int)(p.X * DpiScale.X), (int)(p.Y * DpiScale.Y));
        Win32Interop.ClientToScreen(Handle, ref point);
        return point;
    }

    public bool ShouldDisplayWindow { get; protected set; }

    public void Initialize(IUIContext uiContext)
    {
        UIContext = uiContext;
        
    }
    public abstract void Show();
    public abstract void Close();
    public abstract void Hide();
        
    public abstract bool IsActive { get; internal set; }

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

    public IUIContext UIContext { get; private set; }

    public event SizeChangedEventHandler ClientSizeChanged
    {
        add => AddHandler(ClientSizeChangedEvent, value);
        remove => RemoveHandler(ClientSizeChangedEvent, value);
    }
        
    public event MSAALeveChangedHandler MSAALevelChanged
    {
        add => AddHandler(MSAALevelChangedEvent, value);
        remove => RemoveHandler(MSAALevelChangedEvent, value);
    }

    public event StateChangedHandler StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    public void SetHandle(IntPtr handle)
    {
        Handle = handle;
    }

    public void SetSurface(IntPtr surfaceHandle)
    {
        SurfaceHandle = surfaceHandle;
    }

    void IWindowInternals.OnSourceInitialized()
    {
        SourceInitialized?.Invoke(this, EventArgs.Empty);
        InvalidateMeasure();
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    protected void OnClosed()
    {
        var closingArgs = new WindowClosingEventArgs();
        Closing?.Invoke(this, closingArgs);
        if (!closingArgs.Cancel)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler<WindowClosingEventArgs> Closing;
    public event EventHandler<EventArgs> Closed;
    public event EventHandler<WindowRendererChangedEventArgs> RendererChanged;

    public event EventHandler<EventArgs> SourceInitialized;

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