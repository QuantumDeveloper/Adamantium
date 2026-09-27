using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.Mathematics;

namespace Adamantium.Win32.Ole;

/// <summary>
/// The shell's drag-image helper, source side (<c>CLSID_DragDropHelper</c>). Giving it a bitmap makes the OS carry OUR
/// ghost through the whole native drag - over other applications too - instead of a bare cursor.
/// <para>NB: it stores its private formats INTO the data object, so the data object must accept
/// <c>IDataObject::SetData</c> for arbitrary formats.</para>
/// </summary>
[ComImport]
[Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDragSourceHelper
{
    [PreserveSig]
    int InitializeFromBitmap(ref SHDRAGIMAGE image, [MarshalAs(UnmanagedType.Interface)] IDataObject data);

    [PreserveSig]
    int InitializeFromWindow(IntPtr hwnd, ref NativePoint point, [MarshalAs(UnmanagedType.Interface)] IDataObject data);
}
