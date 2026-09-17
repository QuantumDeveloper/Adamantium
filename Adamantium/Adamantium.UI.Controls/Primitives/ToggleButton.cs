using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// Base for controls that hold a checked state toggled by a click - the classic press-to-stay button, plus
/// <see cref="CheckBox"/>, <see cref="RadioButton"/> and <see cref="ToggleSwitch"/>. <see cref="IsChecked"/> is
/// nullable so a control can also be indeterminate when <see cref="IsThreeState"/> is on. Mirrors WPF's ToggleButton:
/// the click cycles the state (false -&gt; true -&gt; [null -&gt;] false) and raises Checked/Unchecked/Indeterminate.
/// The visuals live entirely in the theme template (a trigger on IsChecked), never here.
/// </summary>
public class ToggleButton : ButtonBase
{
    public static readonly AdamantiumProperty IsCheckedProperty = AdamantiumProperty.Register(nameof(IsChecked),
        typeof(bool?), typeof(ToggleButton),
        new PropertyMetadata((bool?)false, PropertyMetadataOptions.AffectsRender, OnIsCheckedChanged));

    public static readonly AdamantiumProperty IsThreeStateProperty = AdamantiumProperty.Register(nameof(IsThreeState),
        typeof(bool), typeof(ToggleButton), new PropertyMetadata(false));

    // The CHECKED state's brushes, for the same reason ButtonBase carries the hover and pressed ones: one template has
    // to serve every toggle there is, and what a checked toggle looks like is not the same everywhere. A toggle sitting
    // on a colour of somebody else's choosing - the fold on a graph node, which sits on the node's own accent strip -
    // has to be able to say "not painted", and a theme that stated the accent in the trigger itself left it nothing to
    // say it with. Set by the theme; null means "no change in that state".
    public static readonly AdamantiumProperty BackgroundCheckedProperty = AdamantiumProperty.Register(
        nameof(BackgroundChecked), typeof(Brush), typeof(ToggleButton), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BorderBrushCheckedProperty = AdamantiumProperty.Register(
        nameof(BorderBrushChecked), typeof(Brush), typeof(ToggleButton), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty ForegroundCheckedProperty = AdamantiumProperty.Register(
        nameof(ForegroundChecked), typeof(Brush), typeof(ToggleButton), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundCheckedPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundCheckedPointerOver), typeof(Brush), typeof(ToggleButton), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundCheckedPressedProperty = AdamantiumProperty.Register(
        nameof(BackgroundCheckedPressed), typeof(Brush), typeof(ToggleButton), new PropertyMetadata(default(Brush)));

    public static readonly RoutedEvent CheckedEvent = EventManager.RegisterRoutedEvent(nameof(Checked),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));

    public static readonly RoutedEvent UncheckedEvent = EventManager.RegisterRoutedEvent(nameof(Unchecked),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));

    public static readonly RoutedEvent IndeterminateEvent = EventManager.RegisterRoutedEvent(nameof(Indeterminate),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));

    /// <summary>Checked (true), unchecked (false) or - only when <see cref="IsThreeState"/> - indeterminate (null).</summary>
    public bool? IsChecked
    {
        get => GetValue<bool?>(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>When true a click also cycles through the indeterminate (null) state; default false (two-state).</summary>
    public bool IsThreeState
    {
        get => GetValue<bool>(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    /// <summary>What fills it while checked. Transparent for a toggle that must not paint over what it is standing on.
    /// </summary>
    public Brush BackgroundChecked
    {
        get => GetValue<Brush>(BackgroundCheckedProperty);
        set => SetValue(BackgroundCheckedProperty, value);
    }

    /// <summary>Its edge while checked.</summary>
    public Brush BorderBrushChecked
    {
        get => GetValue<Brush>(BorderBrushCheckedProperty);
        set => SetValue(BorderBrushCheckedProperty, value);
    }

    /// <summary>What its content is drawn in while checked.</summary>
    public Brush ForegroundChecked
    {
        get => GetValue<Brush>(ForegroundCheckedProperty);
        set => SetValue(ForegroundCheckedProperty, value);
    }

    /// <summary>Checked and under the pointer. Its own brush because a checked toggle must not take the plain hover
    /// fill - that paints over the checked look and leaves a blank box.</summary>
    public Brush BackgroundCheckedPointerOver
    {
        get => GetValue<Brush>(BackgroundCheckedPointerOverProperty);
        set => SetValue(BackgroundCheckedPointerOverProperty, value);
    }

    /// <summary>Checked and held down.</summary>
    public Brush BackgroundCheckedPressed
    {
        get => GetValue<Brush>(BackgroundCheckedPressedProperty);
        set => SetValue(BackgroundCheckedPressedProperty, value);
    }

    /// <summary>Raised when <see cref="IsChecked"/> becomes true.</summary>
    public event RoutedEventHandler Checked
    {
        add => AddHandler(CheckedEvent, value);
        remove => RemoveHandler(CheckedEvent, value);
    }

    /// <summary>Raised when <see cref="IsChecked"/> becomes false.</summary>
    public event RoutedEventHandler Unchecked
    {
        add => AddHandler(UncheckedEvent, value);
        remove => RemoveHandler(UncheckedEvent, value);
    }

    /// <summary>Raised when <see cref="IsChecked"/> becomes null (indeterminate).</summary>
    public event RoutedEventHandler Indeterminate
    {
        add => AddHandler(IndeterminateEvent, value);
        remove => RemoveHandler(IndeterminateEvent, value);
    }

    // A click toggles before the base raises Click/Command, so a handler/command sees the new IsChecked.
    protected override void OnClick()
    {
        OnToggle();
        base.OnClick();
    }

    /// <summary>Advances the checked state on a click: false -&gt; true -&gt; (three-state ? null) -&gt; false.</summary>
    protected virtual void OnToggle()
    {
        // SetCurrentValue, NOT the CLR setter: a user click must NOT write a Local value, which (higher priority than
        // Binding) would mask a TwoWay binding and stop the toggle tracking its source afterwards - so an external change
        // to the bound value (e.g. a SlidePanel closing itself) would no longer flip the switch back. Mirrors Slider.
        SetCurrentValue(IsCheckedProperty, IsChecked switch
        {
            false => true,
            true => IsThreeState ? (bool?)null : false,
            _ => false
        });
    }

    private static void OnIsCheckedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ToggleButton toggle) return;
        toggle.OnToggleStateChanged((bool?)e.NewValue);
        toggle.RaiseCheckedEvent((bool?)e.NewValue);
    }

    /// <summary>Hook for subclasses that react to the new checked state (e.g. <see cref="RadioButton"/> clearing its
    /// group), called before the routed Checked/Unchecked/Indeterminate event fires.</summary>
    protected virtual void OnToggleStateChanged(bool? value)
    {
    }

    private void RaiseCheckedEvent(bool? value)
    {
        var routedEvent = value switch { true => CheckedEvent, false => UncheckedEvent, _ => IndeterminateEvent };
        RaiseEvent(new RoutedEventArgs(routedEvent, this) { RoutedEvent = routedEvent });
    }
}
