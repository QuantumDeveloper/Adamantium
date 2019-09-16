using System;
using Adamantium.Win32;

namespace Adamantium.UI
{
   public static class PlatformSettings
   {
      public static UInt32 DoubleClickTime => Win32Interop.GetDoubleClickTime();
   }
}
