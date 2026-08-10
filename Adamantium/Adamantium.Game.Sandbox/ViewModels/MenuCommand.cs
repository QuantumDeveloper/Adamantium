using Adamantium.Core.Commands;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One row of a drop-down command's menu, as DATA. A command that can be put in the quick-access bar has to
/// describe its menu this way: the bar draws its own copy, and a <c>MenuItem</c> control can only be in one place.</summary>
public class MenuCommand
{
    public string Header { get; set; }

    public ICommand Command { get; set; }
}
