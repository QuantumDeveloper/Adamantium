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
                Win32Interop.SystemParametersInfo(SPI.GetMouseSpeed, 0, ref speedPtr, SPIF.None);
                return (int) speedPtr;
            }
        }

        public static int[] MouseAcceleration
        {
            get
            {
                int[] data = new int [3];
                Win32Interop.SystemParametersInfo(SPI.GetMouse, 0, data, SPIF.None);
                return data;
            }
        }

        public static int VirtualScreenWidth => Win32Interop.GetSystemMetrics(SystemMetrics.CxVirtualscreen);

        public static int VirtualScreenHeight => Win32Interop.GetSystemMetrics(SystemMetrics.CyVirtualscreen);
    }
}