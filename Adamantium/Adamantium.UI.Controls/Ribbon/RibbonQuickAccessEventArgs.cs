using System.Collections;
using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A command asking to be put in the quick-access bar, or taken out of it. Carries a SLEEP of what the command
/// looks like rather than the command itself, so whoever answers can build its own kind of item out of it - the bar's
/// collection belongs to the application, and the ribbon never writes into it.
/// <para>The same object reaches the application twice over: as the argument of the routed event, and as the parameter
/// of <see cref="Ribbon.AddToQuickAccessCommandProperty"/>. A view written in AUML has no code-behind, so hearing the
/// event there means writing a behaviour - which works, but a bound command is the short way to a view model.</para></summary>
public class RibbonQuickAccessEventArgs : RoutedEventArgs
{
    public RibbonQuickAccessEventArgs(RoutedEvent routedEvent, IUIComponent command)
    {
        RoutedEvent = routedEvent;
        OriginalSource = command;
        Source = command;
        Command = command;

        Icon = Ribbon.GetIcon(command);
        Template = Ribbon.GetQuickAccessTemplate(command);
        ToolTip = command is AdamantiumComponent component ? ToolTipService.GetToolTip(component) : null;

        // Not everything in a group is a button - a slider, a drop-down and a label all sit in one, and asking them for
        // a Command they never declared is asking for a property that is not theirs.
        if (command is Primitives.ButtonBase button)
        {
            Action = button.Command;
            ActionParameter = button.CommandParameter;
        }

        // What a drop-down drops, taken as DATA. The menu itself is not handed over: a ContextMenu is a logical CHILD,
        // and a logical child has one parent - lending it to the bar would take it away from the ribbon.
        if (command is RibbonDropDownButton dropDown && dropDown.DropDownMenu is { } menu)
        {
            DropDownItems = menu.ItemsSource;
            DropDownItemTemplate = menu.ItemTemplate;
        }
    }

    /// <summary>The ribbon command that was asked about. Held so an application can read whatever else it needs off it -
    /// but it is NOT what should be stored: a control outlives nothing, and re-templating replaces it.</summary>
    public IUIComponent Command { get; }

    /// <summary>What marks the command - the small icon it draws in the bar.</summary>
    public object Icon { get; }

    /// <summary>The command's own COMPACT form (see <see cref="Ribbon.QuickAccessTemplateProperty"/>), or null to be
    /// drawn as an ordinary icon button. This is what a slider or a drop-down hands over instead of pretending to be a
    /// button - the bar builds a fresh visual from it, it is not the ribbon's control on loan.</summary>
    public DataTemplate Template { get; }

    public object ToolTip { get; }

    /// <summary>What the command DOES, and what a button in the bar has to run. Null when what asked was not a button.</summary>
    public ICommand Action { get; }

    /// <summary>The rows of a drop-down command's menu, and how one row is drawn. Null for anything that drops nothing.
    /// A menu authored as literal <c>MenuItem</c> children cannot travel - state it as <c>ItemsSource</c> to let the
    /// command keep its arrow in the bar.</summary>
    public IEnumerable DropDownItems { get; }

    public DataTemplate DropDownItemTemplate { get; }

    public object ActionParameter { get; }
}
