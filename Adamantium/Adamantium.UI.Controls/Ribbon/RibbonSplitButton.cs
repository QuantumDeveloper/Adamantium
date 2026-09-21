using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>A command split in two: pressing the body runs <see cref="Primitives.ButtonBase.Command"/>, pressing the
/// arrow drops the menu. Paste-and-paste-special, undo-and-undo-history - the common case is one click away and the
/// rarer ones stay reachable.
/// <para>Which half was pressed is a fact about the POINTER, so it is settled on the press, before the click that
/// follows can ask. The keyboard has no halves: Space and Enter run the action.</para></summary>
public class RibbonSplitButton : RibbonDropDownButton
{
    /// <summary>Whether the pointer is over the ARROW half. A split button that highlights as one piece cannot say
    /// which of its two things a click is about to do, so the theme lights the halves separately.</summary>
    public static readonly AdamantiumProperty IsPointerOverDropDownProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsPointerOverDropDown),
            typeof(bool), typeof(RibbonSplitButton), new PropertyMetadata(false));

    private InputUIComponent _dropDownArea;
    private bool _pressedDropDown;

    public bool IsPointerOverDropDown
    {
        get => GetValue<bool>(IsPointerOverDropDownProperty);
        private set => SetValue(IsPointerOverDropDownProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _dropDownArea = GetTemplateChild("PART_DropDownArea") as InputUIComponent;
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        _dropDownArea = null;
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedDropDown = IsOverDropDownArea(e);
        base.OnMouseLeftButtonDown(sender, e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _pressedDropDown = false;
        base.OnKeyDown(e);
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        IsPointerOverDropDown = IsOverDropDownArea(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsPointerOverDropDown = false;
    }

    // Only the arrow half is a toggle. Reached from the base click for a press on the body, where it must do nothing.
    protected override void OnToggle()
    {
        if (_pressedDropDown) base.OnToggle();
    }

    protected override void OnClick()
    {
        if (_pressedDropDown)
        {
            base.OnToggle();
            return;
        }

        base.OnClick();
    }

    private bool IsOverDropDownArea(MouseEventArgs e)
    {
        if (_dropDownArea == null) return false;

        var point = e.GetPosition(_dropDownArea);
        return point.X >= 0 && point.X < _dropDownArea.ActualWidth
            && point.Y >= 0 && point.Y < _dropDownArea.ActualHeight;
    }
}
