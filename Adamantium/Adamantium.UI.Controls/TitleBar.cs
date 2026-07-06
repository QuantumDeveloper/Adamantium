using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
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
        typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ShowTitleProperty = AdamantiumProperty.Register(nameof(ShowTitle),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty TitleAlignmentProperty = AdamantiumProperty.Register(nameof(TitleAlignment),
        typeof(HorizontalAlignment), typeof(TitleBar), new PropertyMetadata(HorizontalAlignment.Left));

    public static readonly AdamantiumProperty ShowMinButtonProperty = AdamantiumProperty.Register(nameof(ShowMinButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ShowMaxRestoreButtonProperty = AdamantiumProperty.Register(nameof(ShowMaxRestoreButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(nameof(ShowCloseButton),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

    // Control has no FontSize (it's registered per-control here); the title text needs one, so declare it on the TitleBar.
    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(TitleBar), new PropertyMetadata(13.0, PropertyMetadataOptions.AffectsMeasure));

    // Reflects the owning window's maximized state (kept in sync via its StateChanged). The theme swaps the maximize
    // button's glyph (maximize <-> restore) off this via a trigger.
    public static readonly AdamantiumProperty IsWindowMaximizedProperty = AdamantiumProperty.Register(nameof(IsWindowMaximized),
        typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public bool IsWindowMaximized
    {
        get => GetValue<bool>(IsWindowMaximizedProperty);
        set => SetValue(IsWindowMaximizedProperty, value);
    }

    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
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
    private IUIComponent _buttonsPanel;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();

        _minButton = GetTemplateChild("PART_MinButton") as ButtonBase;
        _maxButton = GetTemplateChild("PART_MaxButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        _buttonsPanel = GetTemplateChild("PART_ButtonsPanel") as IUIComponent;

        // The caption DRAG + double-click maximize are handled natively by the OS (WM_NCHITTEST returns HTCAPTION over
        // the title area - see WindowBase.IsDraggableCaptionPoint), so the title bar only wires the command buttons.
        if (_minButton != null) _minButton.Click += OnMinClick;
        if (_maxButton != null) _maxButton.Click += OnMaxClick;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;
    }

    private void DetachParts()
    {
        if (_minButton != null) _minButton.Click -= OnMinClick;
        if (_maxButton != null) _maxButton.Click -= OnMaxClick;
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
    }

    // After arrange, publish the caption geometry to the owning window so the worker's WM_NCHITTEST can flag the drag
    // strip geometrically (no visual-tree walk from the OS thread). Assumes the title bar sits at the top of the window.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var window = OwnerWindow;
        if (window != null)
        {
            window.CaptionHeight = size.Height;
            window.CaptionRightInset = _buttonsPanel?.RenderSize.Width ?? 0;
        }
        return size;
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
