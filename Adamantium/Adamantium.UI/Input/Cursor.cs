using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Adamantium.MacOS;
using Adamantium.Win32;

namespace Adamantium.UI.Input
{
   /// <summary>
   /// Represents class which holds an instance of Win32 cursor
   /// </summary>
   public sealed class Cursor
   {
      public IntPtr CursorHandle { get; private set; }

      /// <summary>
      /// Specifies a new instance of cursor class from a .cur or .ani file
      /// </summary>
      /// <param name="cursorFile"></param>
      public Cursor(String cursorFile)
      {
         LoadCursorFromFile(cursorFile);
      }

      internal Cursor(IntPtr cursorHandle)
      {
         CursorHandle = cursorHandle;
      }

      public static Cursor Default => New();

      internal static Cursor New()
      {
         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
         {
            return new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow));
         }
         else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
         {
            return new Cursor(MacOSInterop.Cursor.SetCursorType((uint)MacOSCursorType.CrosshairCursor));
         }
         throw new NotImplementedException($"Cursor is not implemented for {RuntimeInformation.OSDescription}");
      }

      internal void LoadCursorFromFile(String cursorFile)
      {
         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
         {
            CursorHandle = Win32Interop.LoadCursorFromFile(cursorFile);
            if (CursorHandle == IntPtr.Zero)
            {
               var err = Marshal.GetLastWin32Error();
               throw new Win32Exception(err);
            }
         }
         else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
         {
            
         }
      }
   }
}
