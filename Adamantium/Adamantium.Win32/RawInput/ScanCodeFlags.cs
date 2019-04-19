using System;

namespace Adamantium.Win32.RawInput
{
   [Flags]
   public enum ScanCodeFlags : ushort
   {
      /// <summary>
      /// The key is down.
      /// </summary>
      KeyMake = 0,

      /// <summary>
      /// The key is up.
      /// </summary>
      KeyBreak = 1,

      /// <summary>
      /// This is the left version of the key.
      /// </summary>
      KeyE0 = 2,

      /// <summary>
      /// This is the right version of the key.
      /// </summary>
      KeyE1 = 4,
   }
}
