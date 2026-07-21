namespace Adamantium.UI.Controls;

/// <summary>A data item that an items control should render as a divider (a <see cref="Separator"/>) rather than a normal
/// row. Lets a data-driven menu / toolbar / list carry separators in its view-model without exposing UI types: a node
/// implements this and returns true, and the control generates a Separator (which draws itself from the Separator style).</summary>
public interface ISeparatorItem
{
    bool IsSeparator { get; }
}
