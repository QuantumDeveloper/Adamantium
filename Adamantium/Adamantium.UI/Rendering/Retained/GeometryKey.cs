using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>The family of local geometry a <see cref="GeometryKey"/> identifies. Parametric kinds carry their exact
/// shape params in the key (no false merges); <see cref="Mesh"/> is arbitrary tessellated geometry keyed by a content
/// fingerprint.</summary>
public enum GeometryKind : byte
{
    RoundedRect,
    Ellipse,
    Mesh
}

/// <summary>
/// Stable identity of a LOCAL geometry (docs/RENDER_CACHE_REDESIGN.md §4e): same key ⟺ same tessellated vertices in the
/// element's own coordinate space, so two elements with the same key SHARE one mesh and instance together. Deliberately
/// excludes the world transform and the colour - those are per-instance (<see cref="GeometryInstance"/>). Corner-radius
/// STRUCTURE / stroke presence that changes the shape ARE part of the key; the fill colour is not.
/// </summary>
/// <remarks>
/// Parametric shapes (rounded rect, ellipse) store their exact defining numbers, so equality is exact - no chance of
/// two different shapes merging into one mesh. Arbitrary geometry (a <see cref="GeometryKind.Mesh"/> from a Path) is
/// keyed by a 64-bit content fingerprint of its local vertices; a fingerprint collision would wrongly merge two shapes,
/// so the registry keeps the source mesh and can validate on insert (a rare, cheap check on a structural change).
/// </remarks>
public readonly struct GeometryKey : IEquatable<GeometryKey>
{
    public readonly GeometryKind Kind;
    private readonly Vector4F _p0;    // parametric params A (rect bounds / ellipse bounds)
    private readonly Vector4F _p1;    // parametric params B (corner radii / ellipse angles+type)
    private readonly long _meshHash;  // Mesh kind: content fingerprint of the local vertices (0 for parametric)

    private GeometryKey(GeometryKind kind, Vector4F p0, Vector4F p1, long meshHash)
    {
        Kind = kind;
        _p0 = p0;
        _p1 = p1;
        _meshHash = meshHash;
    }

    /// <summary>A rounded rectangle: exact bounds + the four corner radii (all in local space). The
    /// <see cref="CornerRadius"/> is packed into the key's params as (TopLeft, TopRight, BottomRight, BottomLeft) - the
    /// same order the payload carries - so equality stays exact per corner.</summary>
    public static GeometryKey RoundedRectangle(Rect bounds, CornerRadius corners) => new(
        GeometryKind.RoundedRect,
        new Vector4F((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height),
        new Vector4F((float)corners.TopLeft, (float)corners.TopRight, (float)corners.BottomRight, (float)corners.BottomLeft),
        0);

    /// <summary>An ellipse / arc: exact bounds + start/sweep angle + the ellipse type discriminator.</summary>
    public static GeometryKey EllipseArc(Rect bounds, double startAngle, double sweepAngle, int ellipseType) => new(
        GeometryKind.Ellipse,
        new Vector4F((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height),
        new Vector4F((float)startAngle, (float)sweepAngle, ellipseType, 0),
        0);

    /// <summary>Arbitrary tessellated geometry (a Path), identified by a content fingerprint of its LOCAL vertices.</summary>
    public static GeometryKey ArbitraryMesh(long fingerprint) => new(GeometryKind.Mesh, default, default, fingerprint);

    public bool Equals(GeometryKey other) =>
        Kind == other.Kind && _meshHash == other._meshHash && _p0.Equals(other._p0) && _p1.Equals(other._p1);

    public override bool Equals(object obj) => obj is GeometryKey k && Equals(k);

    public override int GetHashCode() => HashCode.Combine(Kind, _p0, _p1, _meshHash);

    public override string ToString() => Kind switch
    {
        GeometryKind.RoundedRect => $"Rect[{_p0.X:0},{_p0.Y:0} {_p0.Z:0}x{_p0.W:0} r{_p1.X:0}]",
        GeometryKind.Ellipse => $"Ellipse[{_p0.Z:0}x{_p0.W:0} {_p1.X:0}°+{_p1.Y:0}°]",
        _ => $"Mesh[{_meshHash:x8}]"
    };
}
