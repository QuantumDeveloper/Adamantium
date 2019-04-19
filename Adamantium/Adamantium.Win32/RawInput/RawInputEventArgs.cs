using System;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// RawInput event arguments base.
   /// </summary>
   public abstract class RawInputEventArgs:EventArgs
   {
      protected RawInputEventArgs()
      {
      }

      internal RawInputEventArgs(IntPtr device, IntPtr hwnd)
      {
         Device = device;
         WindowHandle = hwnd;
      }

      /// <summary>
      /// Gets or sets the RawInput device.
      /// </summary>
      /// <value>
      /// The device.
      /// </value>
      public IntPtr Device { get; set; }

      /// <summary>
      /// Gets or sets the handle of the window that received the RawInput mesage.
      /// </summary>
      /// <value>
      /// The window handle.
      /// </value>
      public IntPtr WindowHandle { get; set; }
   }
}
