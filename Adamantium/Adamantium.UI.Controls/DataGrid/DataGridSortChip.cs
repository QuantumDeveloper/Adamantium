using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>One key standing in the sorting strip. Its × takes the column out of the sort; carrying the chip sideways
/// changes which key decides and which only breaks ties; pressing it anywhere else turns that key around.
/// <para>None of the three is answered HERE: the strip owns them all, because a press only turns out to have been a
/// click once the button comes up somewhere near where it went down. See <see cref="DataGridSortPanel"/>.</para>
/// </summary>
public class DataGridSortChip : ContentControl
{
    private IUIComponent _remove;

    /// <summary>Which key this chip is, from 1: the one that decides is 1, the one that breaks its ties is 2. Shown by
    /// the theme, because the ORDER of the chips is what the priority is.</summary>
    public static readonly AdamantiumProperty LevelProperty = AdamantiumProperty.Register(nameof(Level),
        typeof(int), typeof(DataGridSortChip),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender));

    /// <summary>Which way this key runs. The theme draws the arrow - the chip says the fact.</summary>
    public static readonly AdamantiumProperty IsDescendingProperty = AdamantiumProperty.Register(nameof(IsDescending),
        typeof(bool), typeof(DataGridSortChip),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>This chip is being carried to another place in the order.</summary>
    public static readonly AdamantiumProperty IsDraggingProperty = AdamantiumProperty.Register(nameof(IsDragging),
        typeof(bool), typeof(DataGridSortChip),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public int Level
    {
        get => GetValue<int>(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool IsDescending
    {
        get => GetValue<bool>(IsDescendingProperty);
        set => SetValue(IsDescendingProperty, value);
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

    internal void Attach(DataGridSortDescription key, int level)
    {
        Column = key?.Column;
        Level = level;
        IsDescending = key?.Descending ?? false;
        Content = Column?.Header;
    }

    // Whether a press landed on the ×. The strip asks before it arms a carry or turns the key around: the × is drawn
    // as the way out of the sort, so it has to be the only thing that takes a column out.
    internal bool PressedRemove(object source)
    {
        for (var node = source as IUIComponent; node != null; node = node.VisualParent)
        {
            if (ReferenceEquals(node, _remove)) return true;
            if (ReferenceEquals(node, this)) return false;
        }

        return false;
    }
}
