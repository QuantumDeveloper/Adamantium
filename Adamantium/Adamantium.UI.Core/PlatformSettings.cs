using Adamantium.Mathematics;
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

   /// <summary>How far the pointer must travel, PER AXIS, before a press becomes a drag - the user's own setting, so a
   /// shaky hand or a high-DPI mouse doesn't turn every click into a drag. 4x4 is the desktop default.</summary>
   public static Size DragThreshold => Platform?.DragThreshold ?? new Size(4, 4);

   /// <summary>How long the pointer must rest before it counts as a HOVER, in milliseconds - the user's dwell
   /// preference, and the pace for every "hold still and it opens" gesture. 400 is the desktop default; a platform
   /// reporting 0 (or none registered) falls back to it.</summary>
   public static UInt32 HoverTime => Platform?.HoverTime is { } time and > 0 ? time : 400;

   /// <summary>True once the pointer has moved far enough from where it was pressed for the gesture to be a DRAG.
   /// Per-axis, not radial: that is what the OS setting means, and it is what every other application on the desktop
   /// does with it.</summary>
   public static bool ExceedsDragThreshold(Vector2 delta)
   {
      var threshold = DragThreshold;
      return Math.Abs(delta.X) > threshold.Width || Math.Abs(delta.Y) > threshold.Height;
   }
}
