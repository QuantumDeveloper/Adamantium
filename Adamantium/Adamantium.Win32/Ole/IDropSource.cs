using System.Runtime.InteropServices;

namespace Adamantium.Win32.Ole;

/// <summary>
/// OLE <c>IDropSource</c> - the source-side half of a native drag, called by <c>DoDragDrop</c> on every mouse/key change
/// while the drag runs. <see cref="QueryContinueDrag"/> decides continue / drop / cancel;
/// <see cref="GiveFeedback"/> picks the cursor (returning <see cref="OleResult.UseDefaultCursors"/> lets OLE draw the
/// standard ones).
/// </summary>
[ComImport]
[Guid("00000121-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropSource
{
    [PreserveSig]
    int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, OleKeyState keyState);

    [PreserveSig]
    int GiveFeedback(DropEffect effect);
}
