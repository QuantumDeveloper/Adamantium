using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.Shell;

/// <summary>One item in the shell namespace, as a file dialog returns it; it may have no file path. Only used members are
/// declared.</summary>
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
