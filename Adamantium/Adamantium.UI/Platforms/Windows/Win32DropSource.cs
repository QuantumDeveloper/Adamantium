using Adamantium.Win32.Ole;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// The source side of a native drag: OLE asks it, on every mouse and key message of its modal loop, whether the gesture
/// continues - release the button to drop, Esc to cancel - and what feedback to show.
/// </summary>
internal sealed class Win32DropSource : IDropSource
{
    public int QueryContinueDrag(bool escapePressed, OleKeyState keyState)
    {
        if (escapePressed) return OleResult.DragCancel;
        // The gesture always starts on the left button (that is the only drag the engine begins), so its release is the drop.
        if ((keyState & OleKeyState.LeftButton) == 0) return OleResult.DragDrop;
        return OleResult.Ok;
    }

    // Let OLE draw the standard drag cursors for the effect the target reported - the native look users expect once the
    // gesture belongs to the OS (our own Effects->cursor mapping owns the in-app path).
    public int GiveFeedback(DropEffect effect) => OleResult.UseDefaultCursors;
}
