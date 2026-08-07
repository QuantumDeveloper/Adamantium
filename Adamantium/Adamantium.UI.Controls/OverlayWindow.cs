using Adamantium.Navigation;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A window that lives INSIDE its parent window (never a separate OS window) - the analog of MahApps' SimpleChildWindow.
/// It is a themed, rounded card with a title bar (title + close button) and content. It is NOT declared in the layout
/// tree: it is created and shown through an <see cref="OverlayWindowManager"/> (see the ShowOverlayWindow* extensions),
/// which places it on the parent window's overlay, cascades multiple windows, and raises the front one on interaction -
/// so several can overlap and be dragged over each other like normal desktop windows. Drag the title bar to move it
/// (<see cref="AllowMove"/>); it closes via the x button, Escape (<see cref="CloseByEscape"/>) or, when modal, a click on
/// the dim overlay (<see cref="CloseOnOverlay"/>). <see cref="Close(object)"/> sets <see cref="Result"/> - what the
/// awaited Show call returns - and raises <see cref="Closing"/> (cancelable) then <see cref="Closed"/>.
/// </summary>
public class OverlayWindow : ContentControl
{
    // The manager owns the show/close lifecycle, so IsOpen is read-only state a consumer can bind to.
    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsOpen),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsActive),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(object), typeof(OverlayWindow), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(OverlayWindow), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ShowTitleBarProperty = AdamantiumProperty.Register(nameof(ShowTitleBar),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(true));

    public static readonly AdamantiumProperty CanCloseProperty = AdamantiumProperty.Register(nameof(CanClose),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(true));

    public static readonly AdamantiumProperty AllowMoveProperty = AdamantiumProperty.Register(nameof(AllowMove),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(true));

    public static readonly AdamantiumProperty IsModalProperty = AdamantiumProperty.Register(nameof(IsModal),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false, OnIsModalChanged));

    // A modal overlay TRAPS Tab: while it is up the content behind is dimmed and unclickable, so a Tab that walked out
    // into it would put the keyboard somewhere the mouse cannot follow - focused, invisible, and with no way back
    // except more Tab. A non-modal overlay is an ordinary panel and keeps the plain order.
    private static void OnIsModalChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
        KeyboardNavigation.SetTabNavigation(a, (bool)e.NewValue
            ? KeyboardNavigationMode.Cycle
            : KeyboardNavigationMode.Continue);

    public static readonly AdamantiumProperty CloseOnOverlayProperty = AdamantiumProperty.Register(nameof(CloseOnOverlay),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CloseByEscapeProperty = AdamantiumProperty.Register(nameof(CloseByEscape),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(true));

    static OverlayWindow()
    {
        // A card stands off its content; the chrome property itself is Control's, only the default differs.
        PaddingProperty.OverrideMetadata(typeof(OverlayWindow),
            new PropertyMetadata(new Thickness(16),
                PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));
    }

    public static readonly AdamantiumProperty OverlayBrushProperty = AdamantiumProperty.Register(nameof(OverlayBrush),
        typeof(Brush), typeof(OverlayWindow), new PropertyMetadata(null));

    public static readonly AdamantiumProperty TitleBarBackgroundProperty = AdamantiumProperty.Register(nameof(TitleBarBackground),
        typeof(Brush), typeof(OverlayWindow), new PropertyMetadata(Brushes.Transparent));

    public static readonly AdamantiumProperty TitleForegroundProperty = AdamantiumProperty.Register(nameof(TitleForeground),
        typeof(Brush), typeof(OverlayWindow), new PropertyMetadata(null));

    public static readonly AdamantiumProperty OpenDurationProperty = AdamantiumProperty.Register(nameof(OpenDuration),
        typeof(double), typeof(OverlayWindow), new PropertyMetadata(0.18));

    public static readonly AdamantiumProperty CanResizeProperty = AdamantiumProperty.Register(nameof(CanResize),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanPinProperty = AdamantiumProperty.Register(nameof(CanPin),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsPinnedProperty = AdamantiumProperty.Register(nameof(IsPinned),
        typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false, OnIsPinnedChanged));

    public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.Register(nameof(Left),
        typeof(double), typeof(OverlayWindow), new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.Register(nameof(Top),
        typeof(double), typeof(OverlayWindow), new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    /// <summary>True while the window is shown on the overlay. Read-only: the manager owns the lifecycle.</summary>
    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        private set => SetValue(IsOpenProperty, value);
    }

    /// <summary>True when this is the front-most / last-interacted window (like <c>Window.IsActive</c>); the title bar dims
    /// when false. Read-only - the <see cref="OverlayWindowManager"/> maintains it.</summary>
    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        private set => SetValue(IsActiveProperty, value);
    }

    internal void SetActive(bool active) => IsActive = active;

    /// <summary>Title content shown on the left of the title bar.</summary>
    public object Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional icon shown before the title.</summary>
    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool ShowTitleBar
    {
        get => GetValue<bool>(ShowTitleBarProperty);
        set => SetValue(ShowTitleBarProperty, value);
    }

    /// <summary>When false, the window cannot be closed by the user - the x button is hidden and Escape / an overlay
    /// click do nothing (programmatic <see cref="Close()"/> still works). Default true.</summary>
    public bool CanClose
    {
        get => GetValue<bool>(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>When true, a resize grip appears in the bottom-right corner. Default false.</summary>
    public bool CanResize
    {
        get => GetValue<bool>(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    /// <summary>When true, a pin button appears on the title bar; clicking it toggles <see cref="IsPinned"/>. Default false.</summary>
    public bool CanPin
    {
        get => GetValue<bool>(CanPinProperty);
        set => SetValue(CanPinProperty, value);
    }

    /// <summary>When true, the window stays above the non-pinned windows (always on top). Toggled by the pin button.</summary>
    public bool IsPinned
    {
        get => GetValue<bool>(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>When true (default) the title bar can be dragged to move the window within the parent.</summary>
    public bool AllowMove
    {
        get => GetValue<bool>(AllowMoveProperty);
        set => SetValue(AllowMoveProperty, value);
    }

    /// <summary>When true the dim overlay blocks input to everything behind this window (a modal dialog). Default false -
    /// a normal window that lets the ones behind it stay usable.</summary>
    public bool IsModal
    {
        get => GetValue<bool>(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>When modal, a click on the dim overlay (outside the window) closes it. Default false.</summary>
    public bool CloseOnOverlay
    {
        get => GetValue<bool>(CloseOnOverlayProperty);
        set => SetValue(CloseOnOverlayProperty, value);
    }

    /// <summary>When true (default) the Escape key closes the window while it is the front-most one.</summary>
    public bool CloseByEscape
    {
        get => GetValue<bool>(CloseByEscapeProperty);
        set => SetValue(CloseByEscapeProperty, value);
    }

    /// <summary>The dimming overlay used when <see cref="IsModal"/> is true. Defaults to the theme smoke brush.</summary>
    public Brush OverlayBrush
    {
        get => GetValue<Brush>(OverlayBrushProperty);
        set => SetValue(OverlayBrushProperty, value);
    }

    /// <summary>The title bar fill. Defaults to the theme accent brush.</summary>
    public Brush TitleBarBackground
    {
        get => GetValue<Brush>(TitleBarBackgroundProperty);
        set => SetValue(TitleBarBackgroundProperty, value);
    }

    /// <summary>The title text/icon colour (readable on <see cref="TitleBarBackground"/>).</summary>
    public Brush TitleForeground
    {
        get => GetValue<Brush>(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }

    /// <summary>Open/close animation duration in seconds.</summary>
    public double OpenDuration
    {
        get => GetValue<double>(OpenDurationProperty);
        set => SetValue(OpenDurationProperty, value);
    }

    /// <summary>Where the window first appears - centred+cascaded, or a manual top-left. Set before it is shown.</summary>
    public OverlayStartupLocation StartupLocation { get; set; } = OverlayStartupLocation.CenterOwner;

    /// <summary>The window's absolute left in the parent window's coordinates (like <c>Window.Left</c>). Two-way bindable:
    /// dragging updates it, and assigning it (or the bound view-model property) moves the window. Used as the opening X
    /// when <see cref="StartupLocation"/> is Manual.</summary>
    public double Left
    {
        get => GetValue<double>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    /// <summary>The window's absolute top in the parent window's coordinates (like <c>Window.Top</c>). See <see cref="Left"/>.</summary>
    public double Top
    {
        get => GetValue<double>(TopProperty);
        set => SetValue(TopProperty, value);
    }

    /// <summary>The result set by the last <see cref="Close(object)"/> - what the awaited Show call returns.</summary>
    public object Result { get; private set; }

    /// <summary>Raised before the window closes; set <see cref="OverlayWindowClosingEventArgs.Cancel"/> to keep it open.</summary>
    public event EventHandler<OverlayWindowClosingEventArgs> Closing;

    /// <summary>Raised after the window has closed (its close animation finished). <see cref="Result"/> holds the result.</summary>
    public event EventHandler Closed;

    /// <summary>Raised when the window has been shown on the overlay.</summary>
    public event EventHandler Opened;

    private OverlayWindowManager _manager;
    private Button _closeButton;
    private Button _pinButton;
    private InputUIComponent _dragArea;   // the transparent title-bar strip that starts the drag
    private InputUIComponent _resizeGrip;
    private bool _dragging;
    private Vector2 _dragStart;           // pointer at press, in the PARENT WINDOW's space (which does not move)
    private double _dragBaseLeft, _dragBaseTop;   // the card's top-left at press (the anchor the delta is added to)
    private double _dragParentW, _dragParentH;    // parent window size at press (to clamp the card inside)
    private double _dragCardW, _dragCardH;        // card size at press
    private const double MinCardWidth = 300;    // matches PART_Window MinWidth in the theme
    private const double MinCardHeight = 120;
    private bool _resizing;
    private Vector2 _resizeStart;
    private double _resizeBaseW, _resizeBaseH;
    private double _resizeParentW, _resizeParentH;   // parent window size at resize start
    private double _resizeLeft, _resizeTop;          // the card's top-left in window space at resize start (kept fixed)

    private static void OnIsPinnedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is OverlayWindow w) w._manager?.OnPinnedChanged(w);
    }

    // Left/Top are the card's absolute top-left; a change (from a drag write, an assignment, or a two-way binding to the
    // view model) moves the popup there. Skipped before the popup exists - the manager applies the initial placement.
    private static void OnPositionChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is OverlayWindow w) w.ApplyPositionToPopup();
    }

    private void ApplyPositionToPopup()
    {
        if (HostPopup == null) return;
        HostPopup.Placement = PlacementMode.Relative;   // Relative anchors the card's top-left at (offset), not centred
        HostPopup.HorizontalOffset = Left;
        HostPopup.VerticalOffset = Top;
    }

    // The overlay layer hosts each window as its OWN card-sized popup (like a menu/tooltip), so a click outside the card
    // falls through to the content behind - a full-window host popup would absorb every click (see OverlayWindowManager).
    // The card is positioned by this popup's offsets (cascade + drag); the manager owns the popup lifecycle.
    internal Popup HostPopup { get; set; }
    internal Popup ScrimPopup { get; set; }   // the dim backdrop popup, only when this window is modal

    public OverlayWindow()
    {
        // Any press within the window raises it to the front (like clicking a desktop window). MouseLeftButtonDown
        // bubbles, so a press on a child that does not handle it still reaches here.
        MouseLeftButtonDown += (_, _) => _manager?.BringToFront(this);
        // A window is a place the keyboard can be sent to as a whole: Ctrl+Tab steps between it, the other overlays and
        // the page behind, and comes back to where the keyboard was in each. A MODAL one is exempt by its own Tab trap.
        KeyboardNavigation.SetIsFocusArea(this, true);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        if (_pinButton != null) _pinButton.Click -= OnPinClick;
        if (_dragArea != null)
        {
            _dragArea.MouseLeftButtonDown -= OnDragPress;
            _dragArea.MouseMove -= OnDragMove;
            _dragArea.MouseLeftButtonUp -= OnDragRelease;
        }
        if (_resizeGrip != null)
        {
            _resizeGrip.MouseLeftButtonDown -= OnResizePress;
            _resizeGrip.MouseMove -= OnResizeMove;
            _resizeGrip.MouseLeftButtonUp -= OnResizeRelease;
        }

        _dragArea = GetTemplateChild("PART_TitleThumb") as InputUIComponent;
        _closeButton = GetTemplateChild("PART_CloseButton") as Button;
        _pinButton = GetTemplateChild("PART_PinButton") as Button;
        _resizeGrip = GetTemplateChild("PART_ResizeGrip") as InputUIComponent;

        if (_closeButton != null) _closeButton.Click += OnCloseClick;
        if (_pinButton != null) _pinButton.Click += OnPinClick;
        if (_dragArea != null)
        {
            _dragArea.MouseLeftButtonDown += OnDragPress;
            _dragArea.MouseMove += OnDragMove;
            _dragArea.MouseLeftButtonUp += OnDragRelease;
        }
        if (_resizeGrip != null)
        {
            _resizeGrip.Cursor = Cursors.SizeNWSE;   // diagonal resize cursor on hover, so the grip reads as resizable
            _resizeGrip.MouseLeftButtonDown += OnResizePress;
            _resizeGrip.MouseMove += OnResizeMove;
            _resizeGrip.MouseLeftButtonUp += OnResizeRelease;
        }
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate. The guarded
    /// unhooks at the top of OnApplyTemplate cover a template that is REPLACED; this covers one that is dropped.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        if (_pinButton != null) _pinButton.Click -= OnPinClick;
        if (_dragArea != null)
        {
            _dragArea.MouseLeftButtonDown -= OnDragPress;
            _dragArea.MouseMove -= OnDragMove;
            _dragArea.MouseLeftButtonUp -= OnDragRelease;
        }
        if (_resizeGrip != null)
        {
            _resizeGrip.MouseLeftButtonDown -= OnResizePress;
            _resizeGrip.MouseMove -= OnResizeMove;
            _resizeGrip.MouseLeftButtonUp -= OnResizeRelease;
        }
        _closeButton = null;
        _pinButton = null;
        _dragArea = null;
        _resizeGrip = null;
    }

    /// <summary>Closes the window with no result (same as the x button).</summary>
    public void Close() => Close(null);

    /// <summary>Closes the window, setting <see cref="Result"/>, unless a <see cref="Closing"/> handler cancels it.</summary>
    public void Close(object result)
    {
        if (!IsOpen) return;
        var args = new OverlayWindowClosingEventArgs(result);
        Closing?.Invoke(this, args);
        if (args.Cancel) return;

        Result = result;
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);   // the manager listens on Closed to pull it off the overlay
    }

    // --- called by the manager ---

    internal void NotifyShown(OverlayWindowManager manager)
    {
        _manager = manager;
        IsOpen = true;

        // A subtle scale-in "pop". The transform promotes the card to a compositor motion node while it plays; the compositor
        // now un-promotes it on completion (Compositor.Release), so the card returns to a static, world-baked node afterwards
        // instead of full-re-recording every frame. FillBehavior.Stop clears the scale back to 1 at the end.
        if (OpenDuration > 0)
        {
            var pop = RenderTransform ?? new Transform();
            RenderTransform = pop;
            RenderTransformOrigin = new Vector2(0.5f, 0.5f);   // scale about the centre
            var duration = TimeSpan.FromSeconds(OpenDuration);
            pop.BeginAnimation(Transform.ScaleXProperty, new DoubleAnimation { From = 0.85, To = 1, Duration = duration, FillBehavior = FillBehavior.Stop });
            pop.BeginAnimation(Transform.ScaleYProperty, new DoubleAnimation { From = 0.85, To = 1, Duration = duration, FillBehavior = FillBehavior.Stop });
        }

        // An opening window takes the keyboard with it - that is what opening a window means, modal or not. Without it
        // the focus stayed on the page BEHIND, still holding the ring, so the first Tab walked the background and this
        // window's own buttons could only be reached with the mouse. It also decides whether ESCAPE works on the first
        // press: a key routes up from the focused element, so while the focus is still behind us the press reaches the
        // parent window's own Escape - which gives the focus back and marks the key handled - and we never hear it. The
        // content may not be built yet (the host is filling it as we speak), so the attempt repeats over the next few
        // layout passes and stops as soon as it lands.
        _focusTries = FocusAttempts;

        Opened?.Invoke(this, EventArgs.Empty);
    }

    private const int FocusAttempts = 8;
    private int _focusTries;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        TakeKeyboard();
        return size;
    }

    /// <summary>Take the keyboard back - what the window BEHIND a closing one does. Immediately, since this window is
    /// already built and laid out, with the same retry armed behind it in case it is not.</summary>
    internal void TakeKeyboardBack()
    {
        _focusTries = FocusAttempts;
        TakeKeyboard();
    }

    private void TakeKeyboard()
    {
        if (_focusTries <= 0) return;
        _focusTries--;

        // Already in here (the content focused itself, or the person clicked something): leave it alone.
        for (IUIComponent node = FocusManager.Focused as IUIComponent; node != null; node = node.VisualParent)
        {
            if (!ReferenceEquals(node, this)) continue;
            _focusTries = 0;
            return;
        }

        if (KeyboardNavigation.MoveInto(this)) _focusTries = 0;
    }

    // --- input ---

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // Drag moves the window by writing Left/Top (its absolute top-left). The delta is measured in the PARENT WINDOW's space
    // (a fixed frame) - NOT the card's, which moves as we drag it. The base is the card's real top-left, so it works whether
    // the window was centred or explicitly placed; the first drag of a centred window switches it to explicit placement at
    // its current spot, so it doesn't jump.
    private void OnDragPress(object sender, MouseButtonEventArgs e)
    {
        if (!AllowMove || HostPopup?.PlacementTarget is not IInputComponent frame) return;
        _dragging = true;
        e.Handled = true;
        _dragStart = e.GetPosition(frame);
        _dragBaseLeft = WorldTransform.TranslationVector.X;
        _dragBaseTop = WorldTransform.TranslationVector.Y;
        _dragParentW = frame.RenderSize.Width;
        _dragParentH = frame.RenderSize.Height;
        _dragCardW = RenderSize.Width;
        _dragCardH = RenderSize.Height;
        _dragArea.CaptureMouse();
        _manager?.BringToFront(this);
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || HostPopup?.PlacementTarget is not IInputComponent frame) return;
        var delta = e.GetPosition(frame) - _dragStart;
        // SetCurrentValue (not the CLR setter) so a two-way binding forwards Left/Top to the view model instead of being
        // masked by a Local write; OnPositionChanged then moves the popup. Clamp the top-left inside the parent window.
        var x = Math.Clamp(_dragBaseLeft + delta.X, 0, Math.Max(0, _dragParentW - _dragCardW));
        var y = Math.Clamp(_dragBaseTop + delta.Y, 0, Math.Max(0, _dragParentH - _dragCardH));
        SetCurrentValue(LeftProperty, x);
        SetCurrentValue(TopProperty, y);
    }

    private void OnDragRelease(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        e.Handled = true;
        if (_dragArea.IsMouseCaptured) _dragArea.ReleaseMouseCapture();
    }

    private void OnPinClick(object sender, RoutedEventArgs e) => IsPinned = !IsPinned;

    // Resize drags the bottom-right grip. Because the card is CENTRED by the popup, growing it by (dw,dh) moves both edges
    // out by half; shift the popup offset by half the applied change so the top-left corner stays put under the grip.
    private void OnResizePress(object sender, MouseButtonEventArgs e)
    {
        if (!CanResize || HostPopup?.PlacementTarget is not IInputComponent frame) return;
        _resizing = true;
        e.Handled = true;
        _resizeStart = e.GetPosition(frame);
        _resizeBaseW = double.IsNaN(Width) ? RenderSize.Width : Width;
        _resizeBaseH = double.IsNaN(Height) ? RenderSize.Height : Height;
        _resizeParentW = frame.RenderSize.Width;
        _resizeParentH = frame.RenderSize.Height;
        _resizeLeft = WorldTransform.TranslationVector.X;   // the card's current top-left in window space (stays fixed)
        _resizeTop = WorldTransform.TranslationVector.Y;
        _resizeGrip.CaptureMouse();
        _manager?.BringToFront(this);
    }

    private void OnResizeMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || HostPopup?.PlacementTarget is not IInputComponent frame) return;
        var delta = e.GetPosition(frame) - _resizeStart;

        // Grow from the fixed top-left; clamp the bottom-right so it can't leave the parent window (the grip stops at the
        // edge instead of the opposite side stretching), and not below the minimum.
        var maxW = Math.Max(MinCardWidth, _resizeParentW - _resizeLeft);
        var maxH = Math.Max(MinCardHeight, _resizeParentH - _resizeTop);
        var newW = Math.Clamp(_resizeBaseW + delta.X, MinCardWidth, maxW);
        var newH = Math.Clamp(_resizeBaseH + delta.Y, MinCardHeight, maxH);
        Width = newW;
        Height = newH;

        // Keep the top-left anchored: Center placement centres the card, so hold left = _resizeLeft by shifting the offset
        // back half the (parent - size) change; a manually-placed (Relative) card's offset already IS its top-left.
        if (HostPopup.Placement == PlacementMode.Center)
        {
            HostPopup.HorizontalOffset = _resizeLeft - (_resizeParentW - newW) / 2;
            HostPopup.VerticalOffset = _resizeTop - (_resizeParentH - newH) / 2;
        }
        else
        {
            HostPopup.HorizontalOffset = _resizeLeft;
            HostPopup.VerticalOffset = _resizeTop;
        }
    }

    private void OnResizeRelease(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        e.Handled = true;
        if (_resizeGrip.IsMouseCaptured) _resizeGrip.ReleaseMouseCapture();
    }
}
