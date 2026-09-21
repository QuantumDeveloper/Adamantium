namespace Adamantium.UI.Core.Input;

/// <summary>How Tab treats the inside of a container.</summary>
public enum KeyboardNavigationMode
{
    /// <summary>Every focusable thing inside is its own stop - the plain case.</summary>
    Continue,

    /// <summary>The container is entered ONCE and left as a whole: Tab steps into it, and the next Tab goes to whatever
    /// follows the container rather than to its second child. What a list needs - the alternative is a Tab that walks
    /// sixty thousand rows before it reaches the button underneath. Moving BETWEEN the items is then the arrow keys'
    /// job, which is the panel's own answer and is not affected by this.</summary>
    Once,

    /// <summary>The move stops at the container's edge instead of continuing outside it. For the ARROW keys over a field
    /// of tiles: at the edge of the grid the key does nothing, rather than throwing the focus onto whatever happens to
    /// sit beside the panel. Leaving is then Tab's job, which is a deliberate move rather than one arrow too many.</summary>
    Contained,

    /// <summary>Tab never leaves: past the last stop it comes round to the first one INSIDE this container. What a modal
    /// dialog and an overlay need - while one is up, the rest of the window is not reachable by mouse either, so a Tab
    /// that walked out into it would put the keyboard somewhere the user cannot see or click, with no way back.</summary>
    Cycle
}
