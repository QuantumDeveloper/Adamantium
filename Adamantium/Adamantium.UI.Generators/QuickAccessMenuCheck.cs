using System.Collections.Generic;
using Adamantium.UI.Markup.Parsers;
using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Generators;

/// <summary>Warns about a drop-down command whose menu rows are written as CONTROLS while the command may still be
/// taken into the quick-access bar.
/// <para>The bar never gets the control itself - a <c>ContextMenu</c> is a logical child and a logical parent is one,
/// so lending it would take the menu away from the ribbon. What travels is the row DATA
/// (<c>RibbonQuickAccessEventArgs</c> reads <c>menu.ItemsSource</c> and nothing else), and a menu built from literal
/// children has none. The command then reaches the bar as an arrow that drops an empty list - with a clean build, a
/// working ribbon, and no sign of the fault until someone puts it in the bar and presses it.</para></summary>
internal static class QuickAccessMenuCheck
{
    private static readonly HashSet<string> DropDownCommands = new() { "RibbonDropDownButton", "RibbonSplitButton" };

    internal readonly struct Finding(string command, int line, int position)
    {
        public string Command { get; } = command;
        public int Line { get; } = line;
        public int Position { get; } = position;
    }

    public static List<Finding> Run(AumlDocument document)
    {
        var findings = new List<Finding>();
        Walk(document?.Root, findings);
        return findings;
    }

    private static void Walk(IAumlAstNode node, List<Finding> findings)
    {
        if (node is not AumlAstObjectNode obj) return;

        if (DropDownCommands.Contains(obj.TypeReference?.Name) && HasLiteralMenu(obj) && MayJoinTheBar(obj))
        {
            findings.Add(new Finding(obj.TypeReference.Name, obj.Line, obj.Position));
        }

        foreach (var child in obj.Children)
        {
            switch (child)
            {
                case AumlAstObjectNode nested:
                    Walk(nested, findings);
                    break;
                case AumlAstPropertyNode property:
                    foreach (var value in property.Values) Walk(value, findings);
                    break;
            }
        }
    }

    // A menu is "literal" when its ContextMenu carries element children of its own. ItemsSource is an attribute or a
    // property element holding a binding, never an object child, so the two cases cannot be confused.
    private static bool HasLiteralMenu(AumlAstObjectNode command)
    {
        foreach (var child in command.Children)
        {
            if (child is not AumlAstPropertyNode property) continue;
            if (NameOf(property) is not "DropDownMenu") continue;

            foreach (var value in property.Values)
            {
                if (value is AumlAstObjectNode menu && HasObjectChildren(menu)) return true;
            }
        }

        return false;
    }

    private static bool HasObjectChildren(AumlAstObjectNode menu)
    {
        foreach (var child in menu.Children)
        {
            if (child is AumlAstObjectNode) return true;
        }

        return false;
    }

    // Saying CanAddToQuickAccess="False" is the author stating this command is not for the bar - then a menu of
    // controls is a legitimate choice and there is nothing to warn about.
    private static bool MayJoinTheBar(AumlAstObjectNode command)
    {
        foreach (var child in command.Children)
        {
            if (child is not AumlAstPropertyNode property) continue;
            if (NameOf(property) is not ("CanAddToQuickAccess" or "Ribbon.CanAddToQuickAccess")) continue;

            foreach (var value in property.Values)
            {
                if (value is AumlAstTextNode text && text.Text?.Trim().ToLowerInvariant() == "false") return false;
            }
        }

        return true;
    }

    private static string NameOf(AumlAstPropertyNode property) =>
        property.Property is AumlAstPropertyReference reference ? reference.Name : null;
}
