namespace Adamantium.UI.Controls;

/// <summary>
/// Implemented by a window so a <see cref="Popup"/> can register itself with the window-wide <see cref="PopupLayer"/>
/// (the overlay stage that draws open popups on top of the content, within the window). Found by a popup walking up its
/// visual ancestors.
/// </summary>
public interface IPopupHost
{
    PopupLayer PopupLayer { get; }
}
