using System.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>What THIS shell puts in its quick-access bar. A caption command plus what the bar adds: a command that is
/// not a button carries its own compact form, and a drop-down carries the rows it drops. The engine states only the
/// interface - what the item is made of is the application's to decide.</summary>
public class QuickAccessCommand : WindowCommand, IQuickAccessItem
{
    public DataTemplate QuickAccessTemplate { get; set; }

    public IEnumerable DropDownItems { get; set; }

    public DataTemplate DropDownItemTemplate { get; set; }
}
