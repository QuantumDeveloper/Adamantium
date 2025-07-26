using Adamantium.Win32;

namespace Adamantium.UI.Core;

public static class PlatformSettings
{
   public static UInt32 DoubleClickTime => Win32Interop.GetDoubleClickTime();
}