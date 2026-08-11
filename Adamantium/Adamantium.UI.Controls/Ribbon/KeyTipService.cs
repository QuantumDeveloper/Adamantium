using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>Reaching a command by letters instead of by pointer, the way Office does it: Alt shows a badge on every
/// command that can be reached from here, typing its letters goes there, Escape steps back out one level.
/// <para>A SERVICE with attached properties rather than a base class, for the reason the sizes are: the things that wear
/// a key tip share no type - a tab header, a command, a bar item, the application menu.</para></summary>
public static class KeyTipService
{
    /// <summary>What to press to reach this element. Unset means the service works one out from the label.</summary>
    public static readonly AdamantiumProperty KeyTipProperty = AdamantiumProperty.RegisterAttached("KeyTip",
        typeof(string), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static string GetKeyTip(IAdamantiumComponent element) => element.GetValue<string>(KeyTipProperty);

    public static void SetKeyTip(IAdamantiumComponent element, string value) => element.SetValue(KeyTipProperty, value);

    /// <summary>Which edge the badge hangs off. Office does not put them all in one place - a tab header wears it below,
    /// a small command at its left.</summary>
    // Registered as "KeyTipPlacement", not "Placement": attached properties share one namespace per attaches-to type,
    // and ToolTipService got there first. The markup name is still KeyTipService.Placement - that resolves through the
    // Get/Set pair, not through this string.
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.RegisterAttached("KeyTipPlacement",
        typeof(KeyTipPlacement), typeof(AdamantiumComponent), new PropertyMetadata(KeyTipPlacement.Bottom));

    public static KeyTipPlacement GetPlacement(IAdamantiumComponent element) =>
        element.GetValue<KeyTipPlacement>(PlacementProperty);

    public static void SetPlacement(IAdamantiumComponent element, KeyTipPlacement value) =>
        element.SetValue(PlacementProperty, value);

    /// <summary>Marks an element as a LEVEL: pressing its key does not run it but descends into it, and its own subtree
    /// supplies the next set of badges. A ribbon tab is one; so is the application menu.</summary>
    public static readonly AdamantiumProperty IsScopeProperty = AdamantiumProperty.RegisterAttached("IsScope",
        typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    public static bool GetIsScope(IAdamantiumComponent element) => element.GetValue<bool>(IsScopeProperty);

    public static void SetIsScope(IAdamantiumComponent element, bool value) => element.SetValue(IsScopeProperty, value);

    /// <summary>Every element under <paramref name="scope"/> that wears a key tip and can be reached right now, WITHOUT
    /// descending into a nested scope: a tab's commands belong to that tab's level, not to the one above it.</summary>
    public static IReadOnlyList<IUIComponent> Candidates(IUIComponent scope)
    {
        var found = new List<IUIComponent>();
        if (scope == null) return found;

        foreach (var child in scope.VisualChildren)
        {
            Collect(child, found);
        }

        return found;
    }

    private static void Collect(IUIComponent node, List<IUIComponent> found)
    {
        if (node.Visibility != Visibility.Visible) return;

        if (Participates(node))
        {
            found.Add(node);
            // A scope's own subtree is the NEXT level - stop here, or a tab would hand the level above it every command
            // it holds and one letter would be claimed twice.
            if (GetIsScope(node)) return;
        }

        foreach (var child in node.VisualChildren)
        {
            Collect(child, found);
        }
    }

    /// <summary>Does this element take part in the level? Either it was GIVEN a key tip, or it is something a key tip
    /// can act on at all - Office tips every command in a tab, and making an author write out each letter would leave
    /// most of the band unreachable from the keyboard.</summary>
    private static bool Participates(IUIComponent node) =>
        !string.IsNullOrEmpty(GetKeyTip(node)) || node is IKeyTipTarget or Primitives.ButtonBase;

    /// <summary>Gives a key tip to every participant that has none, so the band is reachable without an author writing
    /// out a letter for each command. Same rule Office uses: the first letter of the label that is still free, then the
    /// rest of its letters, then a second letter appended - never a silent collision.</summary>
    public static void AutoAssign(IReadOnlyList<IUIComponent> participants)
    {
        var taken = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var participant in participants)
        {
            var stated = GetKeyTip(participant);
            if (!string.IsNullOrEmpty(stated)) taken.Add(stated);
        }

        foreach (var participant in participants)
        {
            if (!string.IsNullOrEmpty(GetKeyTip(participant))) continue;

            var assigned = FirstFree(LabelOf(participant), taken);
            taken.Add(assigned);
            SetKeyTip(participant, assigned);
        }
    }

    private static string LabelOf(IUIComponent element) =>
        (element as ContentControl)?.Content?.ToString() ?? element.GetType().Name;

    private static string FirstFree(string label, HashSet<string> taken)
    {
        foreach (var c in label)
        {
            if (!char.IsLetterOrDigit(c)) continue;

            var key = char.ToUpperInvariant(c).ToString();
            if (taken.Add(key))
            {
                taken.Remove(key);   // the caller records it - Add here only asked "is it free"
                return key;
            }
        }

        // Every letter of the label is spoken for: pair the first one with a digit, as Office pairs letters.
        var head = char.ToUpperInvariant(label.FirstOrDefault(char.IsLetterOrDigit));
        if (head == '\0') head = 'X';

        for (var i = 1; i < 100; i++)
        {
            var key = $"{head}{i}";
            if (!taken.Contains(key)) return key;
        }

        return head.ToString();
    }

    /// <summary>What <paramref name="typed"/> still leaves reachable. An exact hit is returned alone, so a key tip that
    /// is a PREFIX of another ("F" beside "FN") acts the moment it can.</summary>
    public static IReadOnlyList<IUIComponent> Narrow(IReadOnlyList<IUIComponent> candidates, string typed)
    {
        var exact = candidates.FirstOrDefault(
            x => string.Equals(GetKeyTip(x), typed, System.StringComparison.OrdinalIgnoreCase));

        if (exact != null) return [exact];

        return candidates
            .Where(x => GetKeyTip(x).StartsWith(typed, System.StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
