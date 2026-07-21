using System.Collections.ObjectModel;
using Adamantium.Core.Commands;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One node of a data-driven menu tree: a label (<see cref="Title"/>) + optional shortcut hint, an optional
/// <see cref="Command"/> (leaves only), a <see cref="IsSeparator"/> flag, and <see cref="Children"/> (a submenu). The menu
/// control turns this tree into MenuItems through a HierarchicalDataTemplate (and a Separator per <see cref="ISeparatorItem"/>),
/// so the whole menu comes from the view-model.</summary>
public class MenuNode : ISeparatorItem
{
    public string Title { get; init; }
    public string Gesture { get; init; }
    public bool IsSeparator { get; init; }
    public ICommand Command { get; init; }
    public ObservableCollection<MenuNode> Children { get; } = [];

    /// <summary>A leaf row that runs <paramref name="command"/> when clicked.</summary>
    public static MenuNode Leaf(string title, ICommand command, string gesture = null)
        => new() { Title = title, Gesture = gesture, Command = command };

    /// <summary>A parent row whose <paramref name="children"/> are its submenu.</summary>
    public static MenuNode Parent(string title, params MenuNode[] children)
    {
        var node = new MenuNode { Title = title };
        foreach (var child in children) node.Children.Add(child);
        return node;
    }

    /// <summary>A divider row.</summary>
    public static MenuNode Divider() => new() { IsSeparator = true };
}
