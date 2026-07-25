using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32;

/// <summary>Win32 SIZE (cx, cy).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct NativeSize
{
    public Int32 Width;
    public Int32 Height;

    public NativeSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
