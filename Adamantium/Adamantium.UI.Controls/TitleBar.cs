using System;
using System.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// The custom-chrome caption bar: an icon + title on the left, optional window commands, and the min / max-restore /
/// close buttons on the right. Built into the <see cref="Window"/> default ControlTemplate, but a standalone control so
/// it can be reused elsewhere (e.g. a virtual/designer window). It drives the OWNING window it is hosted in - it resolves
/// that window from the visual tree and calls DragMove / Minimize / ToggleMaximizeRestore / Close on it.
/// </summary>
public class TitleBar : Control
{
    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(string), typeof(TitleBar), new PropertyMetadata(string.Empty));

    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(TitleBar), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ShowIconProperty = AdamantiumProperty.Register(nameof(ShowIcon),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ShowTitleProperty = AdamantiumProperty.Register(nameof(ShowTitle),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty TitleAlignmentProperty = AdamantiumProperty.Register(nameof(TitleAlignment),
        typeof(HorizontalAlignment), typeof(TitleBar), new PropertyMetadata(HorizontalAlignment.Left));

    /// <summary>Content at the very start of the caption, before the window commands. Deliberately a neutral slot: the
    /// title bar shows what was put in it and knows nothing about what that is.</summary>
    public static readonly AdamantiumProperty LeadingContentProperty = AdamantiumProperty.Register(nameof(LeadingContent),
        typeof(object), typeof(TitleBar), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public object LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public static readonly AdamantiumProperty ShowMinButtonProperty = AdamantiumProperty.Register(nameof(ShowMinButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ShowMaxRestoreButtonProperty = AdamantiumProperty.Register(nameof(ShowMaxRestoreButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(nameof(ShowCloseButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    static TitleBar()
    {
        FontSizeProperty.OverrideMetadata(typeof(TitleBar),
            new PropertyMetadata(13.0, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure));
    }

    // Reflects the owning window's maximized state (kept in sync via its StateChanged). The theme swaps the maximize
    // button's glyph (maximize <-> restore) off this via a trigger.
    public static readonly AdamantiumProperty IsWindowMaximizedProperty = AdamantiumProperty.Register(nameof(IsWindowMaximized),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public bool IsWindowMaximized
    {
        get => GetValue<bool>(IsWindowMaximizedProperty);
        set => SetValue(IsWindowMaximizedProperty, value);
    }

    // MahApps-style caption command bars: a collection of WindowCommand items shown LEFT (after the title) and RIGHT
    // (before the min/max/close buttons). Bindable from a view-model; the Window forwards its own collections here.
    public static readonly AdamantiumProperty LeftWindowCommandsProperty = AdamantiumProperty.Register(nameof(LeftWindowCommands),
        typeof(IEnumerable), typeof(TitleBar), new PropertyMetadata(null));

    public static readonly AdamantiumProperty RightWindowCommandsProperty = AdamantiumProperty.Register(nameof(RightWindowCommands),
        typeof(IEnumerable), typeof(TitleBar), new PropertyMetadata(null, OnRightWindowCommandsChanged));

    // Drives the "..." overflow button's visibility: false (no right commands) hides it so an empty caption shows no
    // orphaned overflow button. Updated whenever RightWindowCommands is (re)assigned.
    public static readonly AdamantiumProperty HasRightWindowCommandsProperty = AdamantiumProperty.Register(
        nameof(HasRightWindowCommands), typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public IEnumerable LeftWindowCommands
    {
        get => GetValue<IEnumerable>(LeftWindowCommandsProperty);
        set => SetValue(LeftWindowCommandsProperty, value);
    }

    public IEnumerable RightWindowCommands
    {
        get => GetValue<IEnumerable>(RightWindowCommandsProperty);
        set => SetValue(RightWindowCommandsProperty, value);
    }

    public bool HasRightWindowCommands
    {
        get => GetValue<bool>(HasRightWindowCommandsProperty);
        private set => SetValue(HasRightWindowCommandsProperty, value);
    }

    private static void OnRightWindowCommandsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TitleBar tb) return;
        tb.HasRightWindowCommands = tb.RightWindowCommands is IEnumerable en && en.GetEnumerator().MoveNext();
    }

    /// <summary>The window title shown in the caption (usually TemplateBound to Window.Title).</summary>
    public string Title
    {
        get => GetValue<string>(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional icon/logo content shown at the left of the caption.</summary>
    public object Icon
    {
        get => GetValue<object>(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool ShowIcon
    {
        get => GetValue<bool>(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public bool ShowTitle
    {
        get => GetValue<bool>(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public HorizontalAlignment TitleAlignment
    {
        get => GetValue<HorizontalAlignment>(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }

    public bool ShowMinButton
    {
        get => GetValue<bool>(ShowMinButtonProperty);
        set => SetValue(ShowMinButtonProperty, value);
    }

    public bool ShowMaxRestoreButton
    {
        get => GetValue<bool>(ShowMaxRestoreButtonProperty);
        set => SetValue(ShowMaxRestoreButtonProperty, value);
    }

    public bool ShowCloseButton
    {
        get => GetValue<bool>(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    private ButtonBase _minButton;
    private ButtonBase _maxButton;
    private ButtonBase _closeButton;
    private ButtonBase _rightOverflowButton;
    private ContextMenu _rightOverflowMenu;
    private IUIComponent _dragArea;    // the ONE draggable region; its bounds become the window's CaptionDragRect

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();

        _minButton = GetTemplateChild("PART_MinButton") as ButtonBase;
        _maxButton = GetTemplateChild("PART_MaxButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        _rightOverflowButton = GetTemplateChild("PART_RightOverflowButton") as ButtonBase;
        _rightOverflowMenu = GetTemplateChild("PART_RightOverflowMenu") as ContextMenu;
        // Same as the ribbon's chevron: the button owns the close, so its own press must not light-dismiss first.
        if (_rightOverflowMenu != null) 
            _rightOverflowMenu.IgnoreTargetPress = true;
        _dragArea = GetTemplateChild("PART_DragArea") as IUIComponent;

        // Caption drag is MANAGED (not a geometric HTCAPTION), so it respects z-order: the drag only starts when the
        // drag area is genuinely the topmost element under the pointer. Any overlay floating over the title bar (a popup,
        // a SlidePanel, an overlay button) is hit first and keeps its click. A press hands off to the native move loop via
        // DragMove(); a double-click toggles maximize/restore (the native NCLBUTTONDOWN handoff never sees WM_NCLBUTTONDBLCLK).
        if (_dragArea is IInputComponent dragInput) dragInput.MouseLeftButtonDown += OnDragAreaMouseLeftButtonDown;
        if (_minButton != null) _minButton.Click += OnMinClick;
        if (_maxButton != null) _maxButton.Click += OnMaxClick;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;
        if (_rightOverflowButton != null) _rightOverflowButton.Click += OnRightOverflowClick;
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_dragArea is IInputComponent dragInput) dragInput.MouseLeftButtonDown -= OnDragAreaMouseLeftButtonDown;
        if (_minButton != null) _minButton.Click -= OnMinClick;
        if (_maxButton != null) _maxButton.Click -= OnMaxClick;
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        if (_rightOverflowButton != null) _rightOverflowButton.Click -= OnRightOverflowClick;
    }

    // A press on the drag strip: double-click toggles maximize/restore, a single press begins the native window move.
    // (e.Handled so the press doesn't also focus/interact with anything beneath the strip.)
    private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var window = OwnerWindow;
        if (window == null) return;
        e.Handled = true;
        if (e.ClickCount == 2)
            window.ToggleMaximizeRestore();
        else
            window.DragMove();
    }

    // "..." button: open the overflow menu (all right commands) against the button. Two-state - the second press puts the
    // list away - and what "open" means is asked of the MENU, which is the only thing that hears it close itself.
    private void OnRightOverflowClick(object sender, RoutedEventArgs e)
    {
        if (_rightOverflowMenu == null) return;

        if (_rightOverflowMenu.IsOpen)
        {
            _rightOverflowMenu.IsOpen = false;
            return;
        }

        _rightOverflowMenu.Open(_rightOverflowButton as UIComponent);
    }

    // After arrange, publish the drag-area element's bounds (in window-client coords) as the window's ONE draggable
    // caption region. The worker's WM_NCHITTEST checks a point against this rect only - a plain Rect, safe to read from
    // the OS message thread. Everything outside it (commands, buttons) stays HTCLIENT and clickable.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var window = OwnerWindow;
        if (window != null)
            window.CaptionDragRect = _dragArea != null ? RectRelativeTo(_dragArea, window) : default;
        return size;
    }

    // element's bounds expressed in ancestor-local (here: window-client) coordinates, via the world transforms.
    private static Rect RectRelativeTo(IUIComponent element, IUIComponent ancestor)
    {
        var m = element.WorldTransform * Matrix4x4F.Invert(ancestor.WorldTransform);
        var sz = element.RenderSize;
        var p0 = Vector3F.TransformCoordinate(new Vector3F(0f, 0f, 0f), m);
        var p1 = Vector3F.TransformCoordinate(new Vector3F((float)sz.Width, (float)sz.Height, 0f), m);
        return new Rect(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y), Math.Abs(p1.X - p0.X), Math.Abs(p1.Y - p0.Y));
    }

    private void OnMinClick(object sender, RoutedEventArgs e) => OwnerWindow?.Minimize();
    private void OnMaxClick(object sender, RoutedEventArgs e) => OwnerWindow?.ToggleMaximizeRestore();
    private void OnCloseClick(object sender, RoutedEventArgs e) => OwnerWindow?.Close();

    private WindowBase _window;

    // Track the owning window's state so the maximize button can toggle its glyph (maximize <-> restore). Subscribe once
    // the title bar is in the tree (the window is resolvable then); the state changes whether by our button, the caption
    // double-click, or the OS.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = OwnerWindow;
        if (_window != null)
        {
            _window.StateChanged += OnWindowStateChanged;
            IsWindowMaximized = _window.State == WindowState.Maximized;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window != null) { _window.StateChanged -= OnWindowStateChanged; _window = null; }
        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowStateChanged(object sender, StateChangedEventArgs e)
        => IsWindowMaximized = e.State == WindowState.Maximized;

    // The window this title bar is hosted in. Walk up the visual tree (robust before RootVisual is wired), falling back
    // to RootVisual. Null if the title bar isn't inside a WindowBase (e.g. dropped in a virtual window) - buttons no-op.
    private WindowBase OwnerWindow
    {
        get
        {
            for (IUIComponent node = this; node != null; node = node.VisualParent)
                if (node is WindowBase window) return window;
            return RootVisual as WindowBase;
        }
    }
}
