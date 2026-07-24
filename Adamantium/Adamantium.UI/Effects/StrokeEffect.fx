// GPU stroke effect (line-rendering Phase B/C). One compute technique (StrokeExpand) turns a polyline + half-thickness
// into a miter-joined triangle-LIST ribbon written straight into a vertex buffer via a BDA device address, and one
// graphics technique (StrokeDraw) rasterizes it. Both live in a single .fx so the generator emits one Effect class
// (StrokeEffect) - no C# wrapper needed. Shader bodies are Slang.
//
//   output: one thread per SEGMENT emits a quad as 2 triangles (6 vertices) -> segmentCount * 6 vertices.
//   open   => PointCount - 1 segments;  closed => PointCount segments (last wraps back to point 0).
//
// A triangle LIST (not a strip) keeps segments independent, which is the basis for disjoint dashes and variable
// per-join/per-cap fans (round joins/caps) added on top. Open endpoints are flat or square (StartCap/EndCap); interior
// and closed-loop points use the bisector miter, length clamped so a sharp corner can't shoot the tip to infinity.

// --- StrokeExpand (compute) globals ---
uint64_t PointsAddress;   // float2[] polyline points (PointCount of them)
uint64_t OutputAddress;   // float3[] output vertices: (x, y, signed perpendicular distance from centerline) for AA
uint PointCount;          // continuous mode: polyline points. dash mode: piece-points (2 per dash piece).
uint IsClosed;            // 0 = open polyline (flat ends), 1 = closed loop (wrap-around miters + closing pair)
uint StartCap;            // start cap: 0 = flat, 1 = square, 2 = round, 3 = convex-tri, 4 = concave-tri, 5 = concave-round
uint EndCap;              // end cap (same codes)
uint DashCap;             // cap on each DASH/dot piece's ends (cut path); the contour's real ends use Start/EndCap
uint JoinType;            // 0 = miter (bisector ribbon, clamped), 1 = bevel (per-segment rectangles + corner wedge), 2 = round (rectangles + disc fan)
uint RoundSegments;       // disc-fan subdivision for round joins/caps; 0 = no round geometry (no disc slots)
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

// Unit normal of the segment a->b (perp of the normalized direction).
float2 SegmentNormal(float2 a, float2 b)
{
    float2 d = normalize(b - a);
    return float2(-d.y, d.x);
}

// The two offset vertices (centerline +/- miter) at polyline point i, for the MITER join path only (bevel/round build
// full-width per-segment rectangles instead - see StrokeExpandCS). Open endpoints use the single adjacent segment
// normal (flat) or push out by half a thickness (square cap); interior points and every point of a closed loop use
// the bisector miter, length clamped so a sharp corner can't shoot the tip to infinity.
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
        p -= dir * CapShift(StartCap);                    // square extends out, concave insets in (cap shape added in the fan)
        miter = float2(-dir.y, dir.x);
        miterLen = hw;
    }
    else if (IsClosed == 0 && i + 1 == PointCount)
    {
        float2 dir = normalize(points[i] - points[i - 1]);   // outward
        p += dir * CapShift(EndCap);
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

// Writes a quad (two triangles, 6 verts) from four corners into outVerts at vertex offset o.
// hw = widened half-extent (HalfThickness + Fringe/2): the P corners sit at +hw perpendicular, the M corners at -hw, and
// the signed distance v=+/-hw is interpolated across the quad so StrokePS feathers both long edges.
void EmitQuad(float3* outVerts, uint o, float2 aP, float2 aM, float2 bP, float2 bM, float hw)
{
    outVerts[o + 0] = float3(aP,  hw);
    outVerts[o + 1] = float3(aM, -hw);
    outVerts[o + 2] = float3(bP,  hw);
    outVerts[o + 3] = float3(bP,  hw);
    outVerts[o + 4] = float3(aM, -hw);
    outVerts[o + 5] = float3(bM, -hw);
}

// Writes a degenerate (zero-area) quad at o - used for slots a thread doesn't fill, so the fixed layout stays uniform.
void EmitDegenerateQuad(float3* outVerts, uint o, float2 p)
{
    for (uint q = 0u; q < 6u; ++q) outVerts[o + q] = float3(p, 0.0);
}

// How far to move an end's quad cross-section ALONG THE OUTWARD direction, per cap code: square extends out by half;
// the concave caps inset IN by half (so the carved notch/arc has material to cut from); flat / convex caps leave the
// cross-section on the geometry point (their shape is added beyond it by EmitCapTris).
float CapShift(uint cap)
{
    if (cap == 1u) return HalfThickness;                  // square
    if (cap == 4u || cap == 5u) return -HalfThickness;    // concave triangle / concave round
    return 0.0;                                           // flat, convex round, convex triangle
}

// Emits an end cap as real geometry for cap code (2 = convex round disc, 3 = convex triangle, 4 = concave triangle,
// 5 = concave round); flat(0)/square(1) add nothing (CapShift already shaped the quad). Writes triangles at `base`, at
// most `maxTris`, returns how many it wrote. `p` = geometry endpoint, `o` = outward unit dir, `perp` = half-thickness
// normal (p +/- perp are the stroke corners there). Convex caps add a bulge/tip beyond p; concave caps fill the two
// lobes left between the inset quad (base centre p - o*half) and the corners, carving the notch/arc between them.
uint EmitCapTris(float3* outVerts, uint base, uint maxTris, float2 p, float2 o, float2 perp, uint cap)
{
    const float TWO_PI = 6.28318530717958647692;
    const float PI_ = 3.14159265358979323846;
    uint n = 0u;
    float hw = HalfThickness + Fringe * 0.5;   // widened radius; v = radial distance so the disc rim feathers

    if (cap == 2u)   // convex round: full disc (opaque; the outer half is the visible cap)
    {
        for (uint k = 0u; k < RoundSegments && n < maxTris; ++k)
        {
            float a0 = TWO_PI * float(k) / float(RoundSegments);
            float a1 = TWO_PI * float(k + 1u) / float(RoundSegments);
            outVerts[base + n * 3u + 0] = float3(p, 0.0);
            outVerts[base + n * 3u + 1] = float3(p + hw * float2(cos(a0), sin(a0)), hw);
            outVerts[base + n * 3u + 2] = float3(p + hw * float2(cos(a1), sin(a1)), hw);
            ++n;
        }
    }
    else if (cap == 3u && maxTris >= 1u)   // convex triangle: a tip out at p + o*half
    {
        outVerts[base + 0] = float3(p + perp, 0.0);
        outVerts[base + 1] = float3(p - perp, 0.0);
        outVerts[base + 2] = float3(p + o * HalfThickness, 0.0);
        n = 1u;
    }
    else if (cap == 4u && maxTris >= 2u)   // concave triangle: two lobes back to the inset base centre (a V notch)
    {
        float2 bc = p - o * HalfThickness;
        outVerts[base + 0] = float3(bc + perp, 0.0); outVerts[base + 1] = float3(p + perp, 0.0); outVerts[base + 2] = float3(bc, 0.0);
        outVerts[base + 3] = float3(bc - perp, 0.0); outVerts[base + 4] = float3(p - perp, 0.0); outVerts[base + 5] = float3(bc, 0.0);
        n = 2u;
    }
    else if (cap == 5u)   // concave round: two lobe fans following the inward arc (radius half, centred on p)
    {
        float2 bc = p - o * HalfThickness;
        float aC = atan2(perp.y, perp.x);          // +perp corner direction (on the circle)
        float aB = atan2(-o.y, -o.x);              // base-centre direction (90 deg from aC)
        uint lobeSegs = RoundSegments / 2u;

        float dTop = aB - aC; if (dTop > PI_) dTop -= TWO_PI; if (dTop < -PI_) dTop += TWO_PI;
        for (uint k = 0u; k < lobeSegs && n < maxTris; ++k)
        {
            float th0 = aC + dTop * (float(k) / float(lobeSegs));
            float th1 = aC + dTop * (float(k + 1u) / float(lobeSegs));
            outVerts[base + n * 3u + 0] = float3(bc + perp, 0.0);
            outVerts[base + n * 3u + 1] = float3(p + HalfThickness * float2(cos(th0), sin(th0)), 0.0);
            outVerts[base + n * 3u + 2] = float3(p + HalfThickness * float2(cos(th1), sin(th1)), 0.0);
            ++n;
        }
        float aCm = aC + PI_; if (aCm > PI_) aCm -= TWO_PI;
        float dBot = aCm - aB; if (dBot > PI_) dBot -= TWO_PI; if (dBot < -PI_) dBot += TWO_PI;
        for (uint k = 0u; k < lobeSegs && n < maxTris; ++k)
        {
            float th0 = aB + dBot * (float(k) / float(lobeSegs));
            float th1 = aB + dBot * (float(k + 1u) / float(lobeSegs));
            outVerts[base + n * 3u + 0] = float3(bc - perp, 0.0);
            outVerts[base + n * 3u + 1] = float3(p + HalfThickness * float2(cos(th0), sin(th0)), 0.0);
            outVerts[base + n * 3u + 2] = float3(p + HalfThickness * float2(cos(th1), sin(th1)), 0.0);
            ++n;
        }
    }
    return n;
}

// StrokeExpandCS - CONTINUOUS mode (the dash/trim cut is a SEPARATE kernel, StrokeDashCutCS; merging both into one
// compute shader reliably tripped the NVIDIA NVVM vkCreateShadersEXT compiler). One thread per POINT: emits the
// outgoing segment's quad PLUS a per-join fan (round disc / bevel wedge) at its point into a fixed per-point slot
// (6 + RoundSegments*3 verts) - a deterministic triangle LIST, no atomics, plain Draw.
//   - MITER joins: a continuous bisector ribbon (constant width, spike clamped); the fan slot only ever holds round CAPS.
//   - BEVEL/ROUND joins: each segment is a full-width rectangle offset by its OWN perpendicular normal (so the stroke
//     never pinches at a corner the way a shared, pulled-back bisector point would), and the fan fills the corner wedge:
//     a disc (round, radius=half - an OPAQUE over-approximation; proper outer-arc/transparency comes with the AA pass)
//     or two triangles (bevel).
[shader("compute")]
[numthreads(64, 1, 1)]
void StrokeExpandCS(uint3 tid : SV_DispatchThreadID)
{
    float2* points = (float2*)PointsAddress;
    float3* outVerts = (float3*)OutputAddress;

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

    // 1) Outgoing segment quad i -> i+1 (degenerate for the open contour's last point, which has no segment).
    if (hasSegment)
    {
        uint ni = (i + 1u) % PointCount;
        if (JoinType == 0u)
        {
            // MITER: shared bisector offsets keep the width constant into the corner (spike clamped in OffsetPair).
            float2 aP, aM, bP, bM;
            OffsetPair(i, aP, aM);
            OffsetPair(ni, bP, bM);
            EmitQuad(outVerts, baseV, aP, aM, bP, bM, HalfThickness + Fringe * 0.5);
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
                if (i == 0u) a -= dir * CapShift(StartCap);              // start cap: square extends, concave insets
                if (ni + 1u == PointCount) b += dir * CapShift(EndCap);  // end cap
            }
            EmitQuad(outVerts, baseV, a + nrm, a - nrm, b + nrm, b - nrm, hw);
        }
    }
    else
    {
        EmitDegenerateQuad(outVerts, baseV, p);
    }

    // 2) Per-point fan slot: an end CAP (open endpoints) or a JOIN (round disc / bevel wedge at interior & closed
    //    points). Skipped when RoundSegments==0 (miter joins with flat/square caps need no fan geometry).
    if (RoundSegments == 0u)
        return;

    uint discBase = baseV + 6u;
    uint nTris = 0u;

    if ((IsClosed == 0u) && (isStart || isEnd))
    {
        // Open endpoint -> the cap. Outward dir points away from the stroke; perp is the end's half-thickness normal.
        uint cap = isStart ? StartCap : EndCap;
        float2 o = isStart ? normalize(points[0] - points[1]) : normalize(points[i] - points[i - 1u]);
        float2 perp = float2(-o.y, o.x) * HalfThickness;
        nTris = EmitCapTris(outVerts, discBase, RoundSegments, p, o, perp, cap);
    }
    else if (JoinType == 2u)
    {
        // Round join -> the same disc as a round cap (o/perp are unused for a full disc).
        nTris = EmitCapTris(outVerts, discBase, RoundSegments, p, float2(1.0, 0.0), float2(0.0, 0.0), 2u);
    }
    else if (JoinType == 1u && hasSegment)
    {
        // Bevel join: fill the wedge between the incoming and outgoing rectangles. Two triangles cover both sides; the
        // inner one lands inside the overlap, harmless for an opaque stroke.
        uint prev = (i + PointCount - 1u) % PointCount;
        uint next = (i + 1u) % PointCount;
        float hw = HalfThickness + Fringe * 0.5;
        float2 nIn = SegmentNormal(points[prev], p) * hw;
        float2 nOut = SegmentNormal(p, points[next]) * hw;
        outVerts[discBase + 0] = float3(p + nIn, hw); outVerts[discBase + 1] = float3(p + nOut, hw); outVerts[discBase + 2] = float3(p, 0.0);
        outVerts[discBase + 3] = float3(p - nIn, -hw); outVerts[discBase + 4] = float3(p - nOut, -hw); outVerts[discBase + 5] = float3(p, 0.0);
        nTris = 2u;
    }

    // Degenerate-fill the unused triangles so the fixed per-point slot stays uniform.
    for (uint k = nTris; k < RoundSegments; ++k)
    {
        uint o = discBase + k * 3u;
        outVerts[o + 0] = float3(p, 0.0);
        outVerts[o + 1] = float3(p, 0.0);
        outVerts[o + 2] = float3(p, 0.0);
    }
}

// Emits the BEVEL/ROUND join between two dash quads at corner V (the dash crosses the corner): round = a disc, bevel =
// a both-sided corner wedge. nInU/nOutU are the UNIT normals of the incoming / outgoing segments. MITER is NOT emitted
// here - the dash quad ends are mitred onto the shared bisector offset (like StrokeExpandCS), so its corner already
// meets with constant width and needs no wedge; only bevel/round route through this function.
uint EmitJoin(float3* outVerts, uint vCount, uint maxV, float2 V, float2 nInU, float2 nOutU)
{
    float h = HalfThickness + Fringe * 0.5;   // widened; the outer corner verts carry v=+/-h, the centre V carries v=0
    if (JoinType == 2u)   // round join: a disc of radius half (same as a round cap; o/perp unused for a full disc)
    {
        uint tris = EmitCapTris(outVerts, vCount, (maxV - vCount) / 3u, V, float2(1.0, 0.0), float2(0.0, 0.0), 2u);
        return vCount + tris * 3u;
    }

    // BEVEL wedge, filled on BOTH sides (mirrors StrokeExpandCS's bevel): the outer triangle closes the gap the turn
    // opens, the inner one lands inside the two quads' overlap (harmless for an opaque stroke). Doing both sides avoids
    // ever having to pick - and mis-pick - the outer side. MITER is not routed here (its quad ends already meet).
    if (vCount + 6u <= maxV)
    {
        outVerts[vCount + 0] = float3(V + nInU * h,  h); outVerts[vCount + 1] = float3(V + nOutU * h,  h); outVerts[vCount + 2] = float3(V, 0.0);
        outVerts[vCount + 3] = float3(V - nInU * h, -h); outVerts[vCount + 4] = float3(V - nOutU * h, -h); outVerts[vCount + 5] = float3(V, 0.0);
        vCount += 6u;
    }
    return vCount;
}

// StrokeDashCutCS - DASH/TRIM mode, a SEPARATE compute kernel (see StrokeExpandCS note re: the NVVM compiler). A
// single thread walks the contour by arc length, applies the trim window and dash pattern, emits a capped quad (plus
// round-cap discs) per visible piece AND a join at every corner the dash crosses, and writes the vertex count into the
// VkDrawIndirectCommand for DrawIndirect.
// Zero per-frame CPU work: dashes/trim/caps are uniforms; the CPU only uploaded the raw contour + the pattern once.
// Caps: each dash piece's start-facing end uses StartCap and its end-facing end uses EndCap, so both StartLineCap and
// EndLineCap show on every dash (the trim-only single piece is just one such piece).
[shader("compute")]
[numthreads(1, 1, 1)]
void StrokeDashCutCS(uint3 tid : SV_DispatchThreadID)
{
    if (tid.x != 0u)
        return;

    float2* points = (float2*)PointsAddress;
    float3* outVerts = (float3*)OutputAddress;
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

    uint vCount = 0u;
    float arc = 0.0;
    bool dashStartPending = false;   // set when the pattern toggles ON: the next visible piece STARTS a dash (gets a cap)
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
                // Per-end cap: the contour's REAL ends (the first piece's start at tStart, the last piece's end at tEnd)
                // use StartCap/EndCap; every other (dash-internal) piece end uses DashCap - so dots/dashes are capped by
                // DashCap (round -> round dots) while the stroke's actual ends keep Start/EndLineCap. CapShift shapes the
                // quad (square out / concave inset); EmitCapTris adds the disc/triangle/concave geometry.
                // A cap belongs ONLY at a REAL dash boundary. A polyline/curve is a chain of segments and a dash routinely
                // spans several of them (each star edge is one segment; a curve is many); at those internal segment
                // boundaries the dash CONTINUES, so it must stay FLAT (0) - the corner is closed by the join below. Capping
                // every segment boundary sprouted a stray DashCap (convex tri/disc) on BOTH sides of every corner => the
                // "extra angles sticking out". endsDash = this piece eats the rest of the ON element (real dash end);
                // dashStartPending = the pattern just toggled ON (real dash start). Only those two get DashCap.
                bool endsDash = PatternCount > 0u && (rem - take) <= 1e-6;
                uint capA = (drawA <= tStart + 1e-4) ? StartCap : (dashStartPending ? DashCap : 0u);
                uint capB = (drawB >= tEnd - 1e-4) ? EndCap : (endsDash ? DashCap : 0u);
                dashStartPending = false;
                float2 e0 = p0 - dir * CapShift(capA);
                float2 e1 = p1 + dir * CapShift(capB);

                // For MITER, a quad end that lands on a CROSSED vertex uses the shared bisector offset (no cap, no wedge);
                // every other end stays perpendicular. startCont = first piece of a segment a live dash entered; endCont =
                // last piece, dash continues into the next segment (reaches the seg end, doesn't end the dash here).
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
                    EmitQuad(outVerts, vCount, sTop, sBot, eTop, eBot, hw);
                    vCount += 6u;
                }
                if (RoundSegments > 0u)
                {
                    vCount += EmitCapTris(outVerts, vCount, (MaxVertices - vCount) / 3u, p0, -dir, nrm, capA) * 3u;
                    vCount += EmitCapTris(outVerts, vCount, (MaxVertices - vCount) / 3u, p1,  dir, nrm, capB) * 3u;
                }
            }
            pos += take;
            if (PatternCount > 0u)
            {
                rem -= take;
                if (rem <= 1e-6) { pi = (pi + 1u) % PatternCount; rem = pattern[pi]; on = !on; if (on) dashStartPending = true; }
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
            vCount = EmitJoin(outVerts, vCount, MaxVertices, points[cv], float2(-dir.y, dir.x), float2(-dOut.y, dOut.x));
        }
        contInto = joinHere;
    }

    indirect[0] = vCount;
    indirect[1] = 1u;
    indirect[2] = 0u;
    indirect[3] = 0u;
}

struct VSInput { float2 Position : POSITION; float Distance : TEXCOORD0; };
struct PSInput { float4 Position : SV_Position; float Distance : TEXCOORD0; };

[shader("vertex")]
PSInput StrokeVS(VSInput input)
{
    PSInput o;
    o.Position = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    o.Distance = input.Distance;
    return o;
}

[shader("fragment")]
float4 StrokePS(PSInput input) : SV_Target
{
    // Analytic AA: coverage from the interpolated signed perpendicular distance - 1 in the core, 0.5 at the nominal edge
    // (|d| = HalfThickness), 0 at the widened edge (|d| = HalfThickness + Fringe/2). Feathers both ribbon edges + discs.
    // Fringe == 0 (analytic AA off) => flat hard ribbon (no feather, no divide-by-zero).
    float coverage = Fringe > 0.0 ? saturate((HalfThickness + Fringe * 0.5 - abs(input.Distance)) / Fringe) : 1.0;
    float4 c = StrokeColor;
    c.a *= coverage;
    return c;
}

technique Stroke
{
    // Expand (compute): continuous polyline -> miter ribbon + round-join/cap disc fans, written via BDA. Plain Draw.
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
