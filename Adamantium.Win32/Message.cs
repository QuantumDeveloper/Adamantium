using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Win32
{
   [StructLayout(LayoutKind.Sequential)]
   public struct Message
   {
      public IntPtr Handle;
      public WindowMessages Msg;
      public IntPtr WParam;
      public IntPtr LParam;
      public uint Time;
      public NativePoint Point;
   }

}
