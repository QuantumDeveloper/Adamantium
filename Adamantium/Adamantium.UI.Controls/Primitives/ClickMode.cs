namespace Adamantium.UI.Controls.Primitives;

/// <summary>Specifies when the <see cref="ButtonBase.Click"/> event fires relative to the mouse gesture.</summary>
public enum ClickMode
{
    /// <summary>Click fires when the mouse is released over the button (default).</summary>
    Release,

    /// <summary>Click fires as soon as the mouse is pressed.</summary>
    Press,

    /// <summary>Click fires when the mouse pointer enters the button.</summary>
    Hover
}
