using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// The drag-drop insertion cue: a templatable adorner showing WHERE a dragged item will land. It has no look of its own -
/// the theme's <c>ControlTemplate</c> supplies it (a capsule line with end dots by default), so an app restyles the drop
/// cue purely in the theme, the same way it restyles a ProgressBar. The drag engine creates one, applies the theme, sizes
/// it to the insertion span, and puts it in the target window's adorner layer. <see cref="Orientation"/> follows the target
/// list's layout: a vertically-stacked list gets a HORIZONTAL caret between rows, a horizontally-flowing one (a WrapPanel)
/// a VERTICAL caret between columns - the theme template keys its shape off it.
/// </summary>
public class DropInsertionIndicator : Adorner
{
    /// <summary>The bar's orientation: <c>Horizontal</c> (a line between stacked rows) or <c>Vertical</c> (a line between
    /// side-by-side items). The engine sets it from the target list's item-flow direction; the theme template swaps the
    /// end-cap layout off it.</summary>
    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(DropInsertionIndicator), new PropertyMetadata(Orientation.Horizontal));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Frame mode: draw a BORDER around the target instead of a caret line - the "drop INTO this node" cue of the
    /// hybrid tree-drop (a caret means before/after a sibling; a frame means as a child). The theme template keys off it.</summary>
    public static readonly AdamantiumProperty IsFrameProperty = AdamantiumProperty.Register(nameof(IsFrame),
        typeof(bool), typeof(DropInsertionIndicator), new PropertyMetadata(false));

    public bool IsFrame
    {
        get => GetValue<bool>(IsFrameProperty);
        set => SetValue(IsFrameProperty, value);
    }
}
