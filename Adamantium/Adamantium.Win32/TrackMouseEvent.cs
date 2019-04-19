using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT
    {
        public int cbSize;
        public uint dwFlags;
        public IntPtr hwndTrack;
        public int dwHoverTime;
    }
}
