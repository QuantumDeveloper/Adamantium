using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

/// <summary>
/// Coordinate-space conversions between elements - the WPF <c>TransformToVisual</c>/<c>TranslatePoint</c> analogs. Built on
/// each element's <see cref="IUIComponent.WorldTransform"/> (local -> world), so they honour the FULL transform chain
/// (offsets + RenderTransforms, and a ScrollViewer's transform-only scroll) and work between ANY two elements, not just an
/// element and its ancestor.
/// </summary>
public static class VisualExtensions
{
    /// <summary>Translate a point in <paramref name="from"/>'s coordinate space into <paramref name="to"/>'s space:
    /// local -> world (via from's WorldTransform) -> local (via the inverse of to's).</summary>
    public static Vector2 TranslatePoint(this IUIComponent from, Vector2 point, IUIComponent to)
    {
        var world = Vector3F.TransformCoordinate(new Vector3F((float)point.X, (float)point.Y, 0), from.WorldTransform);
        var local = Vector3F.TransformCoordinate(world, Matrix4x4F.Invert(to.WorldTransform));
        return new Vector2(local.X, local.Y);
    }

    /// <summary><paramref name="from"/>'s own bounds (0..RenderSize) expressed in <paramref name="to"/>'s coordinate space.
    /// Corner-transformed then re-bounded, so a rotated/scaled element still yields a correct axis-aligned rect.</summary>
    public static Rect TransformBoundsToVisual(this IUIComponent from, IUIComponent to)
    {
        var size = from.RenderSize;
        var a = from.TranslatePoint(new Vector2(0, 0), to);
        var b = from.TranslatePoint(new Vector2(size.Width, 0), to);
        var c = from.TranslatePoint(new Vector2(0, size.Height), to);
        var d = from.TranslatePoint(new Vector2(size.Width, size.Height), to);
        var minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        var minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        var maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        var maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
