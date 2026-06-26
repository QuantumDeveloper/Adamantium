using Adamantium.UI.Core;
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
        IsChecked = IsChecked switch
        {
            false => true,
            true => IsThreeState ? null : false,
            _ => false
        };
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
