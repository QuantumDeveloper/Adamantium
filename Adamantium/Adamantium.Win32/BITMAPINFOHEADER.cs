using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32;

/// <summary>Win32 BITMAPINFOHEADER. For a top-down 32-bit BGRA DIB: biBitCount=32, biCompression=BI_RGB(0),
/// biHeight NEGATIVE (top-down), biPlanes=1.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFOHEADER
{
    public UInt32 biSize;
    public Int32 biWidth;
    public Int32 biHeight;
    public UInt16 biPlanes;
    public UInt16 biBitCount;
    public UInt32 biCompression;
    public UInt32 biSizeImage;
    public Int32 biXPelsPerMeter;
    public Int32 biYPelsPerMeter;
    public UInt32 biClrUsed;
    public UInt32 biClrImportant;
}
