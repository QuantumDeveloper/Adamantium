using Adamantium.Graphics.Core;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
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
    /// within the window. A popup registers itself here (via <see cref="IPopupHost"/>) while open. The layer is told which
    /// window owns it, so what it hosts can find the way back out - see PopupLayer.Owner.</summary>
    public PopupLayer PopupLayer { get; }

    protected WindowBase()
    {
        PopupLayer = new PopupLayer { Owner = this };
        // The window's content is a focus AREA, so Ctrl+Tab can leave a non-modal overlay for the page behind it and
        // come back to where the keyboard was - the overlays declare themselves areas too. See KeyboardNavigation.
        KeyboardNavigation.SetIsFocusArea(this, true);
    }

    public IReadOnlyList<IUIComponent> PopupRoots => PopupLayer.Roots;

    public void LayoutPopups() => PopupLayer.UpdateLayout(new Size(ClientWidth, ClientHeight));

    private bool _hoverHooked;

    /// <summary>Hover is a statement about what is under the cursor NOW, so it has to be re-decided when the CONTENT
    /// moves and the pointer does not: a list scrolled by the keyboard or the wheel slides its rows under a still
    /// cursor, and no Enter or Leave is ever sent. The window is where this belongs - it owns the tree the answer is
    /// hit-tested against - and a settled layout is exactly the moment the answer can have changed.</summary>
    private void HookHoverRefresh()
    {
        if (_hoverHooked) return;

        _hoverHooked = true;
        LayoutManager.GetOrCreate(this).LayoutUpdated += (_, _) => MouseDevice.CurrentDevice.RefreshMouseOver(this);
    }

    // Default/cancel routing: the window is the root, so unhandled Enter/Esc reach it last, after everything on the way
    // up has had its say. Enter activates the IsDefault button, Escape the IsCancel one - unless the focused element
    // already claimed the key. WPF Window default/cancel behaviour.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
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
            return;
        }

        // Escape with nothing to cancel gives the keyboard focus back: the ring is put out and the keyboard leaves the
        // control. Without this there was NO way to drop it - the ring only goes out when focus MOVES somewhere else,
        // and clicking empty space moves it nowhere, so a ring lit by one Tab stayed lit for good. Last in line by
        // construction: a dialog, a popup, an editor cancelling an edit all handle Escape on the way up and never get
        // here.
        if (e.Key == Key.Escape && FocusManager.Focused != null)
        {
            FocusManager.Release(FocusManager.Focused);
            e.Handled = true;
            return;
        }

        // The reading keys: PageDown / PageUp page the view, Home / End jump to its ends. Handled HERE rather than in
        // ScrollViewer because routed keys travel up from the FOCUSED element, and a ScrollViewer is deliberately not
        // focusable - so with the focus outside it, or nowhere at all (the ordinary reading state), it never sees them.
        //
        // Nothing is stolen: whatever wanted the key handled it on the way up - a text editor takes Home/End for the
        // caret, a list or a tree takes them for the selection - and only what nobody wanted reaches the window.
        //
        // SPACE is deliberately NOT here, though a browser pages with it. In a browser the focus normally rests on the
        // document; in an application it rests on a control, and space is the ACTIVATION key - a button, a toggle, a
        // tab, a drop-down all wait for it. Bound to scrolling as well, it would do one thing or the other depending on
        // where the focus happens to be, which is not a gesture anyone can predict.
        var scrolled = e.Key switch
        {
            Key.PageDown => ScrollNearest(v => v.PageVertically(false)),
            Key.PageUp => ScrollNearest(v => v.PageVertically(true)),
            Key.Home => ScrollNearest(v => v.ScrollToVerticalEdge(true)),
            Key.End => ScrollNearest(v => v.ScrollToVerticalEdge(false)),
            _ => false
        };
        if (scrolled) e.Handled = true;
    }

    // The viewer a reading key means: the one the focus is inside (innermost first - a list inside a page scrolls
    // itself), else the first on screen with somewhere to go, which is what "the page" means when the keyboard is
    // nowhere in particular. The action returns false when that viewer cannot move, and the search goes on.
    private bool ScrollNearest(Func<ScrollViewer, bool> scroll)
    {
        for (var node = FocusManager.Focused as IUIComponent; node != null; node = node.VisualParent)
            if (node is ScrollViewer focused && scroll(focused)) return true;

        return ScrollFirstScrollable(this, scroll);
    }

    private static bool ScrollFirstScrollable(IUIComponent root, Func<ScrollViewer, bool> scroll)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is ScrollViewer viewer && scroll(viewer)) return true;
            if (ScrollFirstScrollable(child, scroll)) return true;
        }
        return false;
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


    // Left/Top MOVE the window - they are not just remembered. Without the callback, assigning them changed a managed
    // number and the window stayed where it was, which is not what a window API means anywhere.
    public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d, PositionChangedCallback));

    public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
        typeof(Double), typeof(WindowBase), new PropertyMetadata(0d, PositionChangedCallback));

    private static void PositionChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // Before the OS window exists the value is simply remembered - it is read when the window is created.
        if (a is WindowBase window && !window._positionFromPlatform)
        {
            window.WindowWorkerService?.SetPosition(window.Left, window.Top);
        }
    }

    private bool _positionFromPlatform;

    /// <summary>The window was moved by the PLATFORM - a caption drag, Aero Snap, a monitor going away - and says where
    /// it ended up. Without this Left/Top only ever hold what WE last assigned: the OS move loop swallows the gesture,
    /// so after any drag the window's own idea of its position was wherever it was put programmatically, which is what
    /// a saved layout then wrote down.
    /// <para>Assigned without moving the window again: the position is already true, and echoing it back to the OS
    /// mid-drag fights the move loop.</para></summary>
    public void UpdatePositionFromPlatform(double left, double top)
    {
        if (Left.Equals(left) && Top.Equals(top)) return;

        _positionFromPlatform = true;
        try
        {
            Left = left;
            Top = top;
        }
        finally
        {
            _positionFromPlatform = false;
        }
    }
        
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

    // --- Overlay window traits ------------------------------------------------------------------------------------
    // What an OVERLAY needs and an ordinary window does not: to float above everything, to let clicks through to what is
    // underneath, never to take focus, and to have a background that is not there. A docking compass needs all four - it
    // must sit above the window being DRAGGED, which nothing living inside a window can ever do.
    // Read ONCE by the platform worker at create time, along with the chrome flags: native window styles are fixed then.

    // Each of these re-applies itself to the LIVE window, so they behave as properties rather than as arguments that
    // only matter before the window exists.
    private static void OverlayTraitChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        (component as WindowBase)?.WindowWorkerService?.UpdateOverlayTraits();
    }

    /// <summary>Stays above other windows.</summary>
    public static readonly AdamantiumProperty TopmostProperty = AdamantiumProperty.Register(nameof(Topmost),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(false, OverlayTraitChanged));

    /// <summary>Clicks pass straight through to whatever is behind. The window is seen and never touched.</summary>
    public static readonly AdamantiumProperty TransparentToInputProperty = AdamantiumProperty.Register(nameof(TransparentToInput),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(false, OverlayTraitChanged));

    /// <summary>False to show without taking focus - an overlay that stole activation would end the very drag it is
    /// there to help with. Read when the window is shown.</summary>
    public static readonly AdamantiumProperty ActivateOnShowProperty = AdamantiumProperty.Register(nameof(ActivateOnShow),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(true, OverlayTraitChanged));

    /// <summary>The OS frame around the window: the ambient drop shadow and the accent outline. On by default - it is
    /// what makes a window look like a window. An overlay turns it off.</summary>
    public static readonly AdamantiumProperty ShowWindowBorderProperty = AdamantiumProperty.Register(nameof(ShowWindowBorder),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(true, OverlayTraitChanged));

    public bool ShowWindowBorder
    {
        get => GetValue<bool>(ShowWindowBorderProperty);
        set => SetValue(ShowWindowBorderProperty, value);
    }

    /// <summary>Per-pixel transparency: the window's rendering is composed by the desktop WITH its alpha, so translucent
    /// brushes and antialiased edges show what is behind them.
    /// <para>Settable at any time. The swapchain picks its composite-alpha mode when it is CREATED, so changing this
    /// cannot take effect in place - it marks the renderer stale and the swapchain is rebuilt at the next frame
    /// boundary, which is the same path a resize takes.</para></summary>
    public static readonly AdamantiumProperty UseTransparentCompositionProperty = AdamantiumProperty.Register(nameof(UseTransparentComposition),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(false, TransparentCompositionChanged));

    // Never rebuild from the setter: it is called on whatever thread set the property, while the render thread may be
    // mid-frame with the swapchain it is about to destroy. Marking it stale hands the rebuild to BeginDraw, which runs
    // before the frame draws and is serialized with submit/present.
    private static void TransparentCompositionChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        // The metadata callback fires on EVERY write, not only on a change of value - and a rebuild costs a device-idle
        // wait plus every render target, so a write that said nothing must not buy one.
        if (Equals(e.OldValue, e.NewValue)) return;

        (component as WindowBase)?.Renderer?.InvalidatePresenter();
    }

    /// <summary>Uniform translucency of the whole window, 0..1. Composed by the desktop, so the content underneath
    /// shows through live - which is what a docking preview rectangle is.</summary>
    public static readonly AdamantiumProperty WindowOpacityProperty = AdamantiumProperty.Register(nameof(WindowOpacity),
        typeof(double), typeof(WindowBase), new PropertyMetadata(1.0, OverlayTraitChanged));

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

    public static readonly AdamantiumProperty ResizeModeProperty = AdamantiumProperty.Register(nameof(ResizeMode),
        typeof(WindowResizeMode), typeof(WindowBase), new PropertyMetadata(WindowResizeMode.CanResize, ResizeModeChangedCallback));

    // Whether this window is the active (focused) one - set by the platform on WM_ACTIVATE. An AdamantiumProperty so the
    // theme can trigger on it (accent title bar / border when active, dimmed when not), AffectsRender to repaint the swap.
    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // True while a theme swap's cascade is still draining (see IThemeManager.IsThemeChanging). Mirrored onto the window as
    // an AdamantiumProperty for one reason: it is what a THEME triggers on to raise its own busy overlay in the window
    // template. The engine owns the STATE; what is shown - and whether anything is shown at all - is the theme's call.
    public static readonly AdamantiumProperty IsThemeChangingProperty = AdamantiumProperty.Register(nameof(IsThemeChanging),
        typeof(bool), typeof(WindowBase), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsThemeChanging
    {
        get => GetValue<bool>(IsThemeChangingProperty);
        private set => SetValue(IsThemeChangingProperty, value);
    }

    // The caption a theme's busy overlay shows. A window property (not baked into the template) so the indicator is a
    // GENERIC busy overlay whose text an app sets for any wait, not only the theme swap. The theme provides the default.
    public static readonly AdamantiumProperty LoadingIndicatorTextProperty = AdamantiumProperty.Register(
        nameof(LoadingIndicatorText), typeof(string), typeof(WindowBase), new PropertyMetadata(string.Empty));

    public string LoadingIndicatorText
    {
        get => GetValue<string>(LoadingIndicatorTextProperty);
        set => SetValue(LoadingIndicatorTextProperty, value);
    }

    // A plain .NET event (not a routed one): the platform worker keeps a thread-safe ResizeMode snapshot for the hit-test
    // and refreshes it here when the mode changes at runtime (e.g. toggling grip-resize on).
    public event EventHandler ResizeModeChanged;

    private static void ResizeModeChangedCallback(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (adamantiumComponent is WindowBase component)
            component.ResizeModeChanged?.Invoke(component, EventArgs.Empty);
    }

    // MahApps-style caption command bars, forwarded to the TitleBar by the default Window template. Bind a view-model's
    // collection of WindowCommand items to show quick actions in the title bar (left of it / right, before the buttons).
    public static readonly AdamantiumProperty LeftWindowCommandsProperty = AdamantiumProperty.Register(nameof(LeftWindowCommands),
        typeof(System.Collections.IEnumerable), typeof(WindowBase), new PropertyMetadata(null));

    public static readonly AdamantiumProperty RightWindowCommandsProperty = AdamantiumProperty.Register(nameof(RightWindowCommands),
        typeof(System.Collections.IEnumerable), typeof(WindowBase), new PropertyMetadata(null));

    // Forwarded to TitleBar.LeadingContent by the default template.
    public static readonly AdamantiumProperty TitleBarLeadingContentProperty = AdamantiumProperty.Register(
        nameof(TitleBarLeadingContent), typeof(object), typeof(WindowBase),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Content placed at the start of the custom caption, before the window commands.</summary>
    public object TitleBarLeadingContent
    {
        get => GetValue(TitleBarLeadingContentProperty);
        set => SetValue(TitleBarLeadingContentProperty, value);
    }

    // Window icon/logo shown at the left of the custom title bar (forwarded to the TitleBar by the default template).
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(WindowBase), new PropertyMetadata(null));

    // Caption background for the ACTIVE (focused) and INACTIVE window - the default template paints the TitleBar with
    // InactiveTitleBarBackground and swaps to TitleBarBackground while IsActive. Theme sets the defaults (accent / neutral);
    // a user can override either on the window (e.g. a brand colour when focused, a custom dim when not).
    /// <summary>Height of the custom-chrome caption. The WINDOW owns this number and the theme's title bar measures
    /// itself by it - not the other way round: code that needs the caption (positioning a window under the cursor that
    /// grabbed it, hit-testing the drag area) must not have to reach into a template part, and a restyle must not be
    /// able to drift away from what the window believes its caption to be.</summary>
    public static readonly AdamantiumProperty TitleBarHeightProperty = AdamantiumProperty.Register(nameof(TitleBarHeight),
        typeof(double), typeof(WindowBase), new PropertyMetadata(36.0, PropertyMetadataOptions.AffectsMeasure));

    public double TitleBarHeight
    {
        get => GetValue<double>(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    public static readonly AdamantiumProperty TitleBarBackgroundProperty = AdamantiumProperty.Register(nameof(TitleBarBackground),
        typeof(Brush), typeof(WindowBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty InactiveTitleBarBackgroundProperty = AdamantiumProperty.Register(nameof(InactiveTitleBarBackground),
        typeof(Brush), typeof(WindowBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public Brush TitleBarBackground
    {
        get => GetValue<Brush>(TitleBarBackgroundProperty);
        set => SetValue(TitleBarBackgroundProperty, value);
    }

    public Brush InactiveTitleBarBackground
    {
        get => GetValue<Brush>(InactiveTitleBarBackgroundProperty);
        set => SetValue(InactiveTitleBarBackgroundProperty, value);
    }

    // Caption FOREGROUND (title text + caption-button glyphs) for the ACTIVE and INACTIVE window - mirrors the background
    // pair. Default active = the theme's on-accent contrast colour (white on a dark accent, black on a light one) so the
    // caption reads on an accent-painted bar; inactive = the neutral primary text colour. Overridable per window.
    public static readonly AdamantiumProperty TitleBarForegroundProperty = AdamantiumProperty.Register(nameof(TitleBarForeground),
        typeof(Brush), typeof(WindowBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty InactiveTitleBarForegroundProperty = AdamantiumProperty.Register(nameof(InactiveTitleBarForeground),
        typeof(Brush), typeof(WindowBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public Brush TitleBarForeground
    {
        get => GetValue<Brush>(TitleBarForegroundProperty);
        set => SetValue(TitleBarForegroundProperty, value);
    }

    public Brush InactiveTitleBarForeground
    {
        get => GetValue<Brush>(InactiveTitleBarForegroundProperty);
        set => SetValue(InactiveTitleBarForegroundProperty, value);
    }

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
        // Only concrete numbers are a size to push to the OS window or to report; the default is NaN (auto).
        if (e.OldValue is not double oldWidth || double.IsNaN(oldWidth) || e.NewValue is not double newWidth)
            return;

        // NO forced full walks here any more. A client-size change (drag-resize, maximize) used to demand a whole-tree
        // re-record on every frame until the layout settled, because "parts of that settle never mark the render dirty" -
        // ghosts of the old layout survived otherwise (tiles at stale positions, a scrollbar stripe mid-window).
        //
        // Those unmarked writes have since been found and fixed: a control leaving the drawn set through the DEFAULT value of
        // Visibility named nobody (the auto-hide scrollbar, and every recycled container - see UIComponent.OnVisibilityChanged),
        // and Panel's Children collection changed the visual children without naming them either. The settle marks honestly now,
        // and the resize is just structure changing - so it SPLICES (see ViewportResize_Splices_AndKeepsDrawnGeometryFresh).
        //
        // This matters exactly where it hurts: a maximize to 4K realizes thousands of tiles over many frames, and the forced
        // walk re-recorded all ~20 000 components on every one of them - 100-200 ms per frame of the heaviest thing the app
        // does. Theme and DPI swaps still force (they rebuild templates through paths no mark can name).

        // Tell the OS window, exactly as a Left/Top change does. Without this the client size was a managed number the
        // window itself never followed: it kept whatever it was created with, so nothing could be resized from code
        // after it opened (found on the docking compass overlay, which is re-sized to the area it covers).
        component.WindowWorkerService?.SetSize(component.ClientWidth, component.ClientHeight);

        old.Width = oldWidth;
        old.Height = component.Height;

        var newSize = new Size(newWidth, component.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = ClientSizeChangedEvent;
        component.RaiseEvent(args);
    }
        
    private static void ClientHeightChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumAdamantiumComponent is WindowBase component)) return;
        // See ClientWidthChangedCallBack.
        if (e.OldValue is not double oldHeight || double.IsNaN(oldHeight) || e.NewValue is not double newHeight)
            return;

        // No forced full walks - see ClientWidthChangedCallBack: the resize settle marks honestly now, so it splices.

        component.WindowWorkerService?.SetSize(component.ClientWidth, component.ClientHeight);

        var old = new Size(component.Width, oldHeight);
        var newSize = new Size(component.Width, newHeight);
        var args = new SizeChangedEventArgs(old, newSize, false, true);
        args.RoutedEvent = ClientSizeChangedEvent;
        component?.RaiseEvent(args);
    }

    /// <summary>Where the window's top-left sits on the desktop, in PHYSICAL pixels - the same units as
    /// <see cref="PointToScreen"/> and <c>Mouse.ScreenCoordinates</c>, so a window can be put where a cursor is without
    /// a conversion in between.
    /// <para>Deliberately NOT logical, unlike <see cref="ClientWidth"/>. A window's SIZE has one scale - its monitor's.
    /// Its POSITION does not: the scale belongs to the monitor the point lands on, and which monitor that is can only be
    /// known once the point is physical. Measured: a torn-off window placed in logical units was born at the origin, on
    /// the primary monitor, took its 100% scale, and landed at a third of the way to the cursor on a 4K display.</para></summary>
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

    // Published by a ResizeGripper on layout; read by the platform hit-test (OS message thread). Plain Rect for the same
    // thread-safety reason as CaptionDragRect. Empty = no grip / not in grip-resize mode.
    public Rect ResizeGripRect { get; set; }

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

    public abstract Vector2 PointToClient(PixelPoint point);
    public abstract PixelPoint PointToScreen(Vector2 point);

    /// <summary>Where this window sits on the desktop, as ONE typed value. <see cref="Left"/>/<see cref="Top"/> hold the
    /// same thing as bindable numbers (a position is authored and serialized as two numbers); everything that COMPUTES a
    /// position uses this, so the units cannot be lost on the way - see <see cref="PixelPoint"/>.</summary>
    public PixelPoint Position
    {
        get => new(Left, Top);
        set
        {
            Left = value.X;
            Top = value.Y;
        }
    }
    public void AttachContextAndInitialize(IUIContext context)
    {
        UIContext = context;
        InitializeComponent();
        // The root window has no logical parent, so OnAttachedToLogicalTree never fires for it - resolve its
        // x:ViewModel here, once its tree is built and the context is set. Nested views self-resolve on attach.
        ApplyViewModel();
        WindowWorkerService = UIAppContext.PlatformService.GetWindowWorker(context);
        WindowWorkerService.SetWindow(this);

        var themes = UIAppContext.Current?.ThemeManager;
        if (themes != null)
        {
            themes.ThemeChanging += OnThemeChanging;
            themes.ThemeChanged += OnThemeChanged;
            IsThemeChanging = themes.IsThemeChanging;   // a window opened mid-swap already shows the busy state
        }
    }

    private void OnThemeChanging(object sender, ThemeChangedEventArgs e) => IsThemeChanging = true;

    private void OnThemeChanged(object sender, ThemeChangedEventArgs e) => IsThemeChanging = false;

    protected virtual void InitializeComponent()
    {
        
    }

    public Vector2 ScreenToClient(PixelPoint p)
    {
        var point = new NativePoint((int)p.X, (int)p.Y);
        Win32Interop.ScreenToClient(Handle, ref point);
        // Win32 returns PHYSICAL client px; the framework works in logical DIP -> divide by THIS window's scale.
        return new PixelPoint(point.X, point.Y).ToLogical(DpiScale);
    }

    /// <summary>A point of this window's client area (LOGICAL) to a desktop point (PHYSICAL). The asymmetry is the
    /// desktop's: monitors can differ in scale, so a screen point has no one scale to be logical in. Convert with the
    /// scale of the window the point concerns - see <see cref="Left"/>.</summary>
    public PixelPoint ClientToScreen(Vector2 p)
    {
        // p is logical DIP -> back to physical client px before handing to Win32; the returned screen coords stay physical.
        var physical = PixelPoint.FromLogical(p, DpiScale);
        var point = new NativePoint((int)physical.X, (int)physical.Y);
        Win32Interop.ClientToScreen(Handle, ref point);
        return new PixelPoint(point.X, point.Y);
    }

    public bool ShouldDisplayWindow { get; protected set; }

    public void Initialize(IUIContext uiContext)
    {
        UIContext = uiContext;
        
    }
    public abstract void Show();
    public abstract void Close();
    public abstract void Hide();

    /// <summary>Bring this window to the foreground (restoring it if minimized). Platform-specific via the window worker.</summary>
    public void Activate() => WindowWorkerService?.Activate();

    /// <summary>Raise this window above the others WITHOUT taking focus - the mid-drag-safe counterpart of
    /// <see cref="Activate"/>, which would cost the drag its mouse capture.</summary>
    public void BringToFront() => WindowWorkerService?.RaiseWithoutActivation();

    /// <summary>Enter/leave RELATIVE mouse mode (hidden, centred cursor + synthesized raw delta) for a hosted game's
    /// mouse-look. Driven by a <see cref="Panels.RenderTargetPanel"/> per its <c>MouseLookMode</c>; delegates to the
    /// platform worker.</summary>
    public void SetRelativeMouseMode(bool enabled, PixelPoint restoreScreen) =>
        WindowWorkerService?.SetRelativeMouseMode(enabled, restoreScreen);
        
    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        internal set => SetValue(IsActiveProperty, value);
    }

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
        // Seed the initial layout via the manager, NOT InvalidateMeasure(): a fresh root is IsMeasureValid=false, so that
        // method's early-return drops the enqueue - the first real layout would otherwise defer to the first user input.
        LayoutManager.GetOrCreate(this).InvalidateMeasure(this);
        HookHoverRefresh();
    }

    public void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>The OS is moving this window, once per step of its move loop. The only signal available during a caption
    /// drag: the platform's loop owns the mouse, so no managed move or button-up arrives until it ends. A docking host
    /// listens here to decide where the window would land.</summary>
    public event EventHandler WindowMoving;

    /// <summary>The move loop ended - the button is up and the window has settled. Where a drop is committed.</summary>
    public event EventHandler WindowMoveCompleted;

    public void RaiseWindowMoving() => WindowMoving?.Invoke(this, EventArgs.Empty);

    public void RaiseWindowMoveCompleted() => WindowMoveCompleted?.Invoke(this, EventArgs.Empty);

    protected void OnClosed()
    {
        var closingArgs = new WindowClosingEventArgs();
        Closing?.Invoke(this, closingArgs);
        if (!closingArgs.Cancel)
        {
            // The theme manager outlives every window, so a closed one that stayed subscribed would be kept alive by it.
            var themes = UIAppContext.Current?.ThemeManager;
            if (themes != null)
            {
                themes.ThemeChanging -= OnThemeChanging;
                themes.ThemeChanged -= OnThemeChanged;
            }
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
            // A DPI change re-scales the renderer (RenderScale/projection) and re-lays-out the tree over the next few
            // frames - and, like a theme swap, parts of that settle through paths that never mark the render dirty. A
            // Clean-frame op-replay then keeps showing the OLD-scale content (shrunken in the corner) until an unrelated
            // mark (a mouse move's hover) forces a walk. Force full render walks until the layout settles.
            VisualTreeNotifications.RaiseStateSwapStarted();
            DpiChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler<EventArgs> DpiChanged;
}
