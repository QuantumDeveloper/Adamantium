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

// Global frame time in seconds, advanced by the render loop each present. Only the fractal's auto-morph reads it; every
// other pass ignores it. Unset (0) = no drift, so a static fractal renders fine before the loop starts feeding it.
float Time;

// GPU-resident TRANSFORM TABLE (see Rendering/TransformTable.cs): one world matrix per MOTION NODE (a scrolled panel, an
// animating tile), fetched by the per-instance slot index. Slot 0 is ALWAYS identity, so world-baked instances (index 0)
// render unchanged - the migration path: content moves to node-LOCAL bounds + a real slot incrementally, and from then on
// moving a node costs ONE 64-byte matrix write instead of re-baking its instances. Full matrices also keep ROTATED/3D
// instances inside the batch (the old axis-aligned world bake had to reject them to per-unit draws).
uint64_t TransformsAddress;

// GPU-resident FRACTAL REFERENCE ORBITS (perturbation deep-zoom): a flat float2[] holding every deep-zoom fractal
// instance's reference orbit Z_n concatenated. Each FractalRectData.Ref.x is this instance's START INDEX into it and
// .y the length. Zero (address 0) when no deep-zoom fractal is live - the shader only dereferences it on the deep path.
uint64_t OrbitAddress;

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

// Same as DashTrimMask, but the TRIM window wraps the fragment's signed arc offset to [-P/2, P/2] so a CONVEX cap at the
// contour seam (trimStart 0 -> start at s=0) bulges into the gap on the OTHER side of s=0 instead of clipping flat. Kept
// SEPARATE from DashTrimMask on purpose: this wrapped form miscompiled the driver's GRADIENT/pattern/fractal pixel-shader
// objects (they inline the helper too) into a device-lost, while the SOLID rect/ellipse stroke shaders compile it fine -
// so ONLY those two call it; the fill shaders stay on the plain DashTrimMask. Do NOT re-merge them.
float DashTrimMaskCapped(float sTrim, float sDash, float perimeter, float dashOn, float dashGap, float dashOffset,
    float trimStart, float trimEnd, float dPerp, float halfW, float capFlags)
{
    int dashCap  = int(fmod(capFlags, 8.0));
    int startCap = int(fmod(floor(capFlags / 8.0), 8.0));
    int endCap   = int(fmod(floor(capFlags / 64.0), 8.0));

    float mask = 1.0;
    if (trimStart > 0.0 || trimEnd < 1.0)
    {
        float a = trimStart * perimeter;
        float b = trimEnd * perimeter;
        float windowOpen = (b > a) ? 1.0 : 0.0;
        float centre = (a + b) * 0.5;
        float halfLen = (b - a) * 0.5;
        float ds = sTrim - centre;
        ds -= perimeter * floor(ds / perimeter + 0.5);
        float reach = CapReach((ds < 0.0) ? startCap : endCap, dPerp, halfW);
        mask *= windowOpen * saturate((halfLen - abs(ds)) + reach + 0.5);
    }
    float period = dashOn + dashGap;
    if (dashOn > 0.0 && period > 0.0)
    {
        float ph = frac((sDash + dashOffset) / period) * period;
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
        mask = DashTrimMaskCapped(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
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

// ---- RectBatchInstanced: the SAME SDF rounded-rect batch, but per-instance RectItem read from a BDA STORAGE buffer by
// SV_InstanceID (like InstancedFill) instead of a per-instance VERTEX buffer. This lets the instance data be RETAINED +
// patched only over its dirty range (no full re-upload each frame) and, with tiles baked in a stable space, a scroll
// updates one offset uniform instead of re-baking N instances. Plain (no vertex semantics) struct matching the CPU
// RectItem's Vector4F layout; the quad still comes from SV_VertexID. Pixel shader is the shared RectBatchPS.
struct RectData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x = corner radius (uniform); .y = transform-table slot; .zw reserved
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
};

[shader("vertex")]
PSInput RectBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    RectData* items = (RectData*)InstancesAddress;
    RectData item = items[instanceId];

    PSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(item.Stroke0.x * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    // Node-local corner -> world via the instance's transform-table matrix (slot 0 = identity for legacy world bakes).
    // The SDF inputs (Local/Half) stay in the RECT's own frame, so rounded corners + strokes are correct under rotation.
    float4x4* transforms = (float4x4*)TransformsAddress;
    float4x4 nodeWorld = transforms[(uint)item.Params.y];
    float2 localPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = item.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Radius = item.Params.x;
    o.Color  = item.Color;
    o.StrokeColor = item.StrokeColor;
    o.Stroke0 = item.Stroke0;
    o.Stroke1 = item.Stroke1;
    return o;
}

// ---- Ellipse batch: solid ellipse/circle fills, resolution-independent SDF (docs/PER_MONITOR_DPI_PLAN.md, the "SDF
// family"). Draws MANY solid ellipses in ONE instanced draw: each fill is a per-instance EllipseData record (from the BDA
// storage buffer) expanded to a quad in the vertex stage (corner from SV_VertexID), and the pixel shader reconstructs the
// ellipse coverage from its implicit field - self-anti-aliasing, no AA fringe, no tessellation (crisp at any DPI/zoom).
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
        mask = DashTrimMaskCapped(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
                            input.Stroke1.z, dPerp, halfW, input.Stroke1.w);
    }
    return CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask);
}

// ---- EllipseBatchInstanced: the SAME SDF ellipse batch, per-instance EllipseItem read from a BDA STORAGE buffer by
// SV_InstanceID (mirrors RectBatchInstanced). Plain struct matching the CPU EllipseItem's Vector4F layout; quad from
// SV_VertexID; shared EllipseBatchPS.
struct EllipseData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x = transform-table slot; .yzw reserved (mirrors the CPU EllipseItem)
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
};

[shader("vertex")]
EllipsePSInput EllipseBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    EllipseData* items = (EllipseData*)InstancesAddress;
    EllipseData item = items[instanceId];

    EllipsePSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(item.Stroke0.x * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    // Node-local -> world via the transform table (slot 0 = identity), same scheme as RectBatchInstancedVS.
    float4x4* transforms = (float4x4*)TransformsAddress;
    float4x4 nodeWorld = transforms[(uint)item.Params.x];
    float2 localPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = item.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * item.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Color  = item.Color;
    o.StrokeColor = item.StrokeColor;
    o.Stroke0 = item.Stroke0;
    o.Stroke1 = item.Stroke1;
    return o;
}

// ---- GradientRect: the SAME SDF rounded-rect batch, but the FILL is a LINEAR or RADIAL gradient (up to 8 stops)
// evaluated per fragment, instead of one solid colour. Per-instance GradientRectData from the BDA storage buffer by
// SV_InstanceID; the pixel shader reads the record (BDA) to get the gradient geometry + stops. Solid rects stay in the
// cheaper RectBatch untouched - this is a sibling pass only rects with a gradient fill route to. Matches CPU
// GradientRectItem. Fill+stroke are composited by the shared CompositeFillStroke, so a gradient tile still strokes.
struct GradientRectData
{
    float4 Bounds;       // world x, y, w, h
    float4 Params;       // .x corner radius, .y type (1 linear/2 radial/3 conic), .z stop count, .w spread (0 pad/1 reflect/2 repeat)
    float4 Geom0;        // LOCAL 0..1: linear (startXY, endXY) | radial (centerXY, radiusXY)
    float4 Geom1;        // radial focal (originXY, _, _); unused for linear
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Stop0; float4 Stop1; float4 Stop2; float4 Stop3;   // straight stop RGBA (opacity folded), only .z of Params valid
    float4 Stop4; float4 Stop5; float4 Stop6; float4 Stop7;
    float4 Offsets0;     // stop offsets 0..3
    float4 Offsets1;     // stop offsets 4..7
};

// Apply the spread mode to a raw gradient parameter t: pad = clamp, reflect = mirror-tile, repeat = tile.
float GradSpread(float t, int spread)
{
    if (spread == 1) { float m = fmod(abs(t), 2.0); return m > 1.0 ? 2.0 - m : m; }   // reflect
    if (spread == 2) { return frac(t); }                                              // repeat
    return saturate(t);                                                               // pad
}

// The gradient parameter t at fragment `uv` (0..1 across the bounds). Linear = projection onto the start->end axis.
// Radial = SVG focal formula: the fraction of the way from the focal point (origin) to the ellipse boundary, so an
// off-centre origin gives a real "spotlight". Coordinates are normalised by the radius so an ellipse becomes a unit circle.
float GradParam(GradientRectData it, float2 uv)
{
    if (int(it.Params.y) == 2)
    {
        float2 center = it.Geom0.xy;
        float2 radius = max(it.Geom0.zw, float2(1e-4, 1e-4));
        float2 focal = (it.Geom1.xy - center) / radius;   // focal in unit-circle space
        float2 q = (uv - center) / radius;
        float2 dir = q - focal;
        float dlen = length(dir);
        if (dlen < 1e-6) return 0.0;
        float2 dn = dir / dlen;
        float b = dot(focal, dn);
        float c = dot(focal, focal) - 1.0;
        float sEdge = -b + sqrt(max(b * b - c, 0.0));      // dist focal->unit-circle along the ray
        return (sEdge > 1e-6) ? (dlen / sEdge) : 0.0;
    }
    if (int(it.Params.y) == 3)   // conic: angular sweep around Geom0.xy; Geom0.z = start angle in turns
    {
        float2 d = uv - it.Geom0.xy;
        float ang = atan2(d.x, -d.y);                    // 0 at top (12 o'clock), + is clockwise (screen y is down)
        return frac(ang * 0.15915494309 - it.Geom0.z);   // * 1/(2*pi), rotated by the start angle
    }
    float2 start = it.Geom0.xy;
    float2 axis = it.Geom0.zw - start;
    float denom = dot(axis, axis);
    if (denom < 1e-9) return 0.0;
    return dot(uv - start, axis) / denom;
}

// ---- sRGB <-> OKLab (Bjorn Ottosson) for PERCEPTUAL gradient interpolation. Blending in sRGB muddies midpoints (a grey
// dead-zone between complements) and bands; OKLab is perceptually uniform, so the blend keeps even brightness + hue. Used
// only when a stop's interpolation mode is OKLab (mode 1); mode 0 (sRGB) leaves the colours untouched.
float3 SrgbToLinear(float3 c)
{
    float3 lo = c / 12.92;
    float3 hi = pow((c + 0.055) / 1.055, float3(2.4, 2.4, 2.4));
    return float3(c.x <= 0.04045 ? lo.x : hi.x, c.y <= 0.04045 ? lo.y : hi.y, c.z <= 0.04045 ? lo.z : hi.z);
}

float3 LinearToSrgb(float3 c)
{
    float3 lo = c * 12.92;
    float3 hi = 1.055 * pow(max(c, 0.0), float3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) - 0.055;
    return float3(c.x <= 0.0031308 ? lo.x : hi.x, c.y <= 0.0031308 ? lo.y : hi.y, c.z <= 0.0031308 ? lo.z : hi.z);
}

float3 LinearToOklab(float3 c)
{
    float l = 0.4122214708 * c.x + 0.5363325363 * c.y + 0.0514459929 * c.z;
    float m = 0.2119034982 * c.x + 0.6806995451 * c.y + 0.1073969566 * c.z;
    float s = 0.0883024619 * c.x + 0.2817188376 * c.y + 0.6299787005 * c.z;
    float l_ = pow(max(l, 0.0), 1.0 / 3.0);
    float m_ = pow(max(m, 0.0), 1.0 / 3.0);
    float s_ = pow(max(s, 0.0), 1.0 / 3.0);
    return float3(
        0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
        1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
        0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_);
}

float3 OklabToLinear(float3 c)
{
    float l_ = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
    float m_ = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
    float s_ = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;
    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;
    return float3(
        4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
}

// The colour at parameter t (already spread-mapped to 0..1) by interpolating the (offset-sorted) stops. `aa` is the pixel
// footprint of t (fwidth) so every stop transition is at least one pixel wide - ANTI-ALIASES hard stops (two stops on one
// offset), which otherwise stair-step; a smooth segment keeps its exact linear ramp. Summed OVER-lerps (not an early-out
// lookup) so a zero-width segment still contributes its 1px transition. `mode` 1 interpolates in OKLab (perceptual): the
// stops are converted to OKLab up front, blended there, and the result converted back - only the blend space changes (mode
// 0 is byte-for-byte the old sRGB path).
float4 GradColor(GradientRectData it, float t, float aa, int mode)
{
    int n = int(it.Params.z);
    if (n <= 0) return float4(0.0, 0.0, 0.0, 0.0);
    float offs[8];
    offs[0] = it.Offsets0.x; offs[1] = it.Offsets0.y; offs[2] = it.Offsets0.z; offs[3] = it.Offsets0.w;
    offs[4] = it.Offsets1.x; offs[5] = it.Offsets1.y; offs[6] = it.Offsets1.z; offs[7] = it.Offsets1.w;
    float4 cols[8];
    cols[0] = it.Stop0; cols[1] = it.Stop1; cols[2] = it.Stop2; cols[3] = it.Stop3;
    cols[4] = it.Stop4; cols[5] = it.Stop5; cols[6] = it.Stop6; cols[7] = it.Stop7;

    if (mode == 1)   // straight-sRGB stop colours -> OKLab (alpha stays linear)
    {
        for (int k = 0; k < n; k++)
        {
            cols[k] = float4(LinearToOklab(SrgbToLinear(cols[k].xyz)), cols[k].w);
        }
    }

    float4 col = cols[0];
    for (int i = 1; i < n; i++)
    {
        float lo = offs[i - 1];
        float hi = offs[i];
        float w = hi - lo;
        float bl;
        if (w > aa)
        {
            bl = saturate((t - lo) / max(w, 1e-6));                       // linear across the real segment
        }
        else
        {
            bl = saturate((t - 0.5 * (lo + hi)) / max(aa, 1e-6) + 0.5);   // 1px ramp centred on a hard stop
        }
        col = lerp(col, cols[i], bl);
    }

    if (mode == 1)   // blended in OKLab -> back to straight sRGB
    {
        col = float4(LinearToSrgb(OklabToLinear(col.xyz)), col.w);
    }
    return col;
}

struct GradPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float  Radius   : TEXCOORD2;   // corner radius
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read GradientRectData in the PS for its gradient
};

[shader("vertex")]
GradPSInput GradientRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    GradientRectData* items = (GradientRectData*)InstancesAddress;
    GradientRectData it = items[instanceId];

    GradPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(it.Stroke0.x * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    // Node-local -> world via the transform table (slot in Geom1.w - Geom1.z is the shape flag; slot 0 = identity),
    // same scheme as RectBatchInstancedVS.
    float4x4* transforms = (float4x4*)TransformsAddress;
    float4x4 nodeWorld = transforms[(uint)it.Geom1.w];
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Radius = it.Params.x;
    o.InstId = instanceId;
    return o;
}

// ONE gradient pixel shader for BOTH shapes (rounded rect + ellipse), branched on the per-instance shape flag (Geom1.z:
// >=0.5 = ellipse). A single gradient shader instead of two - so there is not a second near-identical BDA-reading shader.
[shader("fragment")]
float4 GradientPS(GradPSInput input) : SV_Target
{
    GradientRectData* items = (GradientRectData*)InstancesAddress;
    GradientRectData it = items[input.InstId];

    bool ellipse = it.Geom1.z >= 0.5;
    float r = min(input.Radius, min(input.Half.x, input.Half.y));
    int joinType = int(fmod(floor(it.Stroke1.w / 512.0), 8.0));
    float d = ellipse ? SdEllipse(input.Local, input.Half) : SdRoundRectJoin(input.Local, input.Half, r, joinType);

    float2 uv = input.Local / max(input.Half * 2.0, float2(1e-4, 1e-4)) + 0.5;   // 0..1 across the bounds
    int packedW = int(it.Params.w + 0.5);                       // Params.w packs spread (low 3 bits) + interp mode (>> 3)
    // MESH gradient (type 4) DEFERRED - it is NOT the ternary issue: the GRADIENT pass just flakes vkCreateShadersEXT COMPILE
    // with ANY extra code (tested branch-free too: ~25% window launch). All C# kept (MeshGradientBrush / GradientBake type 4 /
    // GradientRectCollector); re-enable ONE of these on a saner driver (branch-free preferred - no ?: which device-losts):
    //   float4 grad = GradColor(it, GradSpread(GradParam(it, uv), packedW & 7), fwidth(gt), packedW >> 3);
    //   float4 mesh = lerp(lerp(it.Stop0, it.Stop1, uv.x), lerp(it.Stop2, it.Stop3, uv.x), uv.y);
    //   float4 fill = lerp(grad, mesh, step(3.5, it.Params.y));
    float gt = GradSpread(GradParam(it, uv), packedW & 7);
    // Wrap-aware AA width: at a conic/repeat seam gt jumps 1->0 so fwidth(gt) spikes to ~1 (the whole gradient collapses to
    // hard-stop ramps -> a coloured line). Shifting by half a turn moves the discontinuity to the far side, so min() picks
    // the TRUE small derivative everywhere. Harmless for linear/radial (min keeps the real value).
    float4 fill = GradColor(it, gt, min(fwidth(gt), fwidth(frac(gt + 0.5))), packedW >> 3);

    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = it.Stroke0.x * 0.5;
        float perim;
        float s = ellipse ? EllipseArc(input.Local, input.Half, perim)
                          : RoundRectArc(input.Local, input.Half, r, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        mask = DashTrimMask(s, s, perim, it.Stroke0.z, it.Stroke0.w, it.Stroke1.x, it.Stroke1.y,
                            it.Stroke1.z, dPerp, halfW, it.Stroke1.w);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, it.Stroke0.x, it.Stroke0.y, mask);
}

// ---- GradientFill: general instanced geometry (a shared tessellated mesh drawn N times) whose FILL is a LINEAR/RADIAL
// gradient - the gradient sibling of InstancedFill. Per-instance GradientGeometryInstance from a BDA storage buffer by
// SV_InstanceID. Mirrors the PROVEN-STABLE unified GradientPS profile: the PIXEL shader re-reads the record by BDA and
// only a FEW interpolators cross the stage (the fragment's local mesh position + the instance id). Passing the whole
// gradient (15 float4) as interpolators was a much heavier shader signature and tripped the driver's shader-object flake
// far more often; BDA-in-PS with a light signature is what the stable rect/ellipse gradient already does.
struct GradGeomData
{
    float4x4 World;
    float4 Params;       // .x type (1 linear/2 radial), .y spread, .z stop count, .w interp mode (0 sRGB/1 OKLab)
    float4 Geom0;        // LOCAL 0..1: linear (startXY, endXY) | radial (centerXY, radiusXY)
    float4 Geom1;        // radial focal (originXY, _, _)
    float4 LocalBounds;  // shape local bounds: minXY, sizeXY
    float4 Stop0; float4 Stop1; float4 Stop2; float4 Stop3;
    float4 Stop4; float4 Stop5; float4 Stop6; float4 Stop7;
    float4 Offsets0; float4 Offsets1;
};

struct GradFillPSInput
{
    float4 Position : SV_Position;
    float2 Local : TEXCOORD0;                   // varying: fragment's local mesh xy (for uv)
    nointerpolation uint InstId : TEXCOORD1;    // instance -> re-read GradGeomData in the PS (light signature)
};

[shader("vertex")]
GradFillPSInput GradientFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    GradGeomData* items = (GradGeomData*)InstancesAddress;
    GradGeomData it = items[instanceId];
    float4 world = mul(float4(v.position.xyz, 1.0), it.World);

    GradFillPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 GradientFillPS(GradFillPSInput input) : SV_Target
{
    GradGeomData* items = (GradGeomData*)InstancesAddress;
    GradGeomData it = items[input.InstId];

    // Reconstruct a GradientRectData for the shared GradParam/GradColor (Bounds/stroke fields unused by the fill eval).
    GradientRectData gd;
    gd.Bounds = float4(0.0, 0.0, 0.0, 0.0);
    gd.Params = float4(0.0, it.Params.x, it.Params.z, it.Params.y);   // (_, type, stopCount, spread)
    gd.Geom0 = it.Geom0; gd.Geom1 = it.Geom1;
    gd.StrokeColor = float4(0.0, 0.0, 0.0, 0.0);
    gd.Stroke0 = float4(0.0, 0.0, 0.0, 0.0);
    gd.Stroke1 = float4(0.0, 0.0, 0.0, 0.0);
    gd.Stop0 = it.Stop0; gd.Stop1 = it.Stop1; gd.Stop2 = it.Stop2; gd.Stop3 = it.Stop3;
    gd.Stop4 = it.Stop4; gd.Stop5 = it.Stop5; gd.Stop6 = it.Stop6; gd.Stop7 = it.Stop7;
    gd.Offsets0 = it.Offsets0; gd.Offsets1 = it.Offsets1;

    float2 uv = (input.Local - it.LocalBounds.xy) / max(it.LocalBounds.zw, float2(1e-4, 1e-4));
    float gt = GradSpread(GradParam(gd, uv), int(gd.Params.w));
    return GradColor(gd, gt, min(fwidth(gt), fwidth(frac(gt + 0.5))), int(it.Params.w));   // wrap-aware AA (conic/repeat seam)
}

// ---- Pattern batch: the SAME SDF rounded-rect (self-AA shape + the shared stroke), but the FILL is a PROCEDURAL two-colour
// PATTERN (checkerboard/stripes/dots/grid) evaluated per fragment - resolution-independent, no texture. Per-instance
// PatternRectData from a BDA storage buffer by SV_InstanceID; the PS re-reads the record (light interpolator signature, like
// GradientPS) and mixes Color1/Color2 by the pattern. Solid/gradient rects stay in their own passes.
struct PatternRectData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x corner radius, .y pattern type (0 checker/1 stripes/2 dots/3 grid/4 FBM noise), .z cell (px), .w slot
    float4 Color1;       // straight RGBA, opacity folded
    float4 Color2;       // straight RGBA, opacity folded
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Noise;        // FBM noise (type 4 only): x octaves, y seed, z lacunarity, w gain
    float4 Color3;       // optional MID colour for a 3-colour noise gradient-map (Color1->Color3->Color2); .w==0 = off
};

struct PatternPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float  Radius   : TEXCOORD2;   // corner radius
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read PatternRectData in the PS
};

[shader("vertex")]
PatternPSInput PatternRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    PatternRectData* items = (PatternRectData*)InstancesAddress;
    PatternRectData it = items[instanceId];

    PatternPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(it.Stroke0.x * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float4x4* transforms = (float4x4*)TransformsAddress;
    float4x4 nodeWorld = transforms[(uint)it.Params.w];
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Radius = it.Params.x;
    o.InstId = instanceId;
    return o;
}

// --- Ashima/Gustavson 2D simplex noise (texture-free, ALU only; the webgl-noise MIT function). Returns ~[-1,1]. Feeds the
// FBM noise pattern type - no texture lookup, so it needs no descriptor, pure ALU like the rest of the batch. ---
float3 mod289_3(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float2 mod289_2(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float3 permute289(float3 x) { return mod289_3(((x * 34.0) + 1.0) * x); }

float snoise(float2 v)
{
    const float4 C = float4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
    float2 i  = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);
    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod289_2(i);
    float3 pp = permute289(permute289(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;
    float3 x = 2.0 * frac(pp * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
    float3 g;
    g.x  = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
}

// --- Alternative base noise functions for NoiseBrush.NoiseType. All texture-free ALU, return ~[-1,1] to match snoise so
// FBM/gradient-map stay identical across types. Only the base field changes. ---
// Dave Hoskins hash12 (same family as hash22, which is seam-free in Worley). Reduces the input with frac FIRST (robust at
// large lattice coords, no sin), mixes every component into every other via the dot, and finishes with an ADDITION
// (p3.x+p3.y)*p3.z - so it never collapses to ~0 along an axis the way frac(p.x*p.y*(p.x+p.y)) did (that zero-column was
// the vertical seam in value/perlin). Returns [0,1).
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Value noise: bilinearly interpolate a random value per lattice point (smoothstep fade). Blockier than gradient noise.
float vnoise(float2 v)
{
    float2 i = floor(v);
    float2 f = frac(v);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return (lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y)) * 2.0 - 1.0;
}

// Classic Perlin gradient noise: a random unit gradient per lattice point (angle from the now well-distributed hash21, so
// no column seam), dotted with the offset and interpolated. Smooth like simplex. The angle stays in [0,2pi], so no sin of
// large arguments.
float pnoise(float2 v)
{
    float2 i = floor(v);
    float2 f = frac(v);
    float2 u = f * f * (3.0 - 2.0 * f);
    float g0 = hash21(i) * 6.2831853;
    float g1 = hash21(i + float2(1.0, 0.0)) * 6.2831853;
    float g2 = hash21(i + float2(0.0, 1.0)) * 6.2831853;
    float g3 = hash21(i + float2(1.0, 1.0)) * 6.2831853;
    float a = dot(float2(cos(g0), sin(g0)), f - float2(0.0, 0.0));
    float b = dot(float2(cos(g1), sin(g1)), f - float2(1.0, 0.0));
    float c = dot(float2(cos(g2), sin(g2)), f - float2(0.0, 1.0));
    float d = dot(float2(cos(g3), sin(g3)), f - float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 1.4;
}

// Worley (cellular / Voronoi): squared distance to the nearest of one feature point per cell over the 3x3 neighbourhood,
// inverted so cell centres are bright. `phase` orbits each cell's feature point on a per-cell Lissajous so the cells FLOW in
// place when animated (phase=0 -> a fixed per-cell point, i.e. a static Voronoi). NESTED loop - this driver's weak spot.
float worley(float2 v, float phase)
{
    float2 i = floor(v);
    float2 f = frac(v);
    float md = 1.5;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 nb = float2(x, y);
            float2 h = hash22(i + nb);
            float2 pt = 0.5 + 0.35 * sin(phase + 6.2831853 * h);   // feature point orbits over time -> flowing cells
            float2 diff = nb + pt - f;
            md = min(md, dot(diff, diff));
        }
    }
    return (1.0 - sqrt(md)) * 2.0 - 1.0;
}

// iq's Voronoi distance (shadertoy Xd23Dh): distance to the nearest cell BORDER (the Voronoi edge network), NOT the nearest
// point - thin glowing cell walls / cracks instead of Worley's filled cells. Pass 1 finds the nearest feature point (mr) and
// its cell (mb); pass 2 takes the min distance to the perpendicular bisectors with the neighbours of mb. Feature points orbit
// by `phase` so the whole network morphs. Guards normalize(0) at the nearest cell itself. TWO nested loops - driver risk.
float voronoiEdge(float2 v, float phase)
{
    float2 n = floor(v);
    float2 f = frac(v);
    float2 mr = float2(0.0, 0.0);
    float2 mb = float2(0.0, 0.0);
    float md = 8.0;
    for (int j = -1; j <= 1; j++)
    {
        for (int i = -1; i <= 1; i++)
        {
            float2 b = float2(i, j);
            float2 h = hash22(n + b);
            float2 o = 0.5 + 0.35 * sin(phase + 6.2831853 * h);   // animated feature point
            float2 r = b + o - f;
            float d = dot(r, r);
            if (d < md)
            {
                md = d;
                mr = r;
                mb = b;
            }
        }
    }
    md = 8.0;
    for (int j = -1; j <= 1; j++)
    {
        for (int i = -1; i <= 1; i++)
        {
            float2 b = mb + float2(i, j);
            float2 h = hash22(n + b);
            float2 o = 0.5 + 0.35 * sin(phase + 6.2831853 * h);
            float2 r = b + o - f;
            float2 diff = r - mr;
            if (dot(diff, diff) > 1e-5)                            // skip the nearest cell itself -> no normalize(0)
            {
                md = min(md, dot(0.5 * (mr + r), normalize(diff)));
            }
        }
    }
    return md;   // 0 at borders, growing inside cells
}

// Pick the base noise by basis index (0 simplex / 1 perlin / 2 value / 3 worley). `phase` drives the Worley flow (others
// ignore it). Scalar branches only - no vector ternary.
float baseNoise(float2 p, int basis, float phase)
{
    if (basis == 1) return pnoise(p);
    if (basis == 2) return vnoise(p);
    if (basis == 3) return worley(p, phase);
    return snoise(p);
}

// Fractional Brownian motion: sum `oct` octaves of the chosen base noise, each octave freq*lacunarity and amp*gain.
// Normalised to ~[-1,1]. The 8-iteration loop with an early break caps the cost while honouring the per-instance octave count.
float fbm(float2 p, int oct, float lacunarity, float gain, int basis, float phase)
{
    float amp = 0.5;
    float freq = 1.0;
    float sum = 0.0;
    float norm = 0.0;
    float dmask = 1.0;
    if (basis == 3) dmask = 0.0;   // Worley animates via its own feature-point orbit, not a domain drift
    for (int o = 0; o < 8; o++)
    {
        if (o >= oct)
        {
            break;
        }
        // Per-octave domain warp: each octave drifts on its own phase-shifted orbit so the layers churn over each other
        // (the field evolves in place instead of rigidly scrolling). phase=0 when not animating -> a static field.
        float2 dv = dmask * 0.4 * float2(sin(phase * 0.6 + 1.7 * float(o)), cos(phase * 0.5 + 1.3 * float(o)));
        sum += amp * baseNoise(p * freq + dv, basis, phase);
        norm += amp;
        freq *= lacunarity;
        amp *= gain;
    }
    return (norm > 1e-5) ? sum / norm : 0.0;
}

// Ridged / turbulence FBM folds over simplex. Turbulence (mode 0) sums |noise| -> billowy/smoky; ridged (mode 1) sums
// (1-|noise|)^2 -> sharp ridges / marble veins. Returns ~[0,1] (already non-negative from the abs), so the caller maps it
// straight to the colour ramp rather than the signed *0.5+0.5 of the plain FBM types.
float fbmFold(float2 p, int oct, float lacunarity, float gain, int mode, float phase)
{
    float amp = 0.5;
    float freq = 1.0;
    float sum = 0.0;
    float norm = 0.0;
    for (int o = 0; o < 8; o++)
    {
        if (o >= oct)
        {
            break;
        }
        float2 dv = 0.4 * float2(sin(phase * 0.6 + 1.7 * float(o)), cos(phase * 0.5 + 1.3 * float(o)));   // per-octave churn
        float v = abs(snoise(p * freq + dv));
        if (mode == 1)
        {
            v = 1.0 - v;
            v = v * v;
        }
        sum += amp * v;
        norm += amp;
        freq *= lacunarity;
        amp *= gain;
    }
    return (norm > 1e-5) ? sum / norm : 0.0;
}

// Pattern mix factor at fragment `p` (device px from the rect's top-left): 0 = Color1, 1 = Color2. Anti-aliased by the
// fragment's pixel footprint (fwidth) - the checkerboard analytically (iq's filtered checker), the others via a ~1px
// smoothstep on a signed field - so edges stay crisp without a tiled texture. Type 4 is FBM noise (continuous 0..1).
float PatternMix(int type, float2 p, float cell, float4 noise)
{
    cell = max(cell, 1.0);
    float2 g = p / cell;

    if (type == 1)   // vertical stripes (the x-component of the checker: Color2 every other cell)
    {
        float w = fwidth(g.x) + 1e-4;
        float i = 2.0 * (abs(frac((g.x - 0.5 * w) * 0.5) - 0.5) - abs(frac((g.x + 0.5 * w) * 0.5) - 0.5)) / w;
        return saturate(0.5 - 0.5 * i);
    }
    if (type == 2)   // dots: a Color2 disc centred in each cell
    {
        float2 f = frac(g) - 0.5;
        float d = length(f) - 0.34;                // radius 0.34 cells
        float aa = fwidth(d) + 1e-4;
        return 1.0 - smoothstep(-aa, aa, d);       // 1 inside the dot
    }
    if (type == 3)   // grid: thin Color2 lines at cell boundaries
    {
        float2 dl = (0.5 - abs(frac(g) - 0.5)) * cell;   // px distance to the nearest line, per axis
        float dmin = min(dl.x, dl.y);
        float aa = fwidth(dmin) + 1e-4;
        return 1.0 - smoothstep(0.5, 0.5 + aa + 1.0, dmin);   // ~1px line
    }

    if (type == 4 || type == 7 || type == 8 || type == 9)   // FBM noise: 4 simplex / 7 perlin / 8 value / 9 worley
    {
        int basis = type - 6;                                            // 7->1 perlin, 8->2 value, 9->3 worley
        if (type == 4) basis = 0;                                        // 4 -> simplex
        int oct = int(abs(noise.x));                                     // octaves is sign-encoded: negative = animate
        float phase = 0.0;
        if (noise.x < 0.0) phase = Time;                                 // only an animating instance advances by Time
        float2 np = g + noise.y;                                         // base noise domain + seed offset
        float n = fbm(np, oct, max(noise.z, 1.0), noise.w, basis, phase);   // Color1 (low) -> Color2 (high); phase drives flow
        return saturate(n * 0.5 + 0.5);
    }
    if (type == 10 || type == 11)   // ridged (10) / turbulence (11): FBM folds over simplex, already ~[0,1]
    {
        int mode = 0;                              // turbulence
        if (type == 10) mode = 1;                  // ridged
        int oct = int(abs(noise.x));
        float phase = 0.0;
        if (noise.x < 0.0) phase = Time;
        float2 np = g + noise.y;
        float n = fbmFold(np, oct, max(noise.z, 1.0), noise.w, mode, phase);
        if (type == 11) n = n * 1.6;               // turbulence is dimmer (averaged |noise|) - lift it for contrast
        return saturate(n);
    }
    if (type == 12)   // Voronoi BORDER network (iq Xd23Dh): thin bright cell walls, morphing under Animate
    {
        float ph = 0.0;
        if (noise.x < 0.0) ph = Time;              // octaves-sign = animate flag
        float dd = voronoiEdge(g + noise.y, ph);
        float aa = fwidth(dd) + 1e-4;
        return 1.0 - smoothstep(0.0, 0.06 + aa, dd);   // Color2 on the borders, Color1 inside the cells
    }
    if (type == 5)   // hexagonal grid (honeycomb) lines
    {
        float2 grid = float2(1.0, 1.7320508);
        float2 hh = grid * 0.5;
        float2 a = frac(g / grid) * grid - hh;
        float2 b = frac((g + hh) / grid) * grid - hh;
        float2 gv = dot(a, a) < dot(b, b) ? a : b;             // fragment within the nearest hex, centred
        float2 ag = abs(gv);
        float hd = max(ag.x * 0.8660254 + ag.y * 0.5, ag.y);   // 0 at centre .. 0.5 at the hex edge
        float dpx = (0.5 - hd) * cell;                         // px to the nearest honeycomb edge
        float aa = fwidth(dpx) + 1e-4;
        return 1.0 - smoothstep(0.5, 0.5 + aa + 1.0, dpx);     // ~1px hex lines
    }
    if (type == 6)   // hatch lines; noise.xy = the unit line normal (cos/sin baked on the CPU - NO trig here, so the
    {                //                 already-maxed pattern PS doesn't grow: dot replaces the old p.x+p.y)
        float t = dot(p, float2(noise.x, noise.y)) / cell;
        float dpx = (0.5 - abs(frac(t) - 0.5)) * cell;       // px to the nearest line (cell = perpendicular spacing)
        float aa = fwidth(dpx) + 1e-4;
        return 1.0 - smoothstep(0.5, 0.5 + aa + 1.0, dpx);
    }

    // checkerboard (type 0): iq's analytically-filtered checker (period 2 in g -> cell-sized squares)
    float2 w2 = fwidth(g) + 1e-4;
    float2 i2 = 2.0 * (abs(frac((g - 0.5 * w2) * 0.5) - 0.5) - abs(frac((g + 0.5 * w2) * 0.5) - 0.5)) / w2;
    return saturate(0.5 - 0.5 * i2.x * i2.y);
}

// --- Combustible Voronoi (Shane, shadertoy 4tlSzl): 3D Voronoi fBm coloured by a blackbody FIRE palette. Its own colour
// path (the palette returns RGB, not a 2-colour lerp), so PatternPS handles type 13 specially. 5 layers x a 3x3x3 cell
// search - the heaviest pattern branch; watch the driver. ---
float3 hash33(float3 p)
{
    float n = sin(dot(p, float3(7.0, 157.0, 113.0)));
    return frac(float3(2097152.0, 262144.0, 32768.0) * n);
}

// 3D Voronoi (Shane's rehash of iq): squared distance to the nearest 3D feature point over the 3x3 cell block, the z loop
// unrolled (GPUs dislike deep nesting). Range [0,1].
float voronoi3(float3 p)
{
    float3 g = floor(p);
    p = frac(p);
    float3 b;
    float3 r;
    float d = 1.0;
    for (int j = -1; j <= 1; j++)
    {
        for (int i = -1; i <= 1; i++)
        {
            b = float3(float(i), float(j), -1.0);
            r = b - p + hash33(g + b);
            d = min(d, dot(r, r));
            b.z = 0.0;
            r = b - p + hash33(g + b);
            d = min(d, dot(r, r));
            b.z = 1.0;
            r = b - p + hash33(g + b);
            d = min(d, dot(r, r));
        }
    }
    return d;
}

// fBm of the 3D Voronoi with time dilation on the z axis (position and time frequencies advance at different rates -> a
// parallax "combustible" flow). 5 layers. Range [0,1].
float noiseLayers(float3 p, float time)
{
    float3 t = float3(0.0, 0.0, p.z + time * 1.5);
    float tot = 0.0;
    float sum = 0.0;
    float amp = 1.0;
    for (int i = 0; i < 3; i++)   // 3 layers (was 5) - trimmed to buy NVVM budget for the configurable palette
    {
        tot += voronoi3(p + t) * amp;
        p *= 2.0;
        t *= 1.5;
        sum += amp;
        amp *= 0.5;
    }
    return tot / sum;
}

// Shane's favourite fire palette: blackbody radiation across a 1400..2700K temperature range (Planck-ish per wavelength).
float3 firePalette(float i)
{
    float T = 1400.0 + 1300.0 * i;
    float3 L = float3(7.4, 5.6, 4.4);
    L = pow(L, float3(5.0, 5.0, 5.0)) * (exp(143876.719683 / (T * L)) - 1.0);
    return 1.0 - exp(-5e8 / L);
}

// Shared FILL colouring for the pattern/noise family - called by BOTH the SDF rect pattern PS and the arbitrary-geometry
// pattern-fill PS, so the two paths colour identically. `pTopLeft` = fragment from the shape's top-left (the pattern origin,
// fed to PatternMix); `centerRel`/`halfY` = fragment relative to the shape centre + half-height (the Combustible fireball).
// Single return (no early return - NVVM dislikes those in .fx helpers).
float4 PatternFillColor(PatternRectData it, float2 pTopLeft, float2 centerRel, float halfY)
{
    int ptype = int(it.Params.y);
    float4 fill;
    if (ptype == 13)   // Combustible Voronoi: its own 3D-ray + fire-palette colour path (ignores Color1/Color2 as a lerp)
    {
        float time = 0.0;
        if (it.Noise.x < 0.0) time = Time;   // octaves-sign = animate flag; a fire really wants Animate on
        float2 uv = centerRel / max(halfY, 1.0);   // centred, normalised by half height
        float cs = cos(time * 0.25);
        float si = sin(time * 0.25);
        float3 rd = normalize(float3(uv.x, uv.y, 0.3926991));   // ~PI/8 ray, gives the central fireball
        rd.xy = float2(rd.x * cs - rd.y * si, rd.x * si + rd.y * cs);   // rolling camera
        float c = noiseLayers(rd * 2.0, time);
        c = max(c + dot(hash33(rd) * 2.0 - 1.0, float3(0.015, 0.015, 0.015)), 0.0);   // subtle dust
        c *= sqrt(c) * 1.5;                                  // contrast
        // Palette. noise.w = flag (>=0.5 built-in blackbody fire; <0.5 the brush's own Color1->MidColor->Color2 ramp). Both
        // are computed and selected BRANCH-FREE by step() - the NVVM AV'd on this over-full PS with a divergent branch here.
        float3 fireCol = sqrt(saturate(pow(firePalette(c), float3(1.25, 1.25, 1.25))));
        float cc = saturate(c);
        float3 duo3 = lerp(it.Color1.xyz, it.Color2.xyz, cc);
        float3 triLo3 = lerp(it.Color1.xyz, it.Color3.xyz, saturate(cc * 2.0));
        float3 triHi3 = lerp(it.Color3.xyz, it.Color2.xyz, saturate(cc * 2.0 - 1.0));
        float3 tri3 = lerp(triLo3, triHi3, step(0.5, cc));
        float3 userCol = lerp(duo3, tri3, step(0.001, it.Color3.w));   // Color3.w==0 -> 2-colour ramp
        float3 col = lerp(userCol, fireCol, step(0.5, it.Noise.w));
        fill = float4(col, it.Color1.w);                            // alpha carries brush opacity
    }
    else
    {
        float k = PatternMix(ptype, pTopLeft, it.Params.z, it.Noise);
        // TRITONE gradient-map (Color1 -> Color3 mid -> Color2), BRANCH-FREE (step, no vector ternary - the NVVM device-lost
        // on a nested ternary here). Color3.w==0 (no mid colour) blends back to the plain two-colour duotone.
        float4 duo = lerp(it.Color1, it.Color2, k);
        float4 triLo = lerp(it.Color1, it.Color3, saturate(k * 2.0));
        float4 triHi = lerp(it.Color3, it.Color2, saturate(k * 2.0 - 1.0));
        float4 tri = lerp(triLo, triHi, step(0.5, k));
        fill = lerp(duo, tri, step(0.001, it.Color3.w));
    }
    return fill;
}

[shader("fragment")]
float4 PatternPS(PatternPSInput input) : SV_Target
{
    PatternRectData* items = (PatternRectData*)InstancesAddress;
    PatternRectData it = items[input.InstId];

    bool ellipse = it.Params.x < 0.0;   // negative baked corner radius = the ellipse shape flag (a rect passes radius >= 0)
    float r = min(input.Radius, min(input.Half.x, input.Half.y));
    int joinType = int(fmod(floor(it.Stroke1.w / 512.0), 8.0));
    float d = ellipse ? SdEllipse(input.Local, input.Half) : SdRoundRectJoin(input.Local, input.Half, r, joinType);

    float2 p = input.Local + input.Half;   // fragment from the shape's TOP-LEFT (stable pattern origin at the corner)
    float4 fill = PatternFillColor(it, p, input.Local, input.Half.y);

    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = it.Stroke0.x * 0.5;
        float perim;
        float s = ellipse ? EllipseArc(input.Local, input.Half, perim) : RoundRectArc(input.Local, input.Half, r, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        mask = DashTrimMask(s, s, perim, it.Stroke0.z, it.Stroke0.w, it.Stroke1.x, it.Stroke1.y,
                            it.Stroke1.z, dPerp, halfW, it.Stroke1.w);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, it.Stroke0.x, it.Stroke0.y, mask);
}

// ---- PatternFill: general instanced geometry (a shared tessellated mesh drawn N times) whose FILL is a PROCEDURAL
// pattern/noise brush - the pattern sibling of GradientFill, so pattern/noise work on ANY geometry (Path/Polygon/glyphs),
// not just the SDF rect. Per-instance PatGeomData from a BDA buffer by SV_InstanceID; the PS reconstructs a PatternRectData
// and calls the SAME PatternFillColor the SDF rect pattern PS uses (fed the fragment's LOCAL mesh position).
struct PatGeomData
{
    float4x4 World;
    float4 Params;       // .y pattern type, .z cell (LOCAL units). .x/.w unused
    float4 LocalBounds;  // shape local bounds: minXY, sizeXY
    float4 Color1;
    float4 Color2;
    float4 Color3;
    float4 Noise;        // x octaves (sign=animate), y seed, z lacunarity, w gain (or combustible fire-palette flag)
};

struct PatFillPSInput
{
    float4 Position : SV_Position;
    float2 Local : TEXCOORD0;                   // varying: fragment's local mesh xy
    nointerpolation uint InstId : TEXCOORD1;    // instance -> re-read PatGeomData in the PS (light signature)
};

[shader("vertex")]
PatFillPSInput PatternFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    PatGeomData* items = (PatGeomData*)InstancesAddress;
    PatGeomData it = items[instanceId];
    float4 world = mul(float4(v.position.xyz, 1.0), it.World);

    PatFillPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 PatternFillPS(PatFillPSInput input) : SV_Target
{
    PatGeomData* items = (PatGeomData*)InstancesAddress;
    PatGeomData it = items[input.InstId];

    // Reconstruct a PatternRectData for the shared PatternFillColor (Bounds/stroke fields unused by the fill eval).
    PatternRectData pd;
    pd.Bounds = float4(0.0, 0.0, 0.0, 0.0);
    pd.Params = it.Params;
    pd.Color1 = it.Color1;
    pd.Color2 = it.Color2;
    pd.StrokeColor = float4(0.0, 0.0, 0.0, 0.0);
    pd.Stroke0 = float4(0.0, 0.0, 0.0, 0.0);
    pd.Stroke1 = float4(0.0, 0.0, 0.0, 0.0);
    pd.Noise = it.Noise;
    pd.Color3 = it.Color3;

    float2 pTopLeft = input.Local - it.LocalBounds.xy;                                   // fragment from the shape top-left
    float2 centerRel = input.Local - (it.LocalBounds.xy + it.LocalBounds.zw * 0.5);      // fragment from the shape centre
    return PatternFillColor(pd, pTopLeft, centerRel, max(it.LocalBounds.w * 0.5, 1.0));
}

// ---- Fractal batch: the SAME SDF rounded-rect (self-AA shape + shared stroke), but the FILL is an escape-time FRACTAL
// (Julia/Mandelbrot) iterated per fragment - resolution-independent, no texture. Per-instance FractalRectData from a BDA
// storage buffer by SV_InstanceID; the PS re-reads the record, maps the fragment to the complex plane, iterates z=z²+c and
// colours by the smooth escape count. With the animate flag set, a Julia's C drifts on a Lissajous over the global Time.
struct FractalRectData
{
    float4 Bounds;       // NODE-local x, y, w, h
    float4 Params;       // .x corner radius, .y type (0 Julia/1 Mandelbrot), .z transform slot, .w max iterations
    float4 Geom;         // .x/.y complex-plane centre, .z zoom, .w morph speed
    float4 Julia;        // .x/.y Julia constant C, .z animate flag, .w reserved
    float4 Color1;       // straight RGBA, opacity folded
    float4 Color2;       // straight RGBA, opacity folded
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Ref;          // perturbation: .x orbit start index (into OrbitAddress), .y orbit length, .z deep flag (1=use), .w reserved
};

struct FractalPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float  Radius   : TEXCOORD2;   // corner radius
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read FractalRectData in the PS
};

[shader("vertex")]
FractalPSInput FractalRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    FractalRectData* items = (FractalRectData*)InstancesAddress;
    FractalRectData it = items[instanceId];

    FractalPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float outset = max(it.Stroke0.x * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float4x4* transforms = (float4x4*)TransformsAddress;
    float4x4 nodeWorld = transforms[(uint)it.Params.z];
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * it.Bounds.zw + (corner * 2.0 - 1.0) * outset;
    o.Radius = it.Params.x;
    o.InstId = instanceId;
    return o;
}

// Newton fractal for z³ - 1: iterate z -= (z³-1)/(3z²) and colour by which of the 3 cube roots of unity it converges to
// (a different look from escape-time: smooth colour basins with a fractal border). The two brush colours take two roots,
// their blend the third; converging in more steps (near a border) darkens, so the boundary detail shows. Animate flows it.
float4 NewtonColor(float2 z, int maxIt, bool animate, float4 c1, float4 c2)
{
    float2 r0 = float2(1.0, 0.0);
    float2 r1 = float2(-0.5, 0.8660254);
    float2 r2 = float2(-0.5, -0.8660254);
    int hit = -1;
    int i = 0;
    for (i = 0; i < maxIt; i++)
    {
        float2 z2 = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y);              // z²
        float2 z3 = float2(z2.x * z.x - z2.y * z.y, z2.x * z.y + z2.y * z.x);    // z³
        float2 num = float2(z3.x - 1.0, z3.y);                                  // z³ - 1
        float2 den = float2(3.0 * z2.x, 3.0 * z2.y);                            // 3z²
        float dd = dot(den, den);
        if (dd < 1e-12) break;                                                  // derivative ~0: stationary
        z -= float2(num.x * den.x + num.y * den.y, num.y * den.x - num.x * den.y) / dd;   // z -= num/den (complex divide)
        if (dot(z - r0, z - r0) < 1e-4) { hit = 0; break; }
        if (dot(z - r1, z - r1) < 1e-4) { hit = 1; break; }
        if (dot(z - r2, z - r2) < 1e-4) { hit = 2; break; }
    }

    float shade = 1.0 - float(i) / float(maxIt);
    if (animate) shade = frac(shade + Time * 0.05);

    float4 baseCol;
    if (hit == 0) baseCol = c1;
    else if (hit == 1) baseCol = c2;
    else if (hit == 2) baseCol = lerp(c1, c2, 0.5);
    else baseCol = float4(0.0, 0.0, 0.0, c1.w);   // never converged
    return float4(baseCol.rgb * shade, baseCol.w);
}

[shader("fragment")]
float4 FractalPS(FractalPSInput input) : SV_Target
{
    FractalRectData* items = (FractalRectData*)InstancesAddress;
    FractalRectData it = items[input.InstId];

    float r = min(input.Radius, min(input.Half.x, input.Half.y));
    int joinType = int(fmod(floor(it.Stroke1.w / 512.0), 8.0));
    float d = SdRoundRectJoin(input.Local, input.Half, r, joinType);

    // Fragment -> complex plane, aspect-correct: the smaller axis spans 3/zoom around the centre, so pixels stay square.
    float minHalf = max(min(input.Half.x, input.Half.y), 1e-4);
    float2 cp = it.Geom.xy + (input.Local / minHalf) * (1.5 / max(it.Geom.z, 1e-4));

    int formula = int(it.Julia.w);   // 0 Quadratic, 1 BurningShip, 2 Tricorn, 3 Celtic, 4 Multibrot, 5 Newton
    bool animate = it.Julia.z > 0.5;
    int maxIt = min(int(it.Params.w), 400);

    float4 fill;
    if (formula == 5)   // Newton: convergence basins, not escape-time (C-mode does not apply, so map the raw fragment)
    {
        fill = NewtonColor(cp, maxIt, animate, it.Color1, it.Color2);
    }
    else
    {
        bool mandelbrot = int(it.Params.y) == 1;   // C-mode: c is the fragment (else c is the Julia constant)
        float zoomAmp = 1.0 / max(it.Geom.z, 1.0);   // shrink morph amplitude as we zoom past 1x, so on-screen morph speed stays constant
        float expo = it.Geom.w;                       // Multibrot exponent d
        if (formula == 4 && animate)
        {
            expo = max(1.1, it.Geom.w + 2.0 * zoomAmp * sin(Time * 0.6));   // breathe the petals open/closed instead of drifting C
        }

        // PERTURBATION deep-zoom path (armed only for Quadratic z2+c past the deep threshold): iterate the SMALL delta
        // from a high-precision reference orbit (Z_n from OrbitAddress) so the whole shader stays float32 - no fp64, no wall.
        // NOTE: the .fx parser does NOT accept unary '!', so bail flags are tested as (escaped) / (escaped == false).
        if (it.Ref.y > 0.5)   // Ref.y = reference-orbit length; > 0 means the deep path is armed for this instance
        {
            float2* orbit = (float2*)OrbitAddress;
            uint ofs = (uint)it.Ref.x;
            int rlen = (int)it.Ref.y;
            // pixel offset from the REFERENCE point: (pixel - view centre) + (view centre - C_ref). The second term
            // (Ref.zw) lets the CPU pick a reference OFF the view centre (a longer-living orbit) without moving the view.
            float2 delta = (input.Local / minHalf) * (1.5 / max(it.Geom.z, 1e-4)) + float2(it.Ref.z, it.Ref.w);
            // SEGMENTED REBASING (Zhuoran, driver-friendly form): the naive rebase indexes the orbit by a data-dependent
            // variable (orbit[ofs+m], m resets to 0) - the driver's NVVM shader compiler AVs on that at startup. Here the
            // reference index is the INNER loop COUNTER j (monotonic, like the plain perturbation loop the driver accepts);
            // a rebase just breaks the inner loop and the OUTER loop starts a fresh segment from j=0. One orbit serves any
            // depth: no glitch blobs (reference near zero) and no short-orbit truncation.
            float2 Ref0 = orbit[ofs];
            float2 dz = mandelbrot ? float2(0.0, 0.0) : delta;   // Delta = z - Ref[j]. Julia: z0 offset. Mandelbrot: 0.
            float2 dc = mandelbrot ? delta : float2(0.0, 0.0);   // per-iteration additive. Mandelbrot: dc. Julia: 0.
            int pi = 0;                 // true iteration count (drives the smooth colour)
            float2 pz = float2(0.0, 0.0);
            bool escaped = false;
            bool done = false;
            for (int seg = 0; seg < maxIt; seg++)   // one segment per rebase; runtime bound so the driver does not unroll
            {
                bool rebased = false;
                int j = 0;
                for (j = 0; j + 1 < rlen; j++)
                {
                    float2 Z = orbit[ofs + (uint)j];                  // MONOTONIC reference index
                    pz = Z + dz;                                      // full z at this true iteration
                    if (dot(pz, pz) > 256.0) { escaped = true; break; }
                    if (pi + 1 >= maxIt) { done = true; break; }
                    float2 zdz = float2(Z.x * dz.x - Z.y * dz.y, Z.x * dz.y + Z.y * dz.x);
                    float2 dz2 = float2(dz.x * dz.x - dz.y * dz.y, 2.0 * dz.x * dz.y);
                    dz = 2.0 * zdz + dz2 + dc;                        // advance the perturbation
                    pi = pi + 1;
                    float2 Zn = orbit[ofs + (uint)(j + 1)];           // MONOTONIC (j+1)
                    pz = Zn + dz;                                     // full z after the advance
                    if (dot(pz, pz) < dot(dz, dz)) { dz = pz - Ref0; rebased = true; break; }   // rebase now
                }
                if (escaped) break;
                if (done) break;
                if (rebased == false) dz = pz - Ref0;   // inner ran out of reference -> rebase, restart the segment at j=0
            }
            if (escaped)
            {
                float sm = float(pi) + 1.0 - log2(max(0.5 * log2(dot(pz, pz)), 1.0));   // smooth continuous escape count
                float ramp = sqrt(saturate(sm / float(maxIt)));
                if (animate && mandelbrot) ramp = frac(ramp + Time * 0.06);   // Mandelbrot-mode: flow the colour ramp
                fill = lerp(it.Color1, it.Color2, ramp);
            }
            else
            {
                fill = float4(0.0, 0.0, 0.0, it.Color1.w);   // inside the set / glitch / ran out of reference: black
            }
        }
        else
        {

        // Julia: z0 = fragment, c = constant (drifting when animate). Mandelbrot: z0 = 0, c = fragment.
        float2 z;
        float2 cc;
        if (mandelbrot)
        {
            z = float2(0.0, 0.0);
            cc = cp;
        }
        else
        {
            z = cp;
            cc = it.Julia.xy;
            if (animate && formula != 4)   // drift the Julia constant; amplitude ~ 1/Zoom so the on-screen morph speed is zoom-independent
            {
                float amp = 0.18 * zoomAmp;
                cc += float2(amp * cos(Time), amp * sin(Time * 0.86));
            }

        }

        int i = 0;
        for (i = 0; i < maxIt; i++)
        {
            if (formula == 1)         // Burning Ship: (|Re z| + i|Im z|)² + c
            {
                float2 za = float2(abs(z.x), abs(z.y));
                z = float2(za.x * za.x - za.y * za.y, 2.0 * za.x * za.y) + cc;
            }
            else if (formula == 2)    // Tricorn / Mandelbar: conj(z)² + c
            {
                z = float2(z.x * z.x - z.y * z.y, -2.0 * z.x * z.y) + cc;
            }
            else if (formula == 3)    // Celtic: |Re(z²)| + i·Im(z²) + c
            {
                float2 z2 = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y);
                z = float2(abs(z2.x), z2.y) + cc;
            }
            else if (formula == 4)    // Multibrot: z^d + c (polar power, so d may be fractional / animated)
            {
                float rr = length(z);
                float th = atan2(z.y, z.x);
                z = pow(rr, expo) * float2(cos(expo * th), sin(expo * th)) + cc;
            }
            else                      // Quadratic: z² + c
            {
                z = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y) + cc;
            }
            if (dot(z, z) > 256.0) break;   // large bail radius -> smoother continuous colouring
        }

        if (i >= maxIt)
        {
            fill = float4(0.0, 0.0, 0.0, it.Color1.w);   // inside the set: black (keep the fill alpha)
        }
        else
        {
            float sm = float(i) + 1.0 - log2(max(0.5 * log2(dot(z, z)), 1.0));   // smooth (continuous) escape count
            float ramp = sqrt(saturate(sm / float(maxIt)));
            if (animate && mandelbrot && formula != 4)   // Mandelbrot-mode: no C to morph, so flow the colour ramp instead
            {
                ramp = frac(ramp + Time * 0.06);
            }
            fill = lerp(it.Color1, it.Color2, ramp);
        }
        }   // end float-path else (perturbation deep path handled above)
    }

    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = it.Stroke0.x * 0.5;
        float perim;
        float s = RoundRectArc(input.Local, input.Half, r, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        mask = DashTrimMask(s, s, perim, it.Stroke0.z, it.Stroke0.w, it.Stroke1.x, it.Stroke1.y,
                            it.Stroke1.z, dPerp, halfW, it.Stroke1.w);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, it.Stroke0.x, it.Stroke0.y, mask);
}

// =====================================================================================================================
// TECHNIQUE - one technique, one pass per draw variant (kept together at the end of the file so the shader code above
// reads top-to-bottom without technique boilerplate breaking it up). Each pass names its vertex + pixel shader; the C#
// accessor for a pass is "{Technique}{Pass}Pass" (e.g. pass Rect -> Effect.BatchRectPass). Every pass is INSTANCED: the
// per-instance data lives in a BDA storage buffer read by SV_InstanceID, so there is NO per-instance vertex buffer (Rect
// and Ellipse generate their quad from SV_VertexID; Fill draws a shared local mesh).
// =====================================================================================================================
technique Batch
{
    // SDF rounded-rect fills - per-instance RectData from a BDA storage buffer by SV_InstanceID; quad from SV_VertexID.
    pass Rect
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = RectBatchInstancedVS;
        PixelShader = RectBatchPS;
    }

    // General geometry instancing - a shared local mesh drawn N times, per-instance world+colour from a BDA buffer.
    pass Fill
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = InstancedFillVS;
        PixelShader = InstancedFillPS;
    }

    // General geometry instancing with a LINEAR/RADIAL GRADIENT fill (per-instance GradientGeometryInstance; gradient
    // passed VS->PS via interpolators, evaluated per fragment). Solid fills use pass Fill instead.
    pass GradientFill
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = GradientFillVS;
        PixelShader = GradientFillPS;
    }

    // General geometry instancing with a PROCEDURAL PATTERN/NOISE fill (per-instance PatGeomData; the PS re-reads the record
    // and evaluates the shared PatternFillColor per fragment). Solid fills use pass Fill, gradients pass GradientFill.
    pass PatternFill
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = PatternFillVS;
        PixelShader = PatternFillPS;
    }

    // SDF ellipse/circle fills - per-instance EllipseData from a BDA storage buffer by SV_InstanceID; quad from SV_VertexID.
    pass Ellipse
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = EllipseBatchInstancedVS;
        PixelShader = EllipseBatchPS;
    }

    // SDF rounded-rect OR ellipse fills with a LINEAR/RADIAL GRADIENT fill (per-instance GradientRectData; PS reads the
    // record by SV_InstanceID, branches shape on Geom1.z); quad from SV_VertexID. Solid shapes use pass Rect/Ellipse.
    pass Gradient
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = GradientRectInstancedVS;
        PixelShader = GradientPS;
    }

    // SDF rounded-rect fills with a PROCEDURAL PATTERN fill (per-instance PatternRectData; the PS re-reads by SV_InstanceID
    // and mixes Color1/Color2 by the pattern); quad from SV_VertexID. Solid/gradient rects use pass Rect/Gradient.
    pass Pattern
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = PatternRectInstancedVS;
        PixelShader = PatternPS;
    }

    // SDF rounded-rect fills with an escape-time FRACTAL fill (per-instance FractalRectData; the PS iterates z=z²+c and
    // colours by the smooth escape count, morphing C over Time when animate is set); quad from SV_VertexID.
    pass Fractal
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = FractalRectInstancedVS;
        PixelShader = FractalPS;
    }
}
