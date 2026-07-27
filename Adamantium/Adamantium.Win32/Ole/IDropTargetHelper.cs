using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.Mathematics;

namespace Adamantium.Win32.Ole;

/// <summary>
/// The shell's drag-image helper, target side (<c>CLSID_DragDropHelper</c>). Mirroring our <see cref="IDropTarget"/>
/// calls into it is what keeps the SOURCE's drag image (Explorer's file thumbnail with its "+ Copy to ..." label)
/// painting and updating while it flies over our window.
/// </summary>
[ComImport]
[Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropTargetHelper
{
    [PreserveSig]
    int DragEnter(IntPtr hwndTarget, [MarshalAs(UnmanagedType.Interface)] IDataObject data, ref NativePoint point, DropEffect effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int DragOver(ref NativePoint point, DropEffect effect);

    [PreserveSig]
    int Drop([MarshalAs(UnmanagedType.Interface)] IDataObject data, ref NativePoint point, DropEffect effect);

    [PreserveSig]
    int Show([MarshalAs(UnmanagedType.Bool)] bool show);
}
