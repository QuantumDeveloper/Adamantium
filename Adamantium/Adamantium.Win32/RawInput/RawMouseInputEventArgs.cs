using System;

namespace Adamantium.Win32.RawInput
{
   public class RawMouseInputEventArgs:RawInputEventArgs
   {
      public RawMouseInputEventArgs()
      {
         
      }

      internal RawMouseInputEventArgs(ref RawMouse mouse, IntPtr device, IntPtr hwnd):base(device, hwnd)
      {
         ButtonsFlags = mouse.Data.ButtonFlags;
         DeltaX = mouse.LastX;
         DeltaY = mouse.LastY;
         WheelDelta = mouse.Data.ButtonData;
         ExtraInformation = mouse.ExtraInformation;
         Buttons = mouse.RawButtons;
         Mode = mouse.Mode;
      }

      public MouseMode Mode { get; set; }

      public RawMouseButtons ButtonsFlags { get; set; }

      public int DeltaX { get; set; }

      public int DeltaY { get; set; }

      public int WheelDelta { get; set; }

      public uint ExtraInformation { get; set; }

      public uint Buttons { get; set; }


   }
}
