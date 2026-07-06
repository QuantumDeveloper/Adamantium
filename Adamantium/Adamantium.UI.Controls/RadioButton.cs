using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A radio button: one of a mutually exclusive set. Clicking it checks it and clears the rest of its group; a click
/// never unchecks it (pick another, or clear it in code). Buttons with the same non-empty <see cref="GroupName"/> are
/// exclusive app-wide; unnamed ones are exclusive among siblings under the same visual parent. Behaviour only -
/// the circle + dot + label are the theme template.
/// </summary>
public class RadioButton : ToggleButton
{
    // Named groups: weak refs so a removed button can be collected; app-wide exclusivity matches WPF.
    private static readonly Dictionary<string, List<WeakReference<RadioButton>>> NamedGroups = [];

    public static readonly AdamantiumProperty GroupNameProperty = AdamantiumProperty.Register(nameof(GroupName),
        typeof(string), typeof(RadioButton), new PropertyMetadata(string.Empty, OnGroupNameChanged));

    /// <summary>Buttons sharing this (non-empty) name are mutually exclusive across the app. Empty - exclusive among
    /// the empty-group radios under the same visual parent.</summary>
    public string GroupName
    {
        get => GetValue<string>(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    // A click only ever checks a radio; it never toggles back off (selecting another in the group clears it instead).
    // SetCurrentValue (not the CLR setter) so it doesn't write a Local value that masks a TwoWay binding (see ToggleButton).
    protected override void OnToggle()
    {
        if (IsChecked != true) SetCurrentValue(IsCheckedProperty, true);
    }

    protected override void OnToggleStateChanged(bool? value)
    {
        if (value == true) ClearGroupSiblings();
    }

    private static void OnGroupNameChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RadioButton radio) return;
        radio.Unregister(e.OldValue as string);   // OldValue is the Unset sentinel on the first assignment, not a string
        radio.Register(e.NewValue as string);
    }

    private void Register(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        lock (NamedGroups)
        {
            if (!NamedGroups.TryGetValue(group, out var list)) NamedGroups[group] = list = [];
            list.RemoveAll(w => !w.TryGetTarget(out _));   // prune collected entries while we're here
            list.Add(new WeakReference<RadioButton>(this));
        }
    }

    private void Unregister(string group)
    {
        if (string.IsNullOrEmpty(group)) return;
        lock (NamedGroups)
        {
            if (NamedGroups.TryGetValue(group, out var list))
                list.RemoveAll(w => !w.TryGetTarget(out var r) || ReferenceEquals(r, this));
        }
    }

    private void ClearGroupSiblings()
    {
        foreach (var other in GroupSiblings())
            if (other.IsChecked == true) other.SetCurrentValue(IsCheckedProperty, false);
    }

    private IEnumerable<RadioButton> GroupSiblings()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            List<WeakReference<RadioButton>> list;
            lock (NamedGroups) { NamedGroups.TryGetValue(GroupName, out list); }
            if (list == null) yield break;
            foreach (var weak in list.ToArray())   // snapshot: clearing a sibling mutates IsChecked, not this list
                if (weak.TryGetTarget(out var radio) && !ReferenceEquals(radio, this)) yield return radio;
            yield break;
        }

        // Unnamed: the other empty-group radios directly under the same visual parent.
        if (VisualParent is { } parent)
            foreach (var child in parent.VisualChildren)
                if (child is RadioButton radio && !ReferenceEquals(radio, this) && string.IsNullOrEmpty(radio.GroupName))
                    yield return radio;
    }

    // A radio button can't stretch: its ring + label are a fixed visual. It reports the size it actually draws (not the
    // slot it was handed) so ActualWidth/RenderSize/ClipRectangle stop lying - see ContentControl.ArrangeContentSize.
    protected override Size ArrangeOverride(Size finalSize) => ArrangeContentSize(finalSize);
}
