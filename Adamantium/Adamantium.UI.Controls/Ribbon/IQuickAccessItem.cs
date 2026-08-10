using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>An item of a <see cref="RibbonQuickAccess"/> that draws itself in its own way - a slider, a drop-down,
/// anything the bar's default icon button cannot stand in for. The template comes from the ribbon command that asked to
/// be added (<see cref="Ribbon.QuickAccessTemplateProperty"/>) and travels as DATA, so the bar builds its own visual and
/// the command keeps standing in the ribbon at the same time.</summary>
public interface IQuickAccessItem
{
    DataTemplate QuickAccessTemplate { get; }
}
