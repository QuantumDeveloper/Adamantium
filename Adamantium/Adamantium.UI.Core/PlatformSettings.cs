using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Core;

/// <summary>Input settings the USER configured in the OS. Honouring them is what keeps the app feeling native, so they
/// are queried from the platform rather than guessed.</summary>
public static class PlatformSettings
{
   /// <summary>The platform that answers these, registered once at startup; null falls back to the defaults below.</summary>
   public static INativePlatformSettings Platform { get; set; }

   /// <summary>Longest gap between two clicks that still counts as a double-click, in milliseconds. 500 is the default
   /// every desktop OS ships with, and what we use until a platform says otherwise.</summary>
   public static UInt32 DoubleClickTime => Platform?.DoubleClickTime ?? 500;

}
