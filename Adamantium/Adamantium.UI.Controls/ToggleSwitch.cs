using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// A modern on/off switch - a pill track with a thumb that slides between the off and on positions on click. It is a
/// strictly two-state <see cref="ToggleButton"/> (a switch is never indeterminate), so <see cref="ToggleButton.IsChecked"/>
/// (true = on) carries the state and the inherited Checked/Unchecked events fire on a flip. The track + sliding thumb
/// are entirely the theme template (a trigger on IsChecked moves the thumb); <see cref="Control"/> content is the label.
/// </summary>
public class ToggleSwitch : ToggleButton
{
    // A switch only ever flips on/off - it ignores IsThreeState (no indeterminate position on the track).
    protected override void OnToggle() => IsChecked = !IsChecked;

    // A switch can't stretch: its pill track + thumb + label are a fixed visual. Report the size it actually draws (not
    // the slot it was handed) so ActualWidth/RenderSize/ClipRectangle stop lying - see ContentControl.ArrangeContentSize.
    protected override Size ArrangeOverride(Size finalSize) => ArrangeContentSize(finalSize);
}
