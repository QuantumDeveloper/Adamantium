namespace Adamantium.UI.Controls;

/// <summary>
/// An item container whose selected state a <see cref="Selector"/> reflects the selection onto - e.g.
/// <see cref="ListBoxItem"/> or <see cref="TabItem"/>. Lets the base track selection by item without knowing the
/// concrete container type (and keeps it correct across container recycling).
/// </summary>
public interface ISelectable
{
    bool IsSelected { get; set; }
}
