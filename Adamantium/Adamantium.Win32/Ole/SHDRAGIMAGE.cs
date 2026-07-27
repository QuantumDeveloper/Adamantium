using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Win32.Ole;

/// <summary>The drag image handed to <see cref="IDragSourceHelper.InitializeFromBitmap"/>: a 32-bit bitmap, the cursor's
/// offset INSIDE it, and the color treated as transparent (unused for a per-pixel-alpha bitmap).
/// The shell takes ownership of <see cref="hbmpDragImage"/> - do not delete it after a successful call.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SHDRAGIMAGE
{
    public NativeSize sizeDragImage;
    public NativePoint ptOffset;
    public IntPtr hbmpDragImage;
    public uint crColorKey;
}
