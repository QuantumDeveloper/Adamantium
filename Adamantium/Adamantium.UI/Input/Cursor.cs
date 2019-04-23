using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Adamantium.Win32;

namespace Adamantium.UI.Input
{
   /// <summary>
   /// Represents class which holds an instance of Win32 cursor
   /// </summary>
   public sealed class Cursor
   {
      internal IntPtr CursorHandle { get; private set; }

      /// <summary>
      /// Specifies a new instance of cursor class from a .cur or .ani file
      /// </summary>
      /// <param name="cursorFile"></param>
      public Cursor(String cursorFile)
      {
         CursorHandle = Interop.LoadCursorFromFile(cursorFile);
         if (CursorHandle == IntPtr.Zero)
         {
            var err = Marshal.GetLastWin32Error();
            throw new Win32Exception(err);
         }
      }

      internal Cursor(IntPtr cursorHandle)
      {
         CursorHandle = cursorHandle;
      }

      public static Cursor Default => new Cursor(Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow));
   }
}
