namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// The Track's invisible page-scroll buttons. A distinct type purely for styling: theme selectors match by EXACT type,
/// so the default <c>RepeatButton</c> chrome (Selector="RepeatButton") does NOT apply to these - the trough's page
/// areas either side of the thumb stay invisible (no template) yet remain hit-testable by their bounds.
/// </summary>
public sealed class ScrollBarPageButton : RepeatButton
{
}
