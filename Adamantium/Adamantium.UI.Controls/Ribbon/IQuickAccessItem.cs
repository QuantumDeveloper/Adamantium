using Adamantium.Core.Commands;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>An item of a <see cref="RibbonQuickAccess"/> that draws itself in its own way - a slider, a drop-down,
/// anything the bar's default icon button cannot stand in for. The template comes from the ribbon command that asked to
/// be added (<see cref="Ribbon.QuickAccessTemplateProperty"/>) and travels as DATA, so the bar builds its own visual and
/// the command keeps standing in the ribbon at the same time.</summary>
public interface IQuickAccessItem
{
    DataTemplate QuickAccessTemplate { get; }

    /// <summary>What the application calls the command this item stands for (<see cref="Ribbon.QuickAccessKeyProperty"/>).
    /// The bar stamps it on the visual it builds, so a request to take the item back out names the same command the
    /// request to put it in did - which is the only identity an item that is not a command has.</summary>
    object Key { get; }

    /// <summary>What the item RUNS, when it runs anything. The ordinary way a command in the ribbon is recognised in the
    /// bar: it is the same <see cref="ICommand"/>. A command that runs nothing - one that only carries a state - has to
    /// be named by <see cref="Key"/> instead.</summary>
    ICommand Action { get; }
}
