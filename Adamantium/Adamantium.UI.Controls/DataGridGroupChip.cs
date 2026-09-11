using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>One column standing in the grouping panel. Its × takes the column back out of the grouping; carrying the
/// chip sideways changes how deep it groups, and pressing it anywhere else does nothing at all.
/// <para>Neither gesture is answered HERE: the strip owns both, because a press only turns out to have been a click
/// once the button comes up somewhere near where it went down. See <see cref="DataGridGroupPanel"/>.</para></summary>
public class DataGridGroupChip : ContentControl
{
    private IUIComponent _remove;

    /// <summary>Which level this chip stands for, from 1: the outermost grouping is 1, the one inside it 2. Shown by
    /// the theme, because the ORDER of the chips is what the order of the groups is.</summary>
    public static readonly AdamantiumProperty LevelProperty = AdamantiumProperty.Register(nameof(Level),
        typeof(int), typeof(DataGridGroupChip),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender));

    /// <summary>This chip is being carried to another place in the order. The theme says so on the chip itself, or the
    /// gesture is only answered where the chip would LAND and not where it was picked up.</summary>
    public static readonly AdamantiumProperty IsDraggingProperty = AdamantiumProperty.Register(nameof(IsDragging),
        typeof(bool), typeof(DataGridGroupChip),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public int Level
    {
        get => GetValue<int>(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>The column this chip stands for.</summary>
    internal DataGridColumn Column { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _remove = GetTemplateChild("PART_Remove") as IUIComponent;
    }

    internal void Attach(DataGridColumn column, int level)
    {
        Column = column;
        Level = level;
        Content = column?.Header;
    }

    // Whether a press landed on the ×. The strip asks before it arms a carry: the × is drawn as the way out of the
    // grouping, so it has to be the only thing that takes a column out - a chip that ungrouped wherever it was pressed
    // made the × decoration and surprised anyone who meant to move it.
    internal bool PressedRemove(object source)
    {
        for (var node = source as IUIComponent; node != null; node = node.VisualParent)
        {
            if (ReferenceEquals(node, _remove)) return true;
        }

        return false;
    }
}
