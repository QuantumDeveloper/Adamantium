using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering;

// Shared CPU packing of a gradient brush into the fields every gradient instance carries (the SDF rect/ellipse batches
// and the general-geometry gradient batch all use these). Keeps the stop-sort + opacity-fold + geometry mapping in ONE
// place so the shader-side layout can't drift between the paths. Up to 8 stops (the shader/instance cap).
internal static class GradientBake
{
    public const int MaxStops = 8;

    // Stops sorted by offset, opacity folded into each colour's alpha, packed into the 8-slot colour/offset arrays.
    // Returns the valid stop count (<= 8). Unused slots are left as passed in (callers zero-init the instance).
    public static int PackStops(GradientBrush g, float alpha, Span<Vector4F> colors, Span<float> offsets)
    {
        var stops = new List<GradientStop>(g.GradientStops);
        stops.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        var count = Math.Min(stops.Count, MaxStops);
        for (var i = 0; i < count; i++)
        {
            var c = stops[i].Color.ToVector4();
            c.W *= alpha;
            colors[i] = c;
            offsets[i] = (float)Math.Clamp(stops[i].Offset, 0.0, 1.0);
        }
        return count;
    }

    // The gradient geometry (relative 0..1): type (1 linear / 2 radial) + the two geometry vectors the shader reads.
    // Linear: Geom0 = (startXY, endXY). Radial: Geom0 = (centerXY, radiusXY), Geom1 = (originXY, 0, 0).
    public static float PackGeometry(GradientBrush g, out Vector4F geom0, out Vector4F geom1)
    {
        if (g is RadialGradientBrush radial)
        {
            geom0 = new Vector4F((float)radial.Center.X, (float)radial.Center.Y, (float)radial.RadiusX, (float)radial.RadiusY);
            geom1 = new Vector4F((float)radial.GradientOrigin.X, (float)radial.GradientOrigin.Y, 0f, 0f);
            return 2f;
        }
        var lin = (LinearGradientBrush)g;
        geom0 = new Vector4F((float)lin.StartPoint.X, (float)lin.StartPoint.Y, (float)lin.EndPoint.X, (float)lin.EndPoint.Y);
        geom1 = Vector4F.Zero;
        return 1f;
    }
}
