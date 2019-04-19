using System;

namespace Adamantium.Win32
{
    public static class SystemParameters
    {
        public static int MouseSpeed
        {
            get
            {
                IntPtr speedPtr = IntPtr.Zero;
                Interop.SystemParametersInfo(SPI.GetMouseSpeed, 0, ref speedPtr, SPIF.None);
                return (int) speedPtr;
            }
        }

        public static int[] MouseAcceleration
        {
            get
            {
                int[] data = new int [3];
                Interop.SystemParametersInfo(SPI.GetMouse, 0, data, SPIF.None);
                return data;
            }
        }

        public static int VirtualScreenWidth => Interop.GetSystemMetrics(SystemMetrics.CxVirtualscreen);

        public static int VirtualScreenHeight => Interop.GetSystemMetrics(SystemMetrics.CyVirtualscreen);
    }
}