using System;

namespace Adamantium.Win32
{
   public sealed class MessageBox
   {
      public static MessageBoxResult Show(String text)
      {
         return Win32Interop.MessageBoxEx(IntPtr.Zero, text, String.Empty, (uint)MessageBoxButtons.OK, 0);
      }

      public static MessageBoxResult Show(IntPtr hwnd, String text)
      {
         return Win32Interop.MessageBoxEx(hwnd, text, String.Empty, (uint)MessageBoxButtons.OK, 0);
      }

      public static MessageBoxResult Show(String text, String caption, MessageBoxButtons buttons)
      {
         return Win32Interop.MessageBoxEx(IntPtr.Zero, text, caption, (uint)buttons, 0);
      }

      public static MessageBoxResult Show(String text, String caption, MessageBoxButtons buttons, MessageBoxIcon icon)
      {
         return Win32Interop.MessageBoxEx(IntPtr.Zero, text, caption, (uint)buttons | (uint)icon, 0);
      }

      public static MessageBoxResult Show(String text, String caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxOptions options)
      {
         return Win32Interop.MessageBoxEx(IntPtr.Zero, text, caption, (uint)buttons| (uint)icon | (uint)options, 0);
      }

      public static MessageBoxResult Show(IntPtr hwnd, String text, String caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxOptions options)
      {
         return Win32Interop.MessageBoxEx(hwnd, text, caption, (uint)buttons | (uint)icon | (uint)options, 0);
      }
   }
}
