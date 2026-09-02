using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// A selectable item container in a <see cref="ListBox"/>. Carries the selectable chrome (border / corner / padding) and
/// the hover/pressed/selected brushes the default template's triggers project, plus its <see cref="IsSelected"/> /
/// <see cref="IsPressed"/> visual state. Pressing it selects it in the owning ListBox. Like all item containers it is
/// recycled, so its selected state is driven BY the ListBox (set in PrepareContainer / on selection change), never
/// stored only here.
/// </summary>
public class ListBoxItem : ContentControl, ISelectable
{
    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(ListBoxItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsPressedProperty = AdamantiumProperty.Register(nameof(IsPressed),
        typeof(bool), typeof(ListBoxItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // State brushes the default template's triggers project onto the chrome (hover / pressed / selected). Exposing them
    // as properties lets ONE template serve every variant - the theme just sets these. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundPressedProperty = AdamantiumProperty.Register(
        nameof(BackgroundPressed), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundSelectedProperty = AdamantiumProperty.Register(
        nameof(ForegroundSelected), typeof(Brush), typeof(ListBoxItem), new PropertyMetadata(default(Brush)));

    static ListBoxItem()
    {
        // A list item has NO background by default - only the hover/pressed/selected states paint one (via the theme
        // triggers). So a rest item creates no fill render unit and no analytic-AA fringe: ~60 transparent-fill fringes
        // in a scrolled wrap list were exhausting GPU memory. Hit-testing is bounds-based, so the whole row stays
        // clickable without a background brush. (Control's default is Brushes.Transparent - a SolidColorBrush, which
        // would build a fringe.)
        BackgroundProperty.OverrideMetadata(typeof(ListBoxItem),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));
        // A list item is a keyboard-focus target (arrow-key navigation, selection) - opt in, since the base default is
        // now false. Metadata priority, so a {Binding}/Style/Trigger can still override it.
        FocusableProperty.OverrideMetadata(typeof(ListBoxItem), new PropertyMetadata(true));
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        private set => SetValue(IsPressedProperty, value);
    }

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public Brush BackgroundPressed
    {
        get => GetValue<Brush>(BackgroundPressedProperty);
        set => SetValue(BackgroundPressedProperty, value);
    }

    public Brush BackgroundSelected
    {
        get => GetValue<Brush>(BackgroundSelectedProperty);
        set => SetValue(BackgroundSelectedProperty, value);
    }

    public Brush ForegroundSelected
    {
        get => GetValue<Brush>(ForegroundSelectedProperty);
        set => SetValue(ForegroundSelectedProperty, value);
    }

    // Multi-select drag support: a plain click on an ALREADY-selected item defers the selection COLLAPSE until mouse-up, so
    // the whole selection survives long enough to be dragged. If the pointer moves first (a drag begins) the collapse is
    // cancelled and the selection is kept; a release without moving applies it (the ordinary "click picks just this one").
    private bool _deferSelect;
    private Vector2 _downPoint;

    private ListBox OwnerListBox => this.GetVisualAncestors().OfType<ListBox>().FirstOrDefault();

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled) return;
        e.Handled = true;
        Focus();
        IsPressed = true;

        // Reach the owner by walking up - works for both generated containers and a ListBoxItem authored directly in markup
        // (an "item is its own container"), without a back-reference to maintain. Use the modifiers carried BY THE EVENT
        // (captured on the OS message thread when it was raised) - input handlers run later on the loop thread, so re-reading
        // Keyboard.Modifiers there misses the Ctrl/Shift that was held.
        var owner = OwnerListBox;
        // Defer the selection change on a press over an ALREADY-selected item (multi-select) - a plain OR Ctrl press - so a
        // drag keeps the whole selection (Ctrl-drag = COPY the selection); a release without a drag applies it on mouse-up
        // (plain -> collapse to one, Ctrl -> toggle off). Shift stays immediate (it extends the range, not a drag gesture).
        var noShift = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) == 0;
        if (owner != null && noShift && IsSelected &&
            owner.SelectionMode is SelectionMode.Extended or SelectionMode.Multiple)
        {
            _deferSelect = true;
            _downPoint = e.GetPosition(owner);
            return;
        }
        owner?.SelectFromContainer(this, e.Modifiers);
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_deferSelect) return;
        var owner = OwnerListBox;
        // Past the OS drag threshold this is a DRAG, not a click - keep the selection intact for it to carry.
        if (owner != null && PlatformSettings.ExceedsDragThreshold(e.GetPosition(owner) - _downPoint, owner)) _deferSelect = false;
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        e.Handled = true;
        IsPressed = false;
        if (_deferSelect)   // released without dragging -> apply the deferred collapse now
        {
            _deferSelect = false;
            OwnerListBox?.SelectFromContainer(this, e.Modifiers);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsPressed = false;
    }
}
