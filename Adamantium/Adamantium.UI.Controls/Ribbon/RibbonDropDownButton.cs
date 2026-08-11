using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A command that opens a menu instead of doing something: Paste with its paste-special rows, a shape tool with
/// its shapes. The flyout is a <see cref="ContextMenu"/> - the same one a right-click uses - so light dismiss, Escape,
/// submenus and <c>ItemsSource</c> are already answered.
/// <para><see cref="Primitives.ToggleButton.IsChecked"/> IS the open state; there is no second property saying the same
/// thing, and the theme's checked look is what marks the command while its menu is down.</para></summary>
public class RibbonDropDownButton : RibbonToggleButton, IKeyTipTarget
{
    /// <summary>Reached by its key tip: drop the menu AND step the keyboard into it. A command opened from the keyboard
    /// has to stay reachable from it - the rows are drawn in the overlay, so a plain click-equivalent leaves the
    /// keyboard standing on the button with a list open in front of it and no way in.</summary>
    public void PressKeyTip()
    {
        IsChecked = true;
        DropDownMenu?.MoveKeyboardInsideWhenReady();
    }

    public static readonly AdamantiumProperty DropDownMenuProperty = AdamantiumProperty.Register(nameof(DropDownMenu),
        typeof(ContextMenu), typeof(RibbonDropDownButton), new PropertyMetadata(null, OnDropDownMenuChanged));

    /// <summary>The menu this command drops. Held as a logical child, so it is themed and inherits DataContext - the
    /// same arrangement <see cref="Base.InputUIComponent.ContextMenu"/> uses.</summary>
    public ContextMenu DropDownMenu
    {
        get => GetValue<ContextMenu>(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    private static void OnDropDownMenuChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RibbonDropDownButton button) return;

        if (e.OldValue is ContextMenu old)
        {
            old.PropertyChanged -= button.OnMenuPropertyChanged;
            button.RemoveLogicalChild(old);
        }

        if (e.NewValue is ContextMenu menu)
        {
            button.AddLogicalChild(menu);
            // The BUTTON owns the close (IsChecked drives the flyout), so its own press must not light-dismiss first -
            // the dismiss would close, the click would re-open, and the second press would look like it did nothing.
            menu.IgnoreTargetPress = true;
            menu.PropertyChanged += button.OnMenuPropertyChanged;
        }
    }

    // The menu closed itself (an outside press, Escape, or a row picked). Nothing else hears that, so the command would
    // stay lit over a flyout that is gone.
    private void OnMenuPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != ContextMenu.IsOpenProperty) return;
        if (!Equals(e.NewValue, false)) return;

        SetCurrentValue(IsCheckedProperty, false);
    }

    protected override void OnToggleStateChanged(bool? value)
    {
        base.OnToggleStateChanged(value);

        var menu = DropDownMenu;
        if (menu == null) return;

        if (value == true) menu.Open(this);
        else menu.IsOpen = false;
    }
}
