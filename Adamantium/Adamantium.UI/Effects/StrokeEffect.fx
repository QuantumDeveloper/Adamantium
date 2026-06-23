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
uint64_t OutputAddress;   // float2[] output vertices
uint PointCount;          // continuous mode: polyline points. dash mode: piece-points (2 per dash piece).
uint IsClosed;            // 0 = open polyline (flat ends), 1 = closed loop (wrap-around miters + closing pair)
uint StartCap;            // start cap: 0 = flat, 1 = square, 2 = round, 3 = convex-tri, 4 = concave-tri, 5 = concave-round
uint EndCap;              // end cap (same codes)
uint DashCap;             // cap on each DASH/dot piece's ends (cut path); the contour's real ends use Start/EndCap
uint JoinType;            // 0 = miter (bisector ribbon, clamped), 1 = bevel (per-segment rectangles + corner wedge), 2 = round (rectangles + disc fan)
uint RoundSegments;       // disc-fan subdivision for round joins/caps; 0 = no round geometry (no disc slots)
uint DashMode;            // 0 = continuous polyline (per-point), 1 = one-pass GPU cut (dash/trim, single-thread + DrawIndirect)
float HalfThickness;

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

    float2 miter;
    float miterLen;

    if (IsClosed == 0 && i == 0)
    {
        float2 dir = normalize(points[1] - points[0]);   // inward; outward is -dir
        p -= dir * CapShift(StartCap);                    // square extends out, concave insets in (cap shape added in the fan)
        miter = float2(-dir.y, dir.x);
        miterLen = HalfThickness;
    }
    else if (IsClosed == 0 && i + 1 == PointCount)
    {
        float2 dir = normalize(points[i] - points[i - 1]);   // outward
        p += dir * CapShift(EndCap);
        miter = float2(-dir.y, dir.x);
        miterLen = HalfThickness;
    }
    else
    {
        uint prev = (i + PointCount - 1) % PointCount;
        uint next = (i + 1) % PointCount;
        float2 n0 = SegmentNormal(points[prev], p);
        float2 n1 = SegmentNormal(p, points[next]);
        miter = normalize(n0 + n1);
        float denom = max(dot(miter, n0), 0.25);       // clamp -> miter length capped at 4*half on sharp corners
        miterLen = HalfThickness / denom;
    }

    plus = p + miter * miterLen;
    minus = p - miter * miterLen;
}

// Writes a quad (two triangles, 6 verts) from four corners into outVerts at vertex offset o.
void EmitQuad(float2* outVerts, uint o, float2 aP, float2 aM, float2 bP, float2 bM)
{
    outVerts[o + 0] = aP;
    outVerts[o + 1] = aM;
    outVerts[o + 2] = bP;
    outVerts[o + 3] = bP;
    outVerts[o + 4] = aM;
    outVerts[o + 5] = bM;
}

// Writes a degenerate (zero-area) quad at o - used for slots a thread doesn't fill, so the fixed layout stays uniform.
void EmitDegenerateQuad(float2* outVerts, uint o, float2 p)
{
    for (uint q = 0u; q < 6u; ++q) outVerts[o + q] = p;
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
uint EmitCapTris(float2* outVerts, uint base, uint maxTris, float2 p, float2 o, float2 perp, uint cap)
{
    const float TWO_PI = 6.28318530717958647692;
    const float PI_ = 3.14159265358979323846;
    uint n = 0u;

    if (cap == 2u)   // convex round: full disc (opaque; the outer half is the visible cap)
    {
        for (uint k = 0u; k < RoundSegments && n < maxTris; ++k)
        {
            float a0 = TWO_PI * float(k) / float(RoundSegments);
            float a1 = TWO_PI * float(k + 1u) / float(RoundSegments);
            outVerts[base + n * 3u + 0] = p;
            outVerts[base + n * 3u + 1] = p + HalfThickness * float2(cos(a0), sin(a0));
            outVerts[base + n * 3u + 2] = p + HalfThickness * float2(cos(a1), sin(a1));
            ++n;
        }
    }
    else if (cap == 3u && maxTris >= 1u)   // convex triangle: a tip out at p + o*half
    {
        outVerts[base + 0] = p + perp;
        outVerts[base + 1] = p - perp;
        outVerts[base + 2] = p + o * HalfThickness;
        n = 1u;
    }
    else if (cap == 4u && maxTris >= 2u)   // concave triangle: two lobes back to the inset base centre (a V notch)
    {
        float2 bc = p - o * HalfThickness;
        outVerts[base + 0] = bc + perp; outVerts[base + 1] = p + perp; outVerts[base + 2] = bc;
        outVerts[base + 3] = bc - perp; outVerts[base + 4] = p - perp; outVerts[base + 5] = bc;
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
            outVerts[base + n * 3u + 0] = bc + perp;
            outVerts[base + n * 3u + 1] = p + HalfThickness * float2(cos(th0), sin(th0));
            outVerts[base + n * 3u + 2] = p + HalfThickness * float2(cos(th1), sin(th1));
            ++n;
        }
        float aCm = aC + PI_; if (aCm > PI_) aCm -= TWO_PI;
        float dBot = aCm - aB; if (dBot > PI_) dBot -= TWO_PI; if (dBot < -PI_) dBot += TWO_PI;
        for (uint k = 0u; k < lobeSegs && n < maxTris; ++k)
        {
            float th0 = aB + dBot * (float(k) / float(lobeSegs));
            float th1 = aB + dBot * (float(k + 1u) / float(lobeSegs));
            outVerts[base + n * 3u + 0] = bc - perp;
            outVerts[base + n * 3u + 1] = p + HalfThickness * float2(cos(th0), sin(th0));
            outVerts[base + n * 3u + 2] = p + HalfThickness * float2(cos(th1), sin(th1));
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
    float2* outVerts = (float2*)OutputAddress;

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
            EmitQuad(outVerts, baseV, aP, aM, bP, bM);
        }
        else
        {
            // BEVEL/ROUND: a full-width rectangle offset by THIS segment's own perpendicular normal. A shared bisector
            // pulled back to half would pinch the stroke thinner at every corner (worst on sharp star tips / gear
            // teeth); a per-segment normal keeps the width exact and the fan below fills the corner.
            float2 a = p;
            float2 b = points[ni];
            float2 dir = normalize(b - a);
            float2 nrm = float2(-dir.y, dir.x) * HalfThickness;
            if (IsClosed == 0u)
            {
                if (i == 0u) a -= dir * CapShift(StartCap);              // start cap: square extends, concave insets
                if (ni + 1u == PointCount) b += dir * CapShift(EndCap);  // end cap
            }
            EmitQuad(outVerts, baseV, a + nrm, a - nrm, b + nrm, b - nrm);
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
        float2 nIn = SegmentNormal(points[prev], p) * HalfThickness;
        float2 nOut = SegmentNormal(p, points[next]) * HalfThickness;
        outVerts[discBase + 0] = p + nIn; outVerts[discBase + 1] = p + nOut; outVerts[discBase + 2] = p;
        outVerts[discBase + 3] = p - nIn; outVerts[discBase + 4] = p - nOut; outVerts[discBase + 5] = p;
        nTris = 2u;
    }

    // Degenerate-fill the unused triangles so the fixed per-point slot stays uniform.
    for (uint k = nTris; k < RoundSegments; ++k)
    {
        uint o = discBase + k * 3u;
        outVerts[o + 0] = p;
        outVerts[o + 1] = p;
        outVerts[o + 2] = p;
    }
}

// Emits the JOIN between two straight dash quads at corner V (the dash crosses the corner): round = a disc, bevel = two
// corner triangles, miter = those plus an outer wedge to the miter tip. nInU/nOutU are the UNIT normals of the incoming
// / outgoing segments. Both corner triangles are emitted (one fills the convex gap, the other the concave gap; the
// non-gap side is harmless overlap), so no per-corner left/right test is needed. Returns the advanced vertex count.
uint EmitJoin(float2* outVerts, uint vCount, uint maxV, float2 V, float2 nInU, float2 nOutU)
{
    float h = HalfThickness;
    if (JoinType == 2u)   // round join: a disc of radius half (same as a round cap; o/perp unused for a full disc)
    {
        uint tris = EmitCapTris(outVerts, vCount, (maxV - vCount) / 3u, V, float2(1.0, 0.0), float2(0.0, 0.0), 2u);
        return vCount + tris * 3u;
    }
    if (vCount + 6u <= maxV)   // bevel/miter: fill both corner sides
    {
        outVerts[vCount + 0] = V + nInU * h; outVerts[vCount + 1] = V + nOutU * h; outVerts[vCount + 2] = V;
        outVerts[vCount + 3] = V - nInU * h; outVerts[vCount + 4] = V - nOutU * h; outVerts[vCount + 5] = V;
        vCount += 6u;
    }
    if (JoinType == 0u && vCount + 3u <= maxV)   // miter: extend the outer wedge to the (clamped) miter tip
    {
        float2 bis = normalize(nInU + nOutU);
        float dd = max(dot(bis, nInU), 0.25);
        float2 tip = V + bis * (h / dd);
        outVerts[vCount + 0] = V + nInU * h; outVerts[vCount + 1] = tip; outVerts[vCount + 2] = V + nOutU * h;
        vCount += 3u;
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
    float2* outVerts = (float2*)OutputAddress;
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
    for (uint s = 0u; s < segCount; ++s)
    {
        float2 a = points[s];
        float2 b = points[(s + 1u) % PointCount];
        float2 d = b - a;
        float segLen = length(d);
        if (segLen < 1e-6) continue;
        float2 dir = d / segLen;
        float2 nrm = float2(-dir.y, dir.x) * HalfThickness;

        float pos = 0.0;
        while (pos < segLen - 1e-6)
        {
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
                uint capA = (drawA <= tStart + 1e-4) ? StartCap : DashCap;
                uint capB = (drawB >= tEnd - 1e-4) ? EndCap : DashCap;
                float2 e0 = p0 - dir * CapShift(capA);
                float2 e1 = p1 + dir * CapShift(capB);
                if (vCount + 6u <= MaxVertices)
                {
                    EmitQuad(outVerts, vCount, e0 + nrm, e0 - nrm, e1 + nrm, e1 - nrm);
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
                if (rem <= 1e-6) { pi = (pi + 1u) % PatternCount; rem = pattern[pi]; on = !on; }
            }
        }
        arc += segLen;

        // Join at the corner that ENDS this segment (points[s+1]) when the dash is ON across it and the corner is
        // inside the trim window. Without this the per-segment dash quads meet a corner with no fill - the gaps on the
        // teeth of a dashed polygon. Skipped at an open contour's final point (no following segment -> it's a cap).
        bool hasNext = (IsClosed != 0u) || (s + 1u < segCount);
        if (hasNext && on && arc > tStart + 1e-4 && arc < tEnd - 1e-4)
        {
            uint cv = (s + 1u) % PointCount;
            float2 dOut = normalize(points[(s + 2u) % PointCount] - points[cv]);
            vCount = EmitJoin(outVerts, vCount, MaxVertices, points[cv], float2(-dir.y, dir.x), float2(-dOut.y, dOut.x));
        }
    }

    indirect[0] = vCount;
    indirect[1] = 1u;
    indirect[2] = 0u;
    indirect[3] = 0u;
}

struct VSInput { float2 Position : POSITION; };
struct PSInput { float4 Position : SV_Position; };

[shader("vertex")]
PSInput StrokeVS(VSInput input)
{
    PSInput o;
    o.Position = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    return o;
}

[shader("fragment")]
float4 StrokePS(PSInput input) : SV_Target
{
    return StrokeColor;
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
