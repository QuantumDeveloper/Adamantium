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

   /// <summary>Every monitor as one rectangle, in PHYSICAL pixels, or an empty one when the platform does not say.
   /// Used to check that a remembered window position still exists - see <see cref="IsOnScreen"/>.</summary>
   public static Rect VirtualScreen => Platform?.VirtualScreen ?? default;

   /// <summary>Whether enough of a remembered rectangle still falls on a monitor for a window there to be reachable.
   /// A layout saved with a panel on a second screen is loaded on a machine that no longer has one, and a window put
   /// back at those coordinates is a window nobody can get to - not even to close it.
   /// <para>"Enough" is its top-left corner plus a grabbable strip: a window is usable as long as some of its caption
   /// is on a screen, and demanding the whole rectangle would reject a window the user themselves left half off the
   /// edge. With no platform answer everything passes, which is what happened before the question was asked.</para></summary>
   public static bool IsOnScreen(Rect bounds)
   {
      var screen = VirtualScreen;
      if (screen.Width <= 0 || screen.Height <= 0) return true;

      const double grabbable = 48;
      return bounds.X + bounds.Width - grabbable > screen.X
             && bounds.X + grabbable < screen.X + screen.Width
             && bounds.Y + grabbable < screen.Y + screen.Height
             && bounds.Y + bounds.Height > screen.Y;
   }

   /// <summary>True once the pointer has moved far enough from where it was pressed for the gesture to be a DRAG. The
   /// delta is LOGICAL - what a control measures in its own space, which is what every caller inside a control has.
   /// Per-axis, not radial: that is what the OS setting means, and it is what every other application on the desktop
   /// does with it.
   /// <para>The OS reports the threshold in PHYSICAL pixels and it is compared as it comes. Exact at 100%, and slightly
   /// eager on a scaled display - a 4px threshold read as 4 DIP is 6 physical px at 150%. Deliberate: making it exact
   /// would need the window's scale at every call site, and no gesture turns on the difference. The physical overload
   /// below is exact, and is what the drag engine uses, since it measures on the desktop.</para></summary>
   public static bool ExceedsDragThreshold(Vector2 delta)
   {
      var threshold = DragThreshold;
      return Math.Abs(delta.X) > threshold.Width || Math.Abs(delta.Y) > threshold.Height;
   }

   /// <summary>The same question for a distance measured on the DESKTOP - between two cursor positions, say. Both sides
   /// are physical here, so this one is exact at any scale.</summary>
   public static bool ExceedsDragThreshold(PixelPoint delta)
   {
      var threshold = DragThreshold;
      return Math.Abs(delta.X) > threshold.Width || Math.Abs(delta.Y) > threshold.Height;
   }
}
