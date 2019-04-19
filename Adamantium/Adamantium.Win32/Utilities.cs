using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32
{
   public static class Utilities
   {
      [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = true)]
      public static extern void CopyMemory(IntPtr destination, IntPtr source, int count);

      public static Int32 SizeOf<T>() where T : struct
      {
         return Marshal.SizeOf<T>();
      }
   }
}
