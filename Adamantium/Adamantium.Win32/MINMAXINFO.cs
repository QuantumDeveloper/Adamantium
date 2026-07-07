using System.Runtime.InteropServices;

namespace Adamantium.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL
    {
        public int X;
        public int Y;
    }

    /// <summary>WM_GETMINMAXINFO payload: the OS-proposed maximized size/position and the min/max drag-track sizes.
    /// Handlers override the fields (all in physical pixels) to constrain the window's size while it is dragged.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINTL ptReserved;
        public POINTL ptMaxSize;
        public POINTL ptMaxPosition;
        public POINTL ptMinTrackSize;
        public POINTL ptMaxTrackSize;
    }
}
