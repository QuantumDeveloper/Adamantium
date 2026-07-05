// Item-background batch (docs/TEXT_GLYPH_BATCH_PLAN.md - the "подложки" instancing). Draws MANY solid rounded-rect
// fills (ItemsControl item backgrounds, and any solid rounded-rect fill) in ONE instanced draw: each fill is one
// per-instance RectItem, expanded to a quad in the vertex stage (corner from SV_VertexID), and the pixel shader
// reconstructs the rounded-rect coverage ANALYTICALLY from a signed-distance field - self-anti-aliasing, so there is
// no separate AA fringe unit per fill. Positions are baked to WORLD space on the CPU during aggregation; the vertex
// shader applies only a single static Projection (the one driver-safe form on this Turing - no per-instance matrix).
// Slang bodies. Row-vector convention (matches the engine's other effects).
//
// This one effect now holds the whole retained batch/instancing family as separate PASSES: RectBatch (the SDF rounded-
// rect instancing above) and InstancedFill (general geometry instancing - a SHARED local mesh drawn N times, per-instance
// world transform + colour fetched from a StructuredBuffer by SV_InstanceID; docs/RENDER_CACHE_REDESIGN.md §4h/§4j).

#include "Effects/CommonData.fxh"   // UI_VERTEX (the shared mesh's per-vertex layout)

float4x4 Projection;

// ---- Shared SDF fill+stroke compositing --------------------------------------------------------------------------
// Both SDF families (rounded-rect, ellipse) share the same stroke story: given the signed distance `d` to the contour
// (device-px, negative inside), a fill and an OPTIONAL stroke are composited in ONE pass. The stroke is a ring built
// analytically from the same `d` (no geometry): a solid stroke is `abs(d - align*halfW) - halfW`, and dashes/trim
// modulate it via `strokeMask` (0..1, computed by the caller from arc length). Output is STRAIGHT alpha (drawn with a
// straight AlphaBlend, like the solid fills) - so two straight layers (fill under stroke) are composited to one.
//
// stroke0 = (width_px, align[-1 inside/0 center/+1 outside], dashOn, dashGap); stroke1 = (dashOffset, trimStart, trimEnd, flags).
float4 CompositeFillStroke(float d, float4 fill, float4 stroke, float width, float align, float strokeMask)
{
    float aa = max(fwidth(d), 1e-5);
    float covFill = saturate(0.5 - d / aa);

    float halfW = width * 0.5;
    float dRing = abs(d - align * halfW) - halfW;            // signed distance to the stroke ring
    float covStroke = (width > 0.0) ? saturate(0.5 - dRing / aa) * saturate(strokeMask) : 0.0;

    float fa = fill.a * covFill;                             // fill effective (straight) alpha
    float sa = stroke.a * covStroke;                         // stroke effective (straight) alpha
    float outA = sa + fa * (1.0 - sa);                       // stroke OVER fill, straight compositing
    float3 outRGB = (outA > 1e-6) ? (stroke.rgb * sa + fill.rgb * fa * (1.0 - sa)) / outA : float3(0.0, 0.0, 0.0);
    return float4(outRGB, outA);
}

// Cap "reach" along the contour, at a fragment whose PERPENDICULAR distance from the stroke centreline is dPerp: how far
// past a piece end the stroke still paints (SIGNED - negative reach cuts the end INWARD, giving the concave caps). All six
// PenLineCaps analytically, no cap geometry, matching the geometry stroker's codes (EmitCapTris): 0 flat, 1 square,
// 2 convex round, 3 convex triangle, 4 concave triangle, 5 concave round. Convex/concave forms are mirror images (+/-).
float CapReach(int cap, float dPerp, float halfW)
{
    if (cap == 1) return halfW;                                            // square: rectangular nub
    if (cap == 2) return sqrt(max(halfW * halfW - dPerp * dPerp, 0.0));    // convex round: semicircle out
    if (cap == 3) return halfW - abs(dPerp);                              // convex triangle: tip out at the centre
    if (cap == 4) return abs(dPerp) - halfW;                              // concave triangle: V notch cut in
    if (cap == 5) return -sqrt(max(halfW * halfW - dPerp * dPerp, 0.0));   // concave round: semicircular bite in
    return 0.0;                                                            // flat: hard cut
}

// Dash + trim coverage (0..1) at arc-length `s` (device px) along a contour of length `perimeter`. Makes dashes/trim
// ANALYTIC (per-fragment, no cut geometry) so a dashed/trimmed stroke still BATCHES. dashOn<=0 = solid. Piece/trim ends
// are shaped by their caps (packed base-8 into capFlags: dash + 8*start + 64*end; 0 flat, 1 square, 2 round). dPerp =
// signed perpendicular distance from the stroke centreline; halfW = half the stroke width. ~1px AA everywhere.
// sTrim cuts the trim window; sDash phases the dashes. Both are the fragment's continuous centreline arc-length (callers
// pass the same value) - corners included, since the corner arc-length is angle-based and thus uniform across the width.
float DashTrimMask(float sTrim, float sDash, float perimeter, float dashOn, float dashGap, float dashOffset,
    float trimStart, float trimEnd, float dPerp, float halfW, float capFlags)
{
    int dashCap  = int(fmod(capFlags, 8.0));
    int startCap = int(fmod(floor(capFlags / 8.0), 8.0));
    int endCap   = int(fmod(floor(capFlags / 64.0), 8.0));   // base-8 mask: else the JOIN in the 512s place leaks in

    float mask = 1.0;
    if (trimStart > 0.0 || trimEnd < 1.0)              // keep only s in [trimStart, trimEnd] * perimeter; ends get caps
    {
        float a = trimStart * perimeter;
        float b = trimEnd * perimeter;
        mask *= saturate((sTrim - a) + CapReach(startCap, dPerp, halfW) + 0.5)
              * saturate((b - sTrim) + CapReach(endCap, dPerp, halfW) + 0.5);
    }
    float period = dashOn + dashGap;
    if (dashOn > 0.0 && period > 0.0)                  // ON for the first dashOn of each (dashOn+dashGap) period
    {
        float ph = frac((sDash + dashOffset) / period) * period;   // 0..period
        // Signed distance to the nearest ON edge (positive inside the run), wrapping so BOTH ends of every gap get a cap.
        float dEdge = (ph <= dashOn) ? min(ph, dashOn - ph) : -min(ph - dashOn, period - ph);
        mask *= saturate(dEdge + CapReach(dashCap, dPerp, halfW) + 0.5);
    }
    return mask;
}

// Arc-length `s` (device px) of the point on the ROUNDED-RECT contour nearest `p`, and the perimeter. Exact/closed-form.
// Traversal CCW from the start of the top-right arc: TR arc, top edge, TL arc, left edge, BL arc, bottom edge, BR arc,
// right edge. (Start point is arbitrary for dashes; dashOffset shifts the phase.)
float RoundRectArc(float2 p, float2 b, float r, out float perimeter)
{
    // Dashes are measured on the CENTRELINE. In the corner, s = corner-start + phi*r uses the fragment's ANGLE (phi),
    // not its radius, so it's already uniform across the stroke width AND continuous with the edges - the dash flows
    // around the corner exactly like on a straight edge (and like the ellipse). No per-corner "single state" is needed.
    float ex = 2.0 * (b.x - r);
    float ey = 2.0 * (b.y - r);
    float qa = 1.5707963268 * r;
    perimeter = 2.0 * ex + 2.0 * ey + 4.0 * qa;

    float bx = b.x - r, by = b.y - r;
    float ax = abs(p.x), ay = abs(p.y);
    float cx = ax - bx, cy = ay - by;

    if (cx > 0.0 && cy > 0.0)                                   // corner -> arc-length by angle (phi), uniform + continuous
    {
        float phi = atan2(cy, cx);
        float s;
        if      (p.x >= 0.0 && p.y >= 0.0) s = phi * r;
        else if (p.x <  0.0 && p.y >= 0.0) s = qa + ex + (1.5707963268 - phi) * r;
        else if (p.x <  0.0 && p.y <  0.0) s = 2.0 * qa + ex + ey + phi * r;
        else                               s = 3.0 * qa + 2.0 * ex + ey + (1.5707963268 - phi) * r;
        return s;
    }
    // Classify by the NEAREST edge, not just cx/cy vs the CORNER radius r: a thick stroke reaches MORE than r inside, so
    // the inner part of a top/bottom-edge stroke has cy<=0 (inside the inner box) yet is still nearest the horizontal
    // edge. Deciding by cx/cy alone mis-routed that inner sliver (width halfW-r) to the VERTICAL-edge arc-length -> a thin
    // mis-dashed line on the inner edge that only showed when halfW > r (thick stroke / small corner).
    bool horizontal = (cx <= 0.0) && (cy > 0.0 || (b.y - ay) < (b.x - ax));
    float s;
    if (horizontal) s = (p.y >= 0.0) ? qa + (bx - p.x) : 3.0 * qa + ex + ey + (p.x + bx);
    else            s = (p.x >= 0.0) ? 4.0 * qa + 2.0 * ex + ey + (p.y + by) : 2.0 * qa + ex + (by - p.y);
    return s;
}

// Signed distance (device px) to a rounded rect with a selectable outer-corner JOIN: 0 = miter (sharp, Chebyshev outer
// corner), 1 = bevel (45-deg chamfer), 2 = round (Euclidean, the natural offset). Straight edges are identical across all
// three; only the corner (both q>0) differs. This is what gives stroke-join parity with the pen (PenLineJoin).
float SdRoundRectJoin(float2 p, float2 b, float r, int joinType)
{
    float2 q = abs(p) - b + r;
    float inside = min(max(q.x, q.y), 0.0);
    float2 qp = max(q, float2(0.0, 0.0));
    // A ROUNDED geometry (r>0) already curves the corner - the join is moot and applying miter/bevel would (wrongly)
    // reshape the FILL, so only a (near-)sharp corner honours the join. Round join is always the plain Euclidean offset.
    float outside = (r > 0.5 || joinType == 2) ? length(qp)
                  : (joinType == 1) ? (qp.x + qp.y)                   // bevel: L1 - a 45-deg chamfer at the corner, but a
                                                                     // straight edge (one component 0) stays exact
                  : max(qp.x, qp.y);                                  // miter (sharp)
    return inside + outside - r;
}

// Arc-length `s` (device px) along an ELLIPSE contour to the fragment's radial projection, and the perimeter. Perimeter
// is Ramanujan's closed form; the partial length is a short trapezoidal integral of ds/dt = sqrt(rx^2 sin^2 t + ry^2
// cos^2 t) - exact for a circle, sub-pixel for real ellipses. The loop runs only for the thin stroke ring's fragments.
float EllipseArc(float2 p, float2 h, out float perimeter)
{
    float a = max(h.x, h.y), b = min(h.x, h.y);
    float hh = ((a - b) * (a - b)) / max((a + b) * (a + b), 1e-6);
    perimeter = 3.14159265 * (a + b) * (1.0 + 3.0 * hh / (10.0 + sqrt(4.0 - 3.0 * hh)));

    float t = atan2(p.y * h.x, p.x * h.y);        // parametric angle of the radial projection
    if (t < 0.0) t += 6.28318530718;

    const int N = 16;
    float dt = t / float(N);
    float s = 0.0;
    float prev = h.y;                             // ds/dt at t=0 = ry
    for (int i = 1; i <= N; i++)
    {
        float u = dt * float(i);
        float su = sin(u), cu = cos(u);
        float cur = sqrt(h.x * h.x * su * su + h.y * h.y * cu * cu);
        s += 0.5 * (prev + cur) * dt;
        prev = cur;
    }
    return s;
}

struct RectItem
{
    float4 Bounds : Position;    // world-space x, y, w, h (baked on the CPU)
    float4 Params : TEXCOORD0;   // .x = corner radius (uniform); .yzw reserved
    float4 Color  : COLOR0;      // straight (non-premultiplied) RGBA, opacity already folded in
    float4 StrokeColor : COLOR1; // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0 : TEXCOORD1;  // width_px, align, dashOn, dashGap
    float4 Stroke1 : TEXCOORD2;  // dashOffset, trimStart, trimEnd, flags
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment position relative to the rect CENTRE (SDF space)
    float2 Half     : TEXCOORD1;   // rect half-size
    float  Radius   : TEXCOORD2;   // corner radius
    float4 Color    : COLOR0;
    float4 StrokeColor : COLOR1;
    float4 Stroke0  : TEXCOORD3;
    float4 Stroke1  : TEXCOORD4;
};

[shader("vertex")]
PSInput RectBatchVS(RectItem item, uint vertexId : SV_VertexID)
{
    PSInput o;
    // 4-vertex triangle strip: corner = (0,0),(1,0),(0,1),(1,1) from the two low bits of the vertex id. The quad is
    // grown by the OUTWARD stroke reach so an outside/centre stroke isn't clipped by the fill's own bounds.
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(item.Stroke0.x * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;   // outward reach + 1px AA
    float2 worldPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Position = mul(float4(worldPos, 0.0, 1.0), Projection);
    o.Half   = item.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Radius = item.Params.x;
    o.Color  = item.Color;
    o.StrokeColor = item.StrokeColor;
    o.Stroke0 = item.Stroke0;
    o.Stroke1 = item.Stroke1;
    return o;
}

// Signed distance to a rounded box (iq): negative inside, 0 on the edge, positive outside.
float SdRoundBox(float2 p, float2 b, float r)
{
    float2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

[shader("fragment")]
float4 RectBatchPS(PSInput input) : SV_Target
{
    float r = min(input.Radius, min(input.Half.x, input.Half.y));   // a corner can't exceed half the smaller side
    int joinType = int(fmod(floor(input.Stroke1.w / 512.0), 8.0));  // 0 miter, 1 bevel, 2 round
    float d = SdRoundRectJoin(input.Local, input.Half, r, joinType);
    float mask = 1.0;
    if (input.Stroke0.z > 0.0 || input.Stroke1.y > 0.0 || input.Stroke1.z < 1.0)   // dashed or trimmed -> arc length
    {
        float halfW = input.Stroke0.x * 0.5;
        float perim;
        float s = RoundRectArc(input.Local, input.Half, r, perim);
        float dPerp = d - input.Stroke0.y * halfW;
        // Dash on the CONTINUOUS centreline arc-length through corners too (like the ellipse), so a dash flows around a
        // corner uniformly instead of the whole corner snapping to one on/off state at its midpoint - the latter cut a
        // dash short at the corner (a stub that wandered with the dash phase) when the run ended inside the corner arc.
        mask = DashTrimMask(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
                            input.Stroke1.z, dPerp, halfW, input.Stroke1.w);
    }
    return CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask);
}

// ---- InstancedFill pass: general retained geometry instancing (§4h/§4j) --------------------------------------------
// A SHARED local mesh (bound as the only vertex buffer) drawn instanceCount times; each instance's world transform and
// colour are fetched from this StructuredBuffer by SV_InstanceID. So N identical shapes = ONE instanced draw, and a
// move/resize/recolour is a patch of one record - no per-frame re-record. Matches Retained/GeometryInstance.cs.
struct GeometryInstance
{
    float4x4 World;   // full per-instance world transform (element local -> world). Matches Matrix4x4F World.
    float4 Color;     // straight-alpha RGBA (element/brush opacity folded into .w by the producer)
};

// Per-instance data by BUFFER DEVICE ADDRESS (BDA), not a descriptor-heap StructuredBuffer: the SV_InstanceID-indexed
// StructuredBuffer form did not bind/read on this device (the fill rasterised nothing - World came out garbage), while
// BDA is the engine's proven GPU-storage pattern (see StrokeEffect/FillFringeEffect: uint64_t address + (T*)addr).
uint64_t InstancesAddress;

struct FillPSInput
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
};

[shader("vertex")]
FillPSInput InstancedFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    GeometryInstance* instances = (GeometryInstance*)InstancesAddress;
    GeometryInstance inst = instances[instanceId];
    float4 world = mul(float4(v.position.xyz, 1.0), inst.World);
    FillPSInput o;
    o.Position = mul(world, Projection);
    o.Color = inst.Color;
    return o;
}

[shader("fragment")]
float4 InstancedFillPS(FillPSInput input) : SV_Target
{
    return input.Color;   // solid fill (straight alpha, drawn with AlphaBlend); analytic-AA fringe is a later pass
}

technique RectBatch
{
    pass Draw
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = RectBatchVS;
        PixelShader = RectBatchPS;
    }
}

technique InstancedFill
{
    pass Draw
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = InstancedFillVS;
        PixelShader = InstancedFillPS;
    }
}

// ---- EllipseBatch pass: solid ellipse/circle fills, resolution-independent SDF (docs/PER_MONITOR_DPI_PLAN.md, the
// "SDF family"). Draws MANY solid ellipses in ONE instanced draw: each fill is a per-instance EllipseItem expanded to a
// quad in the vertex stage (corner from SV_VertexID), and the pixel shader reconstructs the ellipse coverage from its
// implicit field - self-anti-aliasing, so no separate AA fringe per fill and no tessellation (crisp at any DPI/zoom).
// Positions baked to WORLD on the CPU; the vertex shader applies only the static Projection (matches RectBatch).
struct EllipseItem
{
    float4 Bounds : Position;   // world-space x, y, w, h (baked on the CPU)
    float4 Color  : COLOR0;     // straight (non-premultiplied) RGBA, opacity already folded into .w
    float4 StrokeColor : COLOR1; // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0 : TEXCOORD0;  // width_px, align, dashOn, dashGap
    float4 Stroke1 : TEXCOORD1;  // dashOffset, trimStart, trimEnd, flags
};

struct EllipsePSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment position relative to the ellipse CENTRE
    float2 Half     : TEXCOORD1;   // ellipse half-axes (rx, ry)
    float4 Color    : COLOR0;
    float4 StrokeColor : COLOR1;
    float4 Stroke0  : TEXCOORD2;
    float4 Stroke1  : TEXCOORD3;
};

[shader("vertex")]
EllipsePSInput EllipseBatchVS(EllipseItem item, uint vertexId : SV_VertexID)
{
    EllipsePSInput o;
    // 4-vertex triangle strip: corner = (0,0),(1,0),(0,1),(1,1) from the two low bits of the vertex id. Grown by the
    // outward stroke reach so an outside/centre stroke isn't clipped by the fill's bounds.
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(item.Stroke0.x * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 worldPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Position = mul(float4(worldPos, 0.0, 1.0), Projection);
    o.Half   = item.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Color  = item.Color;
    o.StrokeColor = item.StrokeColor;
    o.Stroke0 = item.Stroke0;
    o.Stroke1 = item.Stroke1;
    return o;
}

// Approximate SIGNED DISTANCE (device px) to an ellipse boundary: the implicit F = length(p/half) - 1 normalised by the
// length of its gradient (first-order/Taylor distance). Exact for a circle (rx==ry); for rx!=ry it's the correct shape
// with sub-pixel-accurate distance near the boundary - which is exactly where fill AA and the stroke ring live.
float SdEllipse(float2 p, float2 half)
{
    float2 h = max(half, float2(1e-6, 1e-6));
    float2 nq = p / h;
    float L = max(length(nq), 1e-6);
    float2 grad = float2(nq.x / h.x, nq.y / h.y) / L;   // d(F)/d(p)
    return (L - 1.0) / max(length(grad), 1e-6);
}

[shader("fragment")]
float4 EllipseBatchPS(EllipsePSInput input) : SV_Target
{
    float d = SdEllipse(input.Local, input.Half);
    float mask = 1.0;
    if (input.Stroke0.z > 0.0 || input.Stroke1.y > 0.0 || input.Stroke1.z < 1.0)   // dashed or trimmed -> arc length
    {
        float perim;
        float s = EllipseArc(input.Local, input.Half, perim);
        float halfW = input.Stroke0.x * 0.5;
        float dPerp = d - input.Stroke0.y * halfW;
        mask = DashTrimMask(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
                            input.Stroke1.z, dPerp, halfW, input.Stroke1.w);
    }
    return CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask);
}

technique EllipseBatch
{
    pass Draw
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = EllipseBatchVS;
        PixelShader = EllipseBatchPS;
    }
}
