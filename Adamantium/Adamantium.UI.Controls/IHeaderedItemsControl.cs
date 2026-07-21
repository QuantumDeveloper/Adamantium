using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>An <see cref="ItemsControl"/> that also carries a header (label) - the container shape a
/// <see cref="HierarchicalDataTemplate"/> drives: the template renders the header and supplies the children. Implemented
/// by <see cref="Primitives.MenuItem"/> (and, later, TreeViewItem).</summary>
public interface IHeaderedItemsControl
{
    object Header { get; set; }

    /// <summary>Template that renders <see cref="Header"/>. A hierarchical parent sets this to the HierarchicalDataTemplate
    /// so the header of every generated container is drawn the same way.</summary>
    DataTemplate HeaderTemplate { get; set; }
}
