using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.Shell;

/// <summary>
/// The shell's Save-As dialog (<c>CLSID_FileSaveDialog</c>, Windows Vista and later).
///
/// <para>Used instead of <c>GetSaveFileName</c> from comdlg32, which predates this one by fifteen years: it draws the
/// old Windows XP chrome, has no places bar the user recognises, and takes its filters as a packed string of
/// NUL-separated pairs.</para>
///
/// <para>The vtable is FLATTENED here - <c>IModalWindow</c>'s one method, then <c>IFileDialog</c>'s - because that is
/// the layout a save dialog actually has, and declaring the bases separately buys nothing when nothing else implements
/// them. Only the members we call are declared, but the ORDER of every method up to them is part of the vtable and
/// cannot be shortened, hence the unused slots kept as named placeholders. The slots AFTER the last one we call
/// (<c>IFileSaveDialog</c>'s own properties) are simply absent, which is safe: they are at the end.</para>
/// </summary>
[ComImport]
[Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileSaveDialog
{
    /// <summary>Runs the dialog modally against an owner window, and does not return until it closes. A cancelled
    /// dialog is not an error but it is not S_OK either: it answers HRESULT_FROM_WIN32(ERROR_CANCELLED).</summary>
    [PreserveSig]
    int Show(IntPtr owner);

    /// <summary>The kinds of file on offer. Must be set BEFORE the dialog is shown - afterwards it is refused.</summary>
    [PreserveSig]
    int SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] types);

    [PreserveSig]
    int SetFileTypeIndex(uint index);

    [PreserveSig]
    int GetFileTypeIndex(out uint index);

    [PreserveSig]
    int Advise(IntPtr events, out uint cookie);

    [PreserveSig]
    int Unadvise(uint cookie);

    /// <summary>Flags, as a whole - so a caller reads them first and adds to them rather than replacing the defaults
    /// the dialog already carries.</summary>
    [PreserveSig]
    int SetOptions(uint options);

    [PreserveSig]
    int GetOptions(out uint options);

    [PreserveSig]
    int SetDefaultFolder(IShellItem folder);

    [PreserveSig]
    int SetFolder(IShellItem folder);

    [PreserveSig]
    int GetFolder(out IShellItem folder);

    [PreserveSig]
    int GetCurrentSelection(out IShellItem item);

    /// <summary>The name the dialog opens with, extension included.</summary>
    [PreserveSig]
    int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

    [PreserveSig]
    int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

    [PreserveSig]
    int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

    [PreserveSig]
    int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

    [PreserveSig]
    int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

    /// <summary>What the user chose. Valid only after <see cref="Show"/> returned S_OK.</summary>
    [PreserveSig]
    int GetResult(out IShellItem item);

    [PreserveSig]
    int AddPlace(IShellItem place, int order);

    /// <summary>Appended when the user types a name with no extension. Without the dot.</summary>
    [PreserveSig]
    int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
}
