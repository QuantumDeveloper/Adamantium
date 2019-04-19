using System;

namespace Adamantium.Win32.RawInput
{
   public class RawKeyboardInputEventArgs:RawInputEventArgs
   {
      public RawKeyboardInputEventArgs()
      {
      }

      internal RawKeyboardInputEventArgs(ref RawKeyboard keyboard, IntPtr device, IntPtr hwnd) : base(device, hwnd)
      {
         Key = keyboard.VirtualKey;
         MakeCode = keyboard.MakeCode;
         ScanCodeFlags = (ScanCodeFlags)keyboard.Flags;
         State = (RawKeyState)keyboard.Message;
         ExtraInformation = keyboard.ExtraInformation;
      }

      /// <summary>
      /// Gets or sets the key.
      /// </summary>
      /// <value>
      /// The key.
      /// </value>
      public uint Key { get; set; }

      /// <summary>
      /// Gets or sets the make code.
      /// </summary>
      /// <value>
      /// The make code.
      /// </value>
      public int MakeCode { get; set; }

      /// <summary>
      /// Gets or sets the scan code flags.
      /// </summary>
      /// <value>
      /// The scan code flags.
      /// </value>
      public ScanCodeFlags ScanCodeFlags { get; set; }

      /// <summary>
      /// Gets or sets the state.
      /// </summary>
      /// <value>
      /// The state.
      /// </value>
      public RawKeyState State { get; set; }

      /// <summary>
      /// Gets or sets the extra information.
      /// </summary>
      /// <value>
      /// The extra information.
      /// </value>
      public int ExtraInformation { get; set; }
   }
}
