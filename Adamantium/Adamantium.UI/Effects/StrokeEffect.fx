// GPU stroke effect (line-rendering Phase B/C). One compute technique (StrokeExpand) turns a polyline + half-thickness
// into a miter-joined triangle-LIST ribbon written straight into a vertex buffer via a BDA device address, and one
// graphics technique (StrokeDraw) rasterizes it. Both live in a single .fx so the generator emits one Effect class
// (StrokeEffect) - no C# wrapper needed. Shader bodies are Slang.
//
//   output: one thread per SEGMENT emits a quad as 2 triangles (6 vertices) -> segmentCount * 6 vertices.
//   open   => PointCount - 1 segments;  closed => PointCount segments (last wraps back to point 0).
//
// A triangle LIST (not a strip) keeps segments independent, which is the basis for disjoint dashes and variable
// per-join fans (round joins) added on top.
//
// CAPS ARE A MASK, NOT GEOMETRY. A triangle list can only ADD material, so a cap that has to SUBTRACT (the concave ones
// bite half a thickness inward) cannot be geometry: whatever else lands in the notch - the next quad, a join disc -
// fills it straight back in. That overlap was the row of horns on every dash end. Instead every vertex carries its
// position in the two END FRAMES of its PIECE (a piece = one dash, one trim run, or the whole open contour) plus the
// cap code at each end, and StrokePS carves them per fragment. Two consequences worth knowing:
//   - the mask covers the WHOLE piece, joins included, so a corner that falls inside a concave cap's bite is carved by
//     the same curve as the ribbon instead of poking out of it;
//   - the frames are STRAIGHT (anchor + tangent at the end), which is what a cap actually is. Measuring the bite along
//     the CONTOUR instead wraps it around corners, and a round join - whose fragments carry a RADIAL distance, not a
//     perpendicular one - then turned the bite into a circular hole punched through the dash.
// Convex caps stay geometry-free too: the quad simply extends past the end and the mask shapes the bulge/tip.

// --- StrokeExpand (compute) globals ---
uint64_t PointsAddress;   // float2[] polyline points (PointCount of them)
uint64_t OutputAddress;   // float[] output vertices, 8 floats each (see WriteVert)
uint PointCount;          // continuous mode: polyline points. dash mode: piece-points (2 per dash piece).
uint IsClosed;            // 0 = open polyline (flat ends), 1 = closed loop (wrap-around miters + closing pair)
uint StartCap;            // start cap: 0 = flat, 1 = square, 2 = convex round, 3 = convex tri, 4 = concave tri, 5 = concave round
uint EndCap;              // end cap (same codes)
uint DashStartCap;        // cap on the STARTING end of each dash/dot piece (cut path); the contour's real ends use Start/EndCap
uint DashEndCap;          // cap on the ENDING end of each dash/dot piece - separate, so a dash can be an arrow
uint JoinType;            // 0 = miter (bisector ribbon, clamped), 1 = bevel (per-segment rectangles + corner wedge), 2 = round (rectangles + disc fan)
uint RoundSegments;       // disc-fan subdivision for round joins; 0 = no round geometry (no disc slots)
uint DashMode;            // 0 = continuous polyline (per-point), 1 = one-pass GPU cut (dash/trim, single-thread + DrawIndirect)
float HalfThickness;
float Fringe;             // AA feather width in geometry-LOCAL units (~1 device px / scale); ribbon/discs widen by Fringe/2

// --- one-pass GPU cut (DashMode == 1): the GPU walks the contour by arc length and emits dash/trim pieces ---
uint64_t IndirectAddress; // VkDrawIndirectCommand the cut writes (vertexCount = GPU-decided draw size)
uint64_t PatternAddress;  // float[] dash pattern (on,off,...); PatternCount entries, 0 = solid (one piece over trim)
uint PatternCount;
uint MaxVertices;         // output capacity guard for the cut (CPU sizes it from a worst-case piece bound)
float DashOffset;
float TrimStart;          // 0..1 fraction of total arc length
float TrimEnd;            // 0..1

// --- StrokeDraw (graphics) globals ---
float4x4 Projection;
float4 StrokeColor;

// The two ends of the piece being emitted: where each one is, which way the contour runs there (dA points INTO the
// piece, dB OUT of it) and which cap each wears - capStart + 8*capEnd, with 6 meaning "this end is not an end"
// (an interior join, a closed loop), which leaves that cap's mask inert.
struct PieceFrame
{
    float2 A;
    float2 dA;
    float2 B;
    float2 dB;
    float caps;
    // How much SHALLOWER than a half-thickness that end's bite has to be, as a scale on its coordinates: a concave cap
    // subtracts, and its formulas are homogeneous, so carving to depth c instead of h is exactly feeding them u, v and
    // arc multiplied by h/c. 1 = full depth. Keeping it here rather than as another vertex float costs nothing and
    // leaves the pixel shader unchanged. See BiteDepth for what limits c.
};

// A frame for geometry that has no ends at all.
PieceFrame NoCaps()
{
    PieceFrame f;
    f.A = float2(0.0, 0.0);
    f.dA = float2(1.0, 0.0);
    f.B = float2(0.0, 0.0);
    f.dB = float2(1.0, 0.0);
    f.caps = float(6u + 8u * 6u);
    return f;
}

// Unit normal of the segment a->b (perp of the normalized direction).
float2 SegmentNormal(float2 a, float2 b)
{
    float2 d = normalize(b - a);
    return float2(-d.y, d.x);
}

// Is a join at this turn worth emitting at all? What a missing join leaves is not a shallow dent but a SECTOR: the two
// per-segment rectangles end on their own perpendiculars through the vertex, so a turn of angle t opens a slit of angle
// t and radius hw between them, running from the centerline right out to the edge. Its width out there is hw * t - FIRST
// order in the angle. (Measuring the sagitta hw*(1-cos(t/2)) instead - second order, t^2/8 - is off by 20-50x at the
// angles a tessellated curve actually turns, and declaring those joins unnecessary made the ribbon fall apart.)
// A join is skipped only when that slit is under a quarter pixel wide, which on a dense curve it genuinely is - and it
// is worth skipping there, because a disc is not free: its feathered rim blends over the ribbon it sits in, and that is
// the join "showing through" a dash.
bool JoinIsVisible(float2 dIn, float2 dOut)
{
    float hw = HalfThickness + Fringe * 0.5;
    float px = Fringe > 0.0 ? Fringe : 0.05;   // Fringe is ~1 device px in these local units; AA off -> a fixed floor
    return hw * length(dOut - dIn) > 0.25 * px;   // |dOut - dIn| = the turn angle, to first order
}

// How far past the geometry point a cap's quad has to REACH so the mask has material to shape: the convex caps and the
// square nub paint up to half a thickness beyond the end, the flat and concave ones never paint past it (concave caps
// carve backwards, so they need no room at all). The AA feather is added on top by the callers.
float CapExtend(uint cap)
{
    if (cap == 1u || cap == 2u || cap == 3u) return HalfThickness;   // square / convex round / convex triangle
    return 0.0;                                                      // flat, concave triangle, concave round
}

// How far point i's cross-section is pushed OUT past the geometry point by its cap (open contour ends only).
float EndExtend(uint i)
{
    if (IsClosed != 0u) return 0.0;
    if (i == 0u) return CapExtend(StartCap) + Fringe * 0.5;
    if (i + 1u == PointCount) return CapExtend(EndCap) + Fringe * 0.5;
    return 0.0;
}

// The two offset vertices (centerline +/- miter) at polyline point i, for the MITER join path only (bevel/round build
// full-width per-segment rectangles instead - see StrokeExpandCS). Open endpoints use the single adjacent segment
// normal (flat) or push out by the cap's reach; interior points and every point of a closed loop use the bisector
// miter, length clamped so a sharp corner can't shoot the tip to infinity.
void OffsetPair(uint i, out float2 plus, out float2 minus)
{
    float2* points = (float2*)PointsAddress;
    float2 p = points[i];

    float hw = HalfThickness + Fringe * 0.5;   // widen the ribbon by the AA feather; v carries +/-hw (see EmitQuad)
    float2 miter;
    float miterLen;

    if (IsClosed == 0 && i == 0)
    {
        float2 dir = normalize(points[1] - points[0]);   // inward; outward is -dir
        p -= dir * EndExtend(i);                          // room for the cap mask to shape (convex caps only)
        miter = float2(-dir.y, dir.x);
        miterLen = hw;
    }
    else if (IsClosed == 0 && i + 1 == PointCount)
    {
        float2 dir = normalize(points[i] - points[i - 1]);   // outward
        p += dir * EndExtend(i);
        miter = float2(-dir.y, dir.x);
        miterLen = hw;
    }
    else
    {
        uint prev = (i + PointCount - 1) % PointCount;
        uint next = (i + 1) % PointCount;
        float2 n0 = SegmentNormal(points[prev], p);
        float2 n1 = SegmentNormal(p, points[next]);
        miter = normalize(n0 + n1);
        float denom = max(dot(miter, n0), 0.25);       // clamp -> miter length capped at 4*half on sharp corners
        miterLen = hw / denom;
    }

    plus = p + miter * miterLen;
    minus = p - miter * miterLen;
}

// Position + unit tangent at arc length `target` along the contour. The cut needs the piece's END before it writes the
// piece's FIRST vertex - a cap that carves inward reaches back over everything in between - and the end is known in arc
// length long before the walk gets there. No early return: an early exit inside an inlined .fx helper has made the
// NVIDIA NVVM compiler AV in vkCreateShadersEXT before.
void PointAtArc(float target, out float2 pos, out float2 dir)
{
    float2* points = (float2*)PointsAddress;
    uint segCount = IsClosed != 0u ? PointCount : PointCount - 1u;
    pos = points[0];
    dir = normalize(points[1] - points[0]);
    float acc = 0.0;
    bool found = false;
    for (uint s = 0u; s < segCount; ++s)
    {
        float2 a = points[s];
        float2 b = points[(s + 1u) % PointCount];
        float len = length(b - a);
        if (len > 1e-6)
        {
            float2 d = (b - a) / len;
            if (!found && (acc + len >= target || s + 1u == segCount))
            {
                pos = a + d * clamp(target - acc, 0.0, len);
                dir = d;
                found = true;
            }
            acc += len;
        }
    }
}

// One output vertex = 10 floats: (x, y | perp, uA, vA, arcA | caps, uB, vB, arcB).
//   perp      = signed perpendicular distance from the centerline (a disc's radial distance) -> the ribbon's AA band
//   u / v     = the vertex in that end's STRAIGHT frame: how far inside the piece (u > 0), and across it
//   arc       = the same distance to that end, but measured ALONG THE CONTOUR - the cap's REACH
//   caps      = capStart + 8 * capEnd (6 = no cap on that end)
// Shape from the frame, extent from the arc, and BOTH are needed - each alone has been tried and is a distinct bug.
//   * The arc alone cannot shape: it is constant across a join disc, so a bite came out as a circular hole punched
//     through the dash. `perp` cannot stand in for v either, for exactly the same reason - it is RADIAL on a disc.
//   * The frame alone cannot bound: its axis is an infinite plane, so where the contour turns back on itself (the far
//     edge of a thin star spike, a tight U) ribbon that is 20px away along the path lands "behind" the cap and gets
//     shaved into a hair.
// All ten are affine in the position along a segment, so they interpolate exactly across a triangle.
void WriteVert(float* o, uint vi, float2 p, float perp, float arcA, float arcB, PieceFrame f)
{
    float2 rA = p - f.A;
    float2 rB = p - f.B;
    uint b = vi * 10u;
    o[b + 0u] = p.x;
    o[b + 1u] = p.y;
    o[b + 2u] = perp;
    o[b + 3u] = dot(rA, f.dA);
    o[b + 4u] = dot(rA, float2(-f.dA.y, f.dA.x));
    o[b + 5u] = arcA;
    o[b + 6u] = f.caps;
    o[b + 7u] = -dot(rB, f.dB);
    o[b + 8u] = dot(rB, float2(-f.dB.y, f.dB.x));
    o[b + 9u] = arcB;
}

// Writes a quad (two triangles, 6 verts) from four corners into outVerts at vertex offset o.
// hw = widened half-extent (HalfThickness + Fringe/2): the P corners sit at +hw perpendicular, the M corners at -hw, and
// the signed distance v=+/-hw is interpolated across the quad so StrokePS feathers both long edges.
void EmitQuad(float* outVerts, uint o, float2 aP, float2 aM, float2 bP, float2 bM, float hw,
              float aArcA, float aArcB, float bArcA, float bArcB, PieceFrame f)
{
    WriteVert(outVerts, o + 0u, aP,  hw, aArcA, aArcB, f);
    WriteVert(outVerts, o + 1u, aM, -hw, aArcA, aArcB, f);
    WriteVert(outVerts, o + 2u, bP,  hw, bArcA, bArcB, f);
    WriteVert(outVerts, o + 3u, bP,  hw, bArcA, bArcB, f);
    WriteVert(outVerts, o + 4u, aM, -hw, aArcA, aArcB, f);
    WriteVert(outVerts, o + 5u, bM, -hw, bArcA, bArcB, f);
}

// Writes a degenerate (zero-area) quad at o - used for slots a thread doesn't fill, so the fixed layout stays uniform.
void EmitDegenerateQuad(float* outVerts, uint o, float2 p)
{
    PieceFrame none = NoCaps();
    for (uint q = 0u; q < 6u; ++q) WriteVert(outVerts, o + q, p, 0.0, 1e6, 1e6, none);
}

// Round-join disc at V (radius half, an OPAQUE over-approximation of the corner wedge). Writes triangles at `base`, at
// most `maxTris`, returns how many it wrote.
// A disc spans half a thickness EITHER SIDE of its corner, so its arc distances have to advance across it just like the
// ribbon's do - along the incoming direction on the start side, the outgoing on the end side. Giving the whole disc the
// corner's own arc puts it wholly inside or wholly outside a cap's reach: park the corner just past that reach and the
// half of the disc that lies inside the carved notch is left unmasked, and bulges out of the dash end.
uint EmitDisc(float* outVerts, uint base, uint maxTris, float2 V, float arcA, float arcB,
              float2 dIn, float2 dOut, PieceFrame f)
{
    float hw = HalfThickness + Fringe * 0.5;   // widened radius; perp = radial distance so the rim feathers
    uint n = 0u;
    for (uint k = 0u; k < RoundSegments && n < maxTris; ++k)
    {
        float a0 = 6.28318530717958647692 * float(k) / float(RoundSegments);
        float a1 = 6.28318530717958647692 * float(k + 1u) / float(RoundSegments);
        float2 r0 = hw * float2(cos(a0), sin(a0));
        float2 r1 = hw * float2(cos(a1), sin(a1));
        WriteVert(outVerts, base + n * 3u + 0u, V, 0.0, arcA, arcB, f);
        WriteVert(outVerts, base + n * 3u + 1u, V + r0, hw, arcA + dot(r0, dIn), arcB - dot(r0, dOut), f);
        WriteVert(outVerts, base + n * 3u + 2u, V + r1, hw, arcA + dot(r1, dIn), arcB - dot(r1, dOut), f);
        ++n;
    }
    return n;
}

// StrokeExpandCS - CONTINUOUS mode (the dash/trim cut is a SEPARATE kernel, StrokeDashCutCS; merging both into one
// compute shader reliably tripped the NVIDIA NVVM vkCreateShadersEXT compiler). One thread per POINT: emits the
// outgoing segment's quad PLUS a per-join fan (round disc / bevel wedge) at its point into a fixed per-point slot
// (6 + RoundSegments*3 verts) - a deterministic triangle LIST, no atomics, plain Draw.
//   - MITER joins: a continuous bisector ribbon (constant width, spike clamped); the fan slot stays empty.
//   - BEVEL/ROUND joins: each segment is a full-width rectangle offset by its OWN perpendicular normal (so the stroke
//     never pinches at a corner the way a shared, pulled-back bisector point would), and the fan fills the corner wedge:
//     a disc (round) or two triangles (bevel).
// The piece here is the whole contour: an open one is capped at its two ends, a closed loop has no ends at all.
[shader("compute")]
[numthreads(64, 1, 1)]
void StrokeExpandCS(uint3 tid : SV_DispatchThreadID)
{
    float2* points = (float2*)PointsAddress;
    float* outVerts = (float*)OutputAddress;

    // Continuous polyline, one thread per point. Fixed per-point slot: a segment quad then a join fan.
    if (tid.x >= PointCount)
        return;

    uint i = tid.x;
    uint vpp = 6u + RoundSegments * 3u;
    uint baseV = i * vpp;
    float2 p = points[i];

    bool isStart = (IsClosed == 0) && (i == 0u);
    bool isEnd   = (IsClosed == 0) && (i + 1u == PointCount);
    bool hasSegment = (IsClosed != 0) || (i + 1u < PointCount);

    PieceFrame frame = NoCaps();
    // Arc distance from this point to the contour's two ends - how far a cap can still reach it. Only the first half
    // thickness matters, so the walk stops there and anything past it is simply "far": on a densely tessellated contour
    // one segment is often shorter than the bite, and a walk that stopped on the limit must not be treated as exact or
    // the NEXT point's distance (derived by subtracting a segment) comes out short and its cap bites where no end is.
    float capRange = HalfThickness + Fringe + 1.0;
    float back = 1e6;
    float fwd = 1e6;
    if (IsClosed == 0u)
    {
        uint last = PointCount - 1u;
        frame.A = points[0];
        frame.dA = normalize(points[1] - points[0]);
        frame.B = points[last];
        frame.dB = normalize(points[last] - points[last - 1u]);
        frame.caps = float(StartCap + 8u * EndCap);

        back = 0.0;
        for (uint k = i; k > 0u && back < capRange; --k) back += length(points[k] - points[k - 1u]);
        fwd = 0.0;
        for (uint k2 = i; k2 + 1u < PointCount && fwd < capRange; ++k2) fwd += length(points[k2 + 1u] - points[k2]);
        if (back >= capRange) back = 1e6;
        if (fwd >= capRange) fwd = 1e6;
    }

    // 1) Outgoing segment quad i -> i+1 (degenerate for the open contour's last point, which has no segment).
    if (hasSegment)
    {
        uint ni = (i + 1u) % PointCount;
        float segLen = length(points[ni] - p);
        float nBack = back + segLen;
        float nFwd = fwd - segLen;
        if (JoinType == 0u)
        {
            // MITER: shared bisector offsets keep the width constant into the corner (spike clamped in OffsetPair).
            float2 aP, aM, bP, bM;
            OffsetPair(i, aP, aM);
            OffsetPair(ni, bP, bM);
            EmitQuad(outVerts, baseV, aP, aM, bP, bM, HalfThickness + Fringe * 0.5,
                     back, fwd, nBack, nFwd, frame);
        }
        else
        {
            // BEVEL/ROUND: a full-width rectangle offset by THIS segment's own perpendicular normal. A shared bisector
            // pulled back to half would pinch the stroke thinner at every corner (worst on sharp star tips / gear
            // teeth); a per-segment normal keeps the width exact and the fan below fills the corner.
            float2 a = p;
            float2 b = points[ni];
            float2 dir = normalize(b - a);
            float hw = HalfThickness + Fringe * 0.5;
            float2 nrm = float2(-dir.y, dir.x) * hw;
            if (IsClosed == 0u)
            {
                if (i == 0u) a -= dir * EndExtend(i);              // room for the start cap's mask
                if (ni + 1u == PointCount) b += dir * EndExtend(ni);
            }
            EmitQuad(outVerts, baseV, a + nrm, a - nrm, b + nrm, b - nrm, hw, back, fwd, nBack, nFwd, frame);
        }
    }
    else
    {
        EmitDegenerateQuad(outVerts, baseV, p);
    }

    // 2) Per-point fan slot: the JOIN at an interior (or closed-loop) point - a round disc or a bevel wedge. The two
    //    open ends need nothing here any more: their caps are masks on the quad above. Skipped when RoundSegments==0
    //    (miter joins need no fan geometry at all).
    if (RoundSegments == 0u)
        return;

    uint discBase = baseV + 6u;
    uint nTris = 0u;
    uint prev = (i + PointCount - 1u) % PointCount;
    uint next = (i + 1u) % PointCount;

    float2 dIn = normalize(p - points[prev]);
    float2 dOut = normalize(points[next] - p);
    if (JoinType == 2u && !isStart && !isEnd && JoinIsVisible(dIn, dOut))
    {
        nTris = EmitDisc(outVerts, discBase, RoundSegments, p, back, fwd, dIn, dOut, frame);
    }
    else if (JoinType == 1u && hasSegment && !isStart)
    {
        // Bevel join: fill the wedge between the incoming and outgoing rectangles. Two triangles cover both sides; the
        // inner one lands inside the overlap, harmless for an opaque stroke.
        float hw = HalfThickness + Fringe * 0.5;
        float2 nIn = SegmentNormal(points[prev], p) * hw;
        float2 nOut = SegmentNormal(p, points[next]) * hw;
        WriteVert(outVerts, discBase + 0u, p + nIn, hw, back, fwd, frame);
        WriteVert(outVerts, discBase + 1u, p + nOut, hw, back, fwd, frame);
        WriteVert(outVerts, discBase + 2u, p, 0.0, back, fwd, frame);
        WriteVert(outVerts, discBase + 3u, p - nIn, -hw, back, fwd, frame);
        WriteVert(outVerts, discBase + 4u, p - nOut, -hw, back, fwd, frame);
        WriteVert(outVerts, discBase + 5u, p, 0.0, back, fwd, frame);
        nTris = 2u;
    }

    // Degenerate-fill the unused triangles so the fixed per-point slot stays uniform.
    PieceFrame none = NoCaps();
    for (uint k = nTris; k < RoundSegments; ++k)
    {
        uint o = discBase + k * 3u;
        WriteVert(outVerts, o + 0u, p, 0.0, 1e6, 1e6, none);
        WriteVert(outVerts, o + 1u, p, 0.0, 1e6, 1e6, none);
        WriteVert(outVerts, o + 2u, p, 0.0, 1e6, 1e6, none);
    }
}

// Emits the BEVEL/ROUND join between two dash quads at corner V (the dash crosses the corner): round = a disc, bevel =
// a both-sided corner wedge. nInU/nOutU are the UNIT normals of the incoming / outgoing segments. MITER is NOT emitted
// here - the dash quad ends are mitred onto the shared bisector offset (like StrokeExpandCS), so its corner already
// meets with constant width and needs no wedge.
// The join belongs to the dash PIECE that crosses it and carries that piece's frame, so a corner falling inside a
// concave cap's bite is carved by the same curve as the ribbon instead of poking out of the notch.
uint EmitJoin(float* outVerts, uint vCount, uint maxV, float2 V, float2 dIn, float2 dOut,
              float arcA, float arcB, PieceFrame f)
{
    float h = HalfThickness + Fringe * 0.5;   // widened; the outer corner verts carry perp=+/-h, the centre V carries 0
    float2 nInU = float2(-dIn.y, dIn.x);
    float2 nOutU = float2(-dOut.y, dOut.x);
    if (JoinType == 2u)   // round join: a disc of radius half
    {
        uint tris = EmitDisc(outVerts, vCount, (maxV - vCount) / 3u, V, arcA, arcB, dIn, dOut, f);
        return vCount + tris * 3u;
    }

    // BEVEL wedge, filled on BOTH sides (mirrors StrokeExpandCS's bevel): the outer triangle closes the gap the turn
    // opens, the inner one lands inside the two quads' overlap (harmless for an opaque stroke). Doing both sides avoids
    // ever having to pick - and mis-pick - the outer side. MITER is not routed here (its quad ends already meet).
    if (vCount + 6u <= maxV)
    {
        WriteVert(outVerts, vCount + 0u, V + nInU * h, h, arcA, arcB, f);
        WriteVert(outVerts, vCount + 1u, V + nOutU * h, h, arcA, arcB, f);
        WriteVert(outVerts, vCount + 2u, V, 0.0, arcA, arcB, f);
        WriteVert(outVerts, vCount + 3u, V - nInU * h, -h, arcA, arcB, f);
        WriteVert(outVerts, vCount + 4u, V - nOutU * h, -h, arcA, arcB, f);
        WriteVert(outVerts, vCount + 5u, V, 0.0, arcA, arcB, f);
        vCount += 6u;
    }
    return vCount;
}

// Which cap each end of a piece [pieceStart, pieceEnd] wears, packed as capStart + 8*capEnd. A piece end that IS the
// contour's real end (the trim window's edge) wears Start/EndCap; every other one wears the dash caps. ONE cap per end,
// decided once for the whole piece - deciding it per emitted quad is what used to put a line cap AND a dash cap on the
// same end of the first and last dash.
float PieceCaps(float pieceStart, float pieceEnd, float tStart, float tEnd)
{
    uint capS = (pieceStart <= tStart + 1e-4) ? StartCap : DashStartCap;
    uint capE = (pieceEnd >= tEnd - 1e-4) ? EndCap : DashEndCap;
    return float(capS + 8u * capE);
}

// StrokeDashCutCS - DASH/TRIM mode, a SEPARATE compute kernel (see StrokeExpandCS note re: the NVVM compiler). A
// single thread walks the contour by arc length, applies the trim window and dash pattern, emits a quad per visible
// piece AND a join at every corner the dash crosses, and writes the vertex count into the VkDrawIndirectCommand for
// DrawIndirect.
// Zero per-frame CPU work: dashes/trim/caps are uniforms; the CPU only uploaded the raw contour + the pattern once.
[shader("compute")]
[numthreads(1, 1, 1)]
void StrokeDashCutCS(uint3 tid : SV_DispatchThreadID)
{
    if (tid.x != 0u)
        return;

    float2* points = (float2*)PointsAddress;
    float* outVerts = (float*)OutputAddress;
    float* pattern = (float*)PatternAddress;
    uint* indirect = (uint*)IndirectAddress;
    uint segCount = IsClosed != 0u ? PointCount : PointCount - 1u;

    float total = 0.0;
    for (uint s = 0u; s < segCount; ++s)
        total += length(points[(s + 1u) % PointCount] - points[s]);
    float tStart = TrimStart * total;
    float tEnd = TrimEnd * total;

    float patTotal = 0.0;
    for (uint k = 0u; k < PatternCount; ++k) patTotal += pattern[k];

    uint pi = 0u;
    float rem = PatternCount > 0u ? pattern[0] : 1e30;
    bool on = true;
    if (PatternCount > 0u && patTotal > 0.0)
    {
        float off = DashOffset - floor(DashOffset / patTotal) * patTotal;
        while (off > 1e-6)
        {
            float take = min(off, rem);
            off -= take; rem -= take;
            if (rem <= 1e-6) { pi = (pi + 1u) % PatternCount; rem = pattern[pi]; }
        }
        on = (pi % 2u) == 0u;
    }

    // The ON run currently being drawn, as a PIECE: its two arc-length ends, the frames there and the caps they wear.
    // Everything the piece emits carries this, so the caps mask the piece as ONE shape. The run the walk starts inside
    // began before arc 0 (the offset ate into it), hence the pattern[pi] - rem.
    float pieceStart = -1e30;
    float pieceEnd = 1e30;
    if (PatternCount > 0u && on) { pieceStart = -(pattern[pi] - rem); pieceEnd = rem; }
    // A CLOSED contour with no trim window has no ends at all, so a run that crosses the seam must NOT be clamped to it:
    // the walk still emits it as two pieces (arc runs 0..total), but both keep the run's TRUE arc bounds - one starting
    // before 0, the other ending past total. Their caps then sit outside the walk and the arc gate leaves the seam
    // alone, so the dash crosses it whole. Clamping put a cap on each side of the seam instead: two concave bites
    // face to face, gouging the one corner the arc length happens to start at.
    bool wraps = (IsClosed != 0u) && (TrimStart <= 0.0) && (TrimEnd >= 1.0);
    pieceStart = wraps ? pieceStart : max(pieceStart, tStart);
    pieceEnd = wraps ? pieceEnd : min(pieceEnd, tEnd);
    PieceFrame frame;
    PointAtArc(pieceStart < 0.0 ? pieceStart + total : pieceStart, frame.A, frame.dA);
    PointAtArc(pieceEnd > total ? pieceEnd - total : pieceEnd, frame.B, frame.dB);
    frame.caps = PieceCaps(pieceStart, pieceEnd, tStart, tEnd);

    uint vCount = 0u;
    float arc = 0.0;
    bool contInto = (IsClosed != 0u) && on;   // does a live dash cross the vertex ENTERING the current segment (miter start)
    for (uint s = 0u; s < segCount; ++s)
    {
        float2 a = points[s];
        float2 b = points[(s + 1u) % PointCount];
        float2 d = b - a;
        float segLen = length(d);
        if (segLen < 1e-6) continue;
        float2 dir = d / segLen;
        float hw = HalfThickness + Fringe * 0.5;
        float2 nrm = float2(-dir.y, dir.x) * hw;

        // MITER shares the continuous path's trick: where a dash crosses a polyline vertex the two quads meet on the
        // bisector miter offset (constant width, no gap), NOT perpendicular ends bridged by a wedge - the latter piles
        // stray triangles all over a densely-tessellated curve at large thickness. Precompute the miter offsets at THIS
        // segment's two vertices; used only for the ends that land on a crossed vertex (bevel/round keep perpendicular).
        float2 mSp, mSm, mEp, mEm;
        OffsetPair(s, mSp, mSm);
        OffsetPair((s + 1u) % PointCount, mEp, mEm);

        float pos = 0.0;
        bool lastSpanOn = on;   // on-state of the span ending at this segment's corner (captured BEFORE any end-toggle)
        while (pos < segLen - 1e-6)
        {
            lastSpanOn = on;
            float take = PatternCount > 0u ? min(rem, segLen - pos) : (segLen - pos);
            float drawA = max(arc + pos, tStart);
            float drawB = min(arc + pos + take, tEnd);
            if (on && drawB > drawA + 1e-6)
            {
                float2 p0 = a + (drawA - arc) * dir;
                float2 p1 = a + (drawB - arc) * dir;

                // Only the quad that actually STARTS (ENDS) the piece is stretched, and only for a cap that paints past
                // the end - the mask then carves the bulge/tip out of it. A quad in the middle of a piece (a dash
                // spanning several segments of a curve) is stretched nowhere; its corner is closed by the join below.
                float extA = (drawA <= pieceStart + 1e-4) ? CapExtend(uint(frame.caps) % 8u) + Fringe * 0.5 : 0.0;
                float extB = (drawB >= pieceEnd - 1e-4) ? CapExtend(uint(frame.caps) / 8u) + Fringe * 0.5 : 0.0;
                float2 e0 = p0 - dir * extA;
                float2 e1 = p1 + dir * extB;

                // Arc distance from this quad's two ends to the piece's two ends - the REACH gate the straight cap axis
                // cannot provide. Stretching an end moves it past the piece end, so its own distances move with it.
                float aArcA = (drawA - extA) - pieceStart;
                float aArcB = pieceEnd - (drawA - extA);
                float bArcA = (drawB + extB) - pieceStart;
                float bArcB = pieceEnd - (drawB + extB);

                // For MITER, a quad end that lands on a CROSSED vertex uses the shared bisector offset (no wedge);
                // every other end stays perpendicular. startCont = first piece of a segment a live dash entered; endCont =
                // last piece, dash continues into the next segment (reaches the seg end, doesn't end the dash here).
                bool endsDash = PatternCount > 0u && (rem - take) <= 1e-6;
                bool hasNextSeg = (IsClosed != 0u) || (s + 1u < segCount);
                float arcE = arc + segLen;
                bool startCont = (JoinType == 0u) && (pos == 0.0) && contInto;
                bool endCont = (JoinType == 0u) && !endsDash && (pos + take >= segLen - 1e-6) && hasNextSeg
                               && (arcE > tStart + 1e-4) && (arcE < tEnd - 1e-4);
                float2 sTop = startCont ? mSp : e0 + nrm;
                float2 sBot = startCont ? mSm : e0 - nrm;
                float2 eTop = endCont ? mEp : e1 + nrm;
                float2 eBot = endCont ? mEm : e1 - nrm;
                if (vCount + 6u <= MaxVertices)
                {
                    EmitQuad(outVerts, vCount, sTop, sBot, eTop, eBot, hw, aArcA, aArcB, bArcA, bArcB, frame);
                    vCount += 6u;
                }
            }
            pos += take;
            if (PatternCount > 0u)
            {
                rem -= take;
                if (rem <= 1e-6)
                {
                    pi = (pi + 1u) % PatternCount; rem = pattern[pi]; on = !on;
                    if (on)
                    {
                        // A new dash begins here and its length is already known (rem), so the whole piece - both ends,
                        // both frames - is settled before a single vertex of it is written. That is what lets the far
                        // end's cap mask reach back over everything in between.
                        pieceStart = wraps ? (arc + pos) : max(arc + pos, tStart);
                        pieceEnd = wraps ? (arc + pos + rem) : min(arc + pos + rem, tEnd);
                        PointAtArc(pieceStart, frame.A, frame.dA);
                        PointAtArc(pieceEnd > total ? pieceEnd - total : pieceEnd, frame.B, frame.dB);
                        frame.caps = PieceCaps(pieceStart, pieceEnd, tStart, tEnd);
                    }
                }
            }
        }
        arc += segLen;

        // A dash crosses this segment's END vertex when it is ON on both sides (lastSpanOn && on) and inside the trim.
        // `lastSpanOn && on` requires BOTH sides to be on: if the pattern toggles EXACTLY at the corner, one side is empty
        // and a lone join would jut out as a triangular tab. MITER needs no join here (the mitered quad ends already meet);
        // BEVEL/ROUND add the fan. Either way the flag propagates so the NEXT segment mitres its start off this vertex.
        bool hasNext = (IsClosed != 0u) || (s + 1u < segCount);
        bool joinHere = hasNext && lastSpanOn && on && arc > tStart + 1e-4 && arc < tEnd - 1e-4;
        if (joinHere && JoinType != 0u)
        {
            uint cv = (s + 1u) % PointCount;
            float2 dOut = normalize(points[(s + 2u) % PointCount] - points[cv]);
            if (JoinIsVisible(dir, dOut))
            {
                vCount = EmitJoin(outVerts, vCount, MaxVertices, points[cv], dir, dOut,
                                  arc - pieceStart, pieceEnd - arc, frame);
            }
        }
        contInto = joinHere;
    }

    indirect[0] = vCount;
    indirect[1] = 1u;
    indirect[2] = 0u;
    indirect[3] = 0u;
}

struct VSInput
{
    float2 Position : POSITION;
    float4 Cap0 : TEXCOORD0;   // perp, uA, vA, arcA
    float4 Cap1 : TEXCOORD1;   // caps, uB, vB, arcB
};

struct PSInput
{
    float4 Position : SV_Position;
    float4 Cap0 : TEXCOORD0;
    float4 Cap1 : TEXCOORD1;
};

[shader("vertex")]
PSInput StrokeVS(VSInput input)
{
    PSInput o;
    o.Position = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    o.Cap0 = input.Cap0;
    o.Cap1 = input.Cap1;
    return o;
}

// Signed distance to a cap's boundary. `u` = how far the fragment lies inside the piece along that end's straight axis
// (the SHAPE), `arc` = the same distance measured along the CONTOUR (the REACH), `v` = the fragment's distance across
// the stroke. Positive = painted. The six caps are six boundary curves on the same ribbon - the convex ones reach OUT
// past the end, the concave ones bite IN - and being a distance, each feathers with the ribbon's own AA.
//
// The arc gate is LOAD-BEARING and cannot be replaced by anything the axis knows. A straight axis is an infinite plane,
// and a contour that turns back on itself - the far edge of a thin star spike, a tight U - puts ribbon that is 20px
// away ALONG THE PATH "behind" the cap, which then shaves it into a hair. Nothing more than a cap's own reach along the
// contour can be part of that cap.
// `hBite` is the CONCAVE forms' depth: a cap may not eat past the middle of its own piece, or a dash shorter than a
// thickness is consumed by its two caps and leaves only the slivers at the ribbon's edges. The convex forms keep the
// full half-thickness - their bulge is what makes a zero-length dot render as a circle.
float CapSd(uint cap, float u, float v, float arc, float h, float hBite)
{
    if (cap == 6u) return 1e9;                                    // this end is not an end (interior join, closed loop)
    if (arc > h + Fringe) return 1e9;                             // farther along the contour than this cap can reach
    if (cap == 1u) return u + h;                                  // square: half a thickness of flat nub
    if (cap == 2u) return (u >= 0.0) ? 1e9 : h - length(float2(u, v));        // convex round: half-disc out
    if (cap == 3u) return (u >= 0.0) ? 1e9 : (u + h - abs(v)) * 0.70710678;   // convex triangle: tip out at the centre
    if (cap == 4u) return (u - hBite + abs(v)) * 0.70710678;      // concave triangle: V notch cut in
    if (cap == 5u) return length(float2(max(u, 0.0), v)) - hBite; // concave round: half-disc bitten out
    return u;                                                     // flat
}

[shader("fragment")]
float4 StrokePS(PSInput input) : SV_Target
{
    // Analytic AA: coverage from the signed distance to the nearest boundary - the ribbon's two long edges (|v| vs
    // HalfThickness) and the piece's two caps. 1 in the core, 0.5 on the nominal boundary, 0 a feather past it.
    // Fringe == 0 (analytic AA off) => a hard edge, the mask still shapes the caps.
    // A concave cap may not eat past the middle of its own piece - hBite is that limit.
    uint caps = uint(round(input.Cap1.x));
    float hBite = min(HalfThickness, 0.5 * (input.Cap0.w + input.Cap1.w));
    float sd = HalfThickness - abs(input.Cap0.x);
    sd = min(sd, CapSd(caps % 8u, input.Cap0.y, input.Cap0.z, input.Cap0.w, HalfThickness, hBite));
    sd = min(sd, CapSd(caps / 8u, input.Cap1.y, input.Cap1.z, input.Cap1.w, HalfThickness, hBite));

    float coverage = Fringe > 0.0 ? saturate(sd / Fringe + 0.5) : (sd >= 0.0 ? 1.0 : 0.0);
    float4 c = StrokeColor;
    c.a *= coverage;
    return c;
}

technique Stroke
{
    // Expand (compute): continuous polyline -> miter ribbon + round-join disc fans, written via BDA. Plain Draw.
    pass Expand
    {
        // Slang ignores this (targets spirv_1_6); kept non-zero for the parser + SM 6.6 for the DXC fallback.
        EffectName = "StrokeEffect";
        Profile = 6.6;
        ComputeShader = StrokeExpandCS;
    }

    // DashCut (compute): one-pass dash/trim cut, single thread, writes the VkDrawIndirectCommand. DrawIndirect.
    pass DashCut
    {
        EffectName = "StrokeEffect";
        Profile = 6.6;
        ComputeShader = StrokeDashCutCS;
    }

    // Draw (graphics): rasterize the compute-produced ribbon with a solid colour.
    pass Draw
    {
        EffectName = "StrokeEffect";
        Profile = 6.6;
        VertexShader = StrokeVS;
        PixelShader = StrokePS;
    }
}
