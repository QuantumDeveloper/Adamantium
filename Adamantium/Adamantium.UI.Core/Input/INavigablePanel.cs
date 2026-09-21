namespace Adamantium.UI.Core.Input;

/// <summary>
/// A panel that can say who comes after one of its children in a given direction. Implemented by the panel because the
/// panel is the only one who knows its own layout: a stack knows its children stand in a line, a grid knows it has rows
/// and columns, a wrap panel knows where a visual line ends. A single tree-walking navigator would have to guess all of
/// that from coordinates.
/// </summary>
public interface INavigablePanel
{
    /// <param name="from">A DIRECT child of this panel - the one the focus is currently inside, which is not the
    /// focused element itself when the focus sits deep in a control's template.</param>
    /// <returns>Another direct child to move to, or NULL for "nothing further this way in here" - which is not a
    /// failure: the navigator then asks the panel above about THIS panel, so running off the end of a stack carries on
    /// into the next cell of the grid that holds it, with neither panel knowing about the other.</returns>
    IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction);
}
