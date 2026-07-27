using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Win32.Ole;

/// <summary>The header of a <c>CF_HDROP</c> block: this struct, then the file paths back to back, each NUL-terminated,
/// the list closed by one more NUL. <see cref="pFiles"/> is the byte offset from the start of the block to that list.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct DROPFILES
{
    public uint pFiles;
    public NativePoint pt;
    [MarshalAs(UnmanagedType.Bool)] public bool fNC;
    [MarshalAs(UnmanagedType.Bool)] public bool fWide;
}
