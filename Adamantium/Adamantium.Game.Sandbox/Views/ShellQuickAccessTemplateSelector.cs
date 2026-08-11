using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox.Views;

/// <summary>How THIS shell draws its own commands in the quick-access bar. The bar's items are the shell's own type, so
/// only the shell can switch on what they are - here, whether the command has a checked state.
/// <para>This is where a command's compact form is decided, and the reason it is here rather than in the engine: the
/// ribbon holds a VISUAL of a command, never the command itself, so asking it to carry state across to a second visual
/// would make it own what it only displays. The application already owns the command model; the selector reads it.</para></summary>
public class ShellQuickAccessTemplateSelector : RibbonQuickAccessTemplateSelector
{
    /// <summary>A command that is ON or OFF. Its template binds the ITEM's state, so the button in the caption and the
    /// one in the ribbon are two views of one value rather than two values kept in step.</summary>
    public DataTemplate Toggle { get; set; }

    /// <summary>Only the case the base does not know about. Everything else - a command's own compact form, the ordinary
    /// icon button the theme draws - stays the bar's answer.</summary>
    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
    {
        if (item is QuickAccessCommand command && command.IsChecked.HasValue) return Toggle;

        return base.SelectTemplate(item, container);
    }
}
