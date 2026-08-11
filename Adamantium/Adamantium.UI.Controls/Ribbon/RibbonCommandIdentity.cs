namespace Adamantium.UI.Controls;

/// <summary>The identity a ribbon command is given when its author named none (<see cref="Ribbon.QuickAccessKeyProperty"/>).
/// Opaque on purpose: it says only "this command, not that one", which is all the quick-access bar needs to recognise
/// what it already holds. Lives as long as the control does.
/// <para>An application that wants to RECOGNISE its commands - to point an item at a state, to save the bar between
/// sessions - states a key of its own instead; this stands in only where there is nothing else to tell commands apart
/// by, and a command that cannot be told apart is one that is added twice and removed never.</para></summary>
public sealed class RibbonCommandIdentity
{
    public override string ToString() => $"command #{GetHashCode():x}";
}
