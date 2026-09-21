namespace Adamantium.UI.Controls;

/// <summary>Which side of the title bar the window buttons live on.
/// <para>A PLATFORM convention, not a decoration: Windows keeps them right, macOS keeps them left and round, and on
/// Linux it depends on the desktop - Ubuntu moved them left following macOS, KDE keeps them right. An application that
/// puts them on the wrong side reads as foreign before anything else about it is noticed, so the theme states this the
/// same way it states any other metric.</para></summary>
public enum CaptionButtonPlacement
{
    /// <summary>Right of the title, closing outermost. Windows and most Linux desktops.</summary>
    Right,

    /// <summary>Left of the title, closing outermost - so the order reverses to close, minimise, maximise. macOS, and
    /// Ubuntu since Unity.</summary>
    Left
}
