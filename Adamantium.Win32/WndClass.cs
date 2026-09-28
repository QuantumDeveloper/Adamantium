using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32
{
   [StructLayout(LayoutKind.Sequential,CharSet = CharSet.Unicode)]
   public struct WndClass
   {
      public WindowClassStyle style;
      public IntPtr lpfnWndProc;
      public int cbClsExtra;
      public int cbWndExtra;
      public IntPtr hInstance;
      public IntPtr hIcon;
      public IntPtr hCursor;
      public IntPtr hbrBackground;
      [MarshalAs(UnmanagedType.LPWStr)]
      public string lpszMenuName;
      [MarshalAs(UnmanagedType.LPWStr)]
      public string lpszClassName;
   }
}
