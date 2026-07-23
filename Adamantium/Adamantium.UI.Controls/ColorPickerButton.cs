using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A swatch button that opens a full <see cref="ColorPicker"/> in a FLOATING, light-dismissing popup, so the
/// editor takes no layout space when closed and never pushes siblings. <see cref="SelectedColor"/> is two-way.
///
/// Fully templated (ColorPickerButtonStyleSet): PART_Header = the swatch (bind its fill to <see cref="SelectedBrush"/>),
/// PART_Popup = the flyout hosting a ColorPicker whose SelectedColor two-way {Binding}s to ours - we set the popup's
/// DataContext to this control (Popup.Open propagates it to the child), because {Ancestor} can't reach us from popup
/// content: it walks the visual tree, which is detached onto the overlay, and template parts aren't logical children either.
/// Dragging inside the popup relies on the overlay-aware mouse GetPosition (a detached overlay child falls back to the
/// window the pointer was measured against).</summary>
public class ColorPickerButton : Control
{
    public static readonly AdamantiumProperty SelectedColorProperty = AdamantiumProperty.Register(nameof(SelectedColor),
        typeof(Color), typeof(ColorPickerButton),
        new PropertyMetadata(new Color(255, 255, 255, 255), PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly AdamantiumProperty SelectedBrushProperty = AdamantiumProperty.Register(nameof(SelectedBrush),
        typeof(Brush), typeof(ColorPickerButton), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(ColorPickerButton), new PropertyMetadata(false, OnIsOpenChanged));

    private readonly SolidColorBrush _swatchBrush = new(Colors.White);   // field init runs BEFORE the base ctor's callbacks
    private Popup _popup;
    private UIComponent _header;
    private IPopupHost _host;
    private readonly MouseButtonEventHandler _lightDismiss;

    public ColorPickerButton()
    {
        _lightDismiss = OnGlobalPreviewDown;
        _swatchBrush.Color = SelectedColor;
        SelectedBrush = _swatchBrush;
    }

    /// <summary>The chosen colour. Two-way: bind it to your model; the swatch and the inner picker track it and it tracks them.</summary>
    public Color SelectedColor
    {
        get => GetValue<Color>(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>The swatch fill, kept in step with <see cref="SelectedColor"/>. TemplateBind the swatch's Background to it.</summary>
    public Brush SelectedBrush
    {
        get => GetValue<Brush>(SelectedBrushProperty);
        set => SetValue(SelectedBrushProperty, value);
    }

    /// <summary>Whether the colour flyout is open. Toggled by clicking the swatch; closed on an outside click.</summary>
    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _header = GetTemplateChild("PART_Header") as UIComponent;
        if (_popup != null)
        {
            // The flyout's ColorPicker binds SelectedColor with a plain {Binding} against US: give the popup our DataContext
            // so its content resolves to us (Popup.Open propagates DataContext to the child). {Ancestor} can't be used from
            // popup content - it walks the visual tree, which is detached onto the overlay, and template parts aren't logical
            // children either, so neither Ancestor mode reaches this control.
            _popup.DataContext = this;
            _popup.PlacementTarget = _header ?? this;   // anchor the flyout under the swatch
            _popup.IsOpen = IsOpen;
        }
    }

    // Clicking the swatch toggles the flyout. The picker lives on the overlay, so its own drags never reach here.
    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled) return;
        e.Handled = true;
        IsOpen = !IsOpen;
    }

    private static void OnSelectedColorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ColorPickerButton b) b._swatchBrush.Color = (Color)e.NewValue;
    }

    private static void OnIsOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var b = (ColorPickerButton)a;
        if (b._popup != null) b._popup.IsOpen = (bool)e.NewValue;   // _popup is null until OnApplyTemplate
        if ((bool)e.NewValue) b.HookLightDismiss(); else b.UnhookLightDismiss();
    }

    // Light dismiss (mirrors DropDown): while open, a preview mouse-down anywhere that is NOT inside this button or the
    // popup closes the flyout. Preview tunnels from the window root, so we catch every press.
    private void HookLightDismiss()
    {
        _host ??= this.GetVisualAncestors().OfType<IPopupHost>().FirstOrDefault();
        if (_host is IInputComponent root)
            root.AddHandler(Mouse.PreviewMouseDownEvent, _lightDismiss, handledEventsToo: true);
    }

    private void UnhookLightDismiss()
    {
        if (_host is IInputComponent root)
            root.RemoveHandler(Mouse.PreviewMouseDownEvent, _lightDismiss);
    }

    private void OnGlobalPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is IUIComponent src &&
            (IsWithin(src, this) || (_popup?.Child is IUIComponent child && IsWithin(src, child))))
            return;
        IsOpen = false;
    }

    private static bool IsWithin(IUIComponent node, IUIComponent ancestor)
    {
        if (ancestor == null) return false;
        for (var n = node; n != null; n = n.VisualParent)
            if (ReferenceEquals(n, ancestor)) return true;
        return false;
    }
}
