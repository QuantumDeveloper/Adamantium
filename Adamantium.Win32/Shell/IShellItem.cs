using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.Shell;

/// <summary>One thing in the shell's namespace - what a file dialog answers with. Not a path: the shell's namespace
/// holds items that have no path at all (a library, a device, a search result), so the path is something an item is
/// ASKED for and may not have.
///
/// <para>Only the members we call are declared; see <see cref="IFileSaveDialog"/> on why the order of the ones above
/// them still matters.</para></summary>
[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr instance);

    [PreserveSig]
    int GetParent(out IShellItem parent);

    /// <summary>A name of the requested kind - <see cref="ShellDialog.SigdnFileSysPath"/> for the full path on disk.
    /// The string is allocated by the shell and the CALLER frees it with CoTaskMemFree.</summary>
    [PreserveSig]
    int GetDisplayName(uint kind, out IntPtr name);
}
