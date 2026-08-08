namespace Adamantium.UI.Controls;

/// <summary>The quick-access bar: DOCUMENT commands kept within one click, shown in the caption through
/// <see cref="TitleBar.LeadingContent"/>. Its own control and its own list - the user reorders these, while the
/// window's commands belong to the application, and the reorder gesture must not reach across. It holds no reference
/// to a <see cref="Ribbon"/>: the two meet at a collection in the shell's view model. See docs/RIBBON_PLAN.md §7.1.</summary>
public class RibbonQuickAccess : ItemsControl
{
}
