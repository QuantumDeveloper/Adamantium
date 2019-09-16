using System;
using Adamantium.Win32;

namespace Adamantium.UI.Input
{
   public static class Cursors
   {
      public static Cursor Arrow => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow));

      public static Cursor AppStarting => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.AppStarting));

      public static Cursor Crosshair => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Crosshair));

      public static Cursor Hand => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Hand));

      public static Cursor Help => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Help));

      public static Cursor IBeam => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.IBeam));

      public static Cursor No => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.No));

      public static Cursor SizeAll => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeAll));

      public static Cursor SizeNESW => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeNESW));

      public static Cursor SizeNS => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeNS));

      public static Cursor SizeNWSE => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeNWSE));

      public static Cursor SizeEWE => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.SizeEWE));

      public static Cursor UpArrow => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.UpArrow));

      public static Cursor Wait => new Cursor(Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Wait));

      public static Cursor None => new Cursor(IntPtr.Zero);
   }
}
