using Adamantium.Core;
using System;

namespace Adamantium.Win32.RawInput
{
   public class RawHidInputEventArgs:RawInputEventArgs
   {
      public RawHidInputEventArgs()
      {
         
      }

      internal RawHidInputEventArgs(ref RawInputHid hid, IntPtr device, IntPtr hwnd) : base(device, hwnd)
      {
         Count = hid.Count;
         DataSize = hid.Size;
         RawData = new byte[Count * DataSize];
         unsafe
         {
            if (RawData.Length > 0)
            {
               fixed (void* toPtr = RawData)
               {
                  fixed (void* fromPtr = &hid.Data)
                  {
                     Utilities.CopyMemory((IntPtr)toPtr, (IntPtr)fromPtr, RawData.Length * sizeof(byte));
                  }
               }
            }
         }
      }

      /// <summary>
      /// Gets or sets the number of Hid structure in the <see cref="RawData"/>.
      /// </summary>
      /// <value>
      /// The count.
      /// </value>
      public int Count { get; set; }

      /// <summary>
      /// Gets or sets the size of the Hid structure in the <see cref="RawData"/>.
      /// </summary>
      /// <value>
      /// The size of the data.
      /// </value>
      public int DataSize { get; set; }

      /// <summary>
      /// Gets or sets the raw data.
      /// </summary>
      /// <value>
      /// The raw data.
      /// </value>
      public byte[] RawData { get; set; }
   }
}
