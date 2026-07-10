using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// Extracts the RESOLVED fill boundary (the outline of what is actually painted, holes included) from a tessellated fill
/// mesh, as closed point loops. The analytic-AA fringe needs THIS - not the raw path contours - so that a self-
/// intersecting shape (a pentagram) or a shape with holes gets its inner edges feathered too: the tessellator already
/// resolved the fill (even-odd / non-zero) into triangles, so the boundary between filled and un-filled is exactly the
/// set of triangle edges that belong to a SINGLE triangle. Chaining those boundary edges gives the outer outline plus one
/// loop per hole; the fringe's own even-odd nesting then feathers holes inward and outers outward.
/// </summary>
internal static class FillBoundary
{
    // Positions are quantised (to 1e-3) before comparison so the tessellator's duplicated-but-coincident vertices merge -
    // otherwise a shared edge would look like two single-use edges and every interior edge would be mis-flagged boundary.
    private static (long, long) Q(Vector3 p) => ((long)Math.Round(p.X * 1000.0), (long)Math.Round(p.Y * 1000.0));

    private static (long, long, long, long) EdgeKey((long, long) a, (long, long) b)
        => Comparer<(long, long)>.Default.Compare(a, b) <= 0 ? (a.Item1, a.Item2, b.Item1, b.Item2) : (b.Item1, b.Item2, a.Item1, a.Item2);

    /// <summary>The fill's boundary loops, or null if the mesh has no drawable triangles (caller falls back to the raw
    /// path contours). Each loop is a closed point list (point[n-1] -> point[0] implied), in mesh-LOCAL coordinates.</summary>
    public static List<(Vector2[] Points, bool IsClosed)> ExtractLoops(Mesh mesh)
    {
        var pts = mesh?.Points;
        if (pts == null || pts.Length < 3) return null;
        var idx = mesh.HasIndices ? mesh.Indices : null;
        var triCount = idx != null ? idx.Length / 3 : pts.Length / 3;
        if (triCount == 0) return null;

        // Count each undirected edge; keep a representative real position per quantised vertex.
        var edgeCount = new Dictionary<(long, long, long, long), int>();
        var rep = new Dictionary<(long, long), Vector2>();

        void AddEdge(Vector3 a, Vector3 b)
        {
            var qa = Q(a);
            var qb = Q(b);
            if (qa == qb) return;
            rep[qa] = new Vector2((float)a.X, (float)a.Y);
            rep[qb] = new Vector2((float)b.X, (float)b.Y);
            var key = EdgeKey(qa, qb);
            edgeCount.TryGetValue(key, out var c);
            edgeCount[key] = c + 1;
        }

        for (var t = 0; t < triCount; t++)
        {
            Vector3 a, b, c;
            if (idx != null) { a = pts[idx[t * 3]]; b = pts[idx[t * 3 + 1]]; c = pts[idx[t * 3 + 2]]; }
            else { a = pts[t * 3]; b = pts[t * 3 + 1]; c = pts[t * 3 + 2]; }
            AddEdge(a, b); AddEdge(b, c); AddEdge(c, a);
        }

        // Boundary edges are used by exactly one triangle -> adjacency of the boundary graph.
        var adj = new Dictionary<(long, long), List<(long, long)>>();
        foreach (var kv in edgeCount)
        {
            if (kv.Value != 1) continue;
            var p = (kv.Key.Item1, kv.Key.Item2);
            var q = (kv.Key.Item3, kv.Key.Item4);
            (adj.TryGetValue(p, out var lp) ? lp : adj[p] = new List<(long, long)>()).Add(q);
            (adj.TryGetValue(q, out var lq) ? lq : adj[q] = new List<(long, long)>()).Add(p);
        }
        if (adj.Count == 0) return null;

        // Walk closed loops, greedily consuming boundary edges (a >2-degree vertex at a self-intersection just picks any
        // unused edge - fine for feathering). Each loop = outer outline or one hole.
        var loops = new List<(Vector2[], bool)>();
        var visited = new HashSet<(long, long, long, long)>();
        foreach (var startNode in adj.Keys)
        {
            foreach (var first in adj[startNode])
            {
                if (visited.Contains(EdgeKey(startNode, first))) continue;

                var loop = new List<Vector2> { rep[startNode] };
                var cur = startNode;
                var next = first;
                while (true)
                {
                    var e = EdgeKey(cur, next);
                    if (visited.Contains(e)) break;
                    visited.Add(e);
                    if (next == startNode) break;   // closed - don't re-add the start point
                    loop.Add(rep[next]);
                    var prev = cur;
                    cur = next;
                    var found = false;
                    foreach (var nb in adj[cur])
                    {
                        if (nb == prev) continue;
                        if (visited.Contains(EdgeKey(cur, nb))) continue;
                        next = nb; found = true; break;
                    }
                    if (!found) break;
                }
                if (loop.Count >= 3) loops.Add((loop.ToArray(), true));
            }
        }
        return loops.Count > 0 ? loops : null;
    }
}
