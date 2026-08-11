using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

/// <summary>
/// Putting geometry onto WHOLE DEVICE PIXELS, for anything that draws a thin edge.
/// <para>A 1-DIP line is 1.5 physical pixels at 150%, and where it starts depends on every offset above it in the tree.
/// Half a pixel off the grid, it is drawn at half coverage on both sides and reads as no line at all - measured on a
/// docking panel at 150%, where the same 1-DIP border was crisp along the top and "half a pixel" along the bottom,
/// purely from where the two edges fell.</para>
/// <para>This belongs at RENDER, not in layout: what has to sit on the grid is the line's place ON SCREEN, and that is
/// known only once the whole chain of offsets above it is in. Rounding layout to whole DIPs cannot express it either -
/// one DIP is not a pixel at a fractional scale.</para>
/// </summary>
public static class DevicePixels
{
    // Anything but a plain offset - a zoomed or rotated subtree - and "one device pixel" is no longer a fixed length in
    // the element's own space, so snapping to it would distort what it snapped.
    private const float Tolerance = 1e-4f;

    /// <summary>Moves a rectangle drawn by <paramref name="element"/> onto pixel boundaries. False when snapping does
    /// not apply (no window, or a transform other than an offset), leaving the rectangle untouched.</summary>
    public static bool Snap(this IUIComponent element, ref Rect rect)
    {
        if (!Origin(element, out var at, out var scale)) return false;

        rect = Snapped(rect, at, scale);
        return true;
    }

    /// <summary>The same, plus a THICKNESS in DIPs rounded to whole pixels - never below one, so a hairline asked for in
    /// DIPs cannot round away to nothing.</summary>
    public static bool Snap(this IUIComponent element, ref Rect rect, ref double thickness)
    {
        if (!Origin(element, out var at, out var scale)) return false;

        rect = Snapped(rect, at, scale);
        thickness = Math.Max(1.0, Math.Round(thickness * scale.X, MidpointRounding.AwayFromZero)) / scale.X;
        return true;
    }

    /// <summary>A LENGTH in DIPs as a whole number of pixels - for a line's width, a gap, a shadow's offset. Independent
    /// of where the element sits, so it is safe wherever only the size matters.</summary>
    public static double SnapLength(this IUIComponent element, double length)
    {
        if (!Origin(element, out _, out var scale)) return length;

        return Math.Round(length * scale.X, MidpointRounding.AwayFromZero) / scale.X;
    }

    /// <summary>Where <paramref name="element"/> sits in its window (DIPs) and what a DIP is worth in pixels there.
    /// False when the element is not in a window, or is under anything but a plain offset.</summary>
    private static bool Origin(IUIComponent element, out Vector2 at, out Vector2 scale)
    {
        at = default;
        scale = Vector2.One;

        if (element?.RootVisual is not IWindow window) return false;

        scale = window.DpiScale;
        if (scale.X <= 0 || scale.Y <= 0) return false;

        var world = element.WorldTransform;
        if (Math.Abs(world.M11 - 1) > Tolerance || Math.Abs(world.M22 - 1) > Tolerance
            || Math.Abs(world.M12) > Tolerance || Math.Abs(world.M21) > Tolerance)
        {
            return false;
        }

        at = new Vector2(world.M41, world.M42);
        return true;
    }

    // Both edges of each axis, so a rectangle keeps its corners on the grid rather than its position plus a rounded size
    // - rounding the size instead leaves the far edge off by whatever the near one was moved.
    private static Rect Snapped(Rect rect, Vector2 at, Vector2 scale)
    {
        var left = Edge(rect.X, at.X, scale.X);
        var top = Edge(rect.Y, at.Y, scale.Y);
        var right = Edge(rect.X + rect.Width, at.X, scale.X);
        var bottom = Edge(rect.Y + rect.Height, at.Y, scale.Y);

        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    // A local coordinate taken to the nearest pixel boundary and brought back into local units.
    //
    // AwayFromZero, NOT the default. At a fractional scale a whole number of DIPs lands on HALF a pixel - at 150% every
    // integer coordinate does - so essentially every edge is a midpoint case. The default rounds midpoints to the nearest
    // EVEN pixel, which means the direction flips with parity: 58.5 goes down to 58 while 85.5 goes up to 86. Two edges
    // half a pixel out then move OPPOSITE ways, and neighbours that should line up end up a pixel apart - which is
    // exactly what made two tabs of the same height look different at 150%.
    private static double Edge(double local, double origin, double scale)
    {
        return Math.Round((origin + local) * scale, MidpointRounding.AwayFromZero) / scale - origin;
    }
}
