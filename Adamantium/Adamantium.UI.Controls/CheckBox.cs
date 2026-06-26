using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// A two- or three-state check box: a labelled box that toggles checked / unchecked (and, with
/// <see cref="ToggleButton.IsThreeState"/>, indeterminate) on click. All behaviour is inherited from
/// <see cref="ToggleButton"/>; the box, the check glyph and the label are purely the theme template.
/// </summary>
public class CheckBox : ToggleButton
{
    // A check box can't stretch: its box + glyph + label are a fixed visual. It reports the size it actually draws (not
    // the slot it was handed) so ActualWidth/RenderSize/ClipRectangle stop lying - see ContentControl.ArrangeContentSize.
    protected override Size ArrangeOverride(Size finalSize) => ArrangeContentSize(finalSize);
}
