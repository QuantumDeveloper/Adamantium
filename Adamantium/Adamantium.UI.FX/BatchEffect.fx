// Item-background batch (docs/TEXT_GLYPH_BATCH_PLAN.md - the item-backing instancing). Draws MANY solid rounded-rect
// fills (ItemsControl item backgrounds, and any solid rounded-rect fill) in ONE instanced draw: each fill is one
// per-instance RectItem, expanded to a quad in the vertex stage (corner from SV_VertexID), and the pixel shader
// reconstructs the rounded-rect coverage ANALYTICALLY from a signed-distance field - self-anti-aliasing, so there is
// no separate AA fringe unit per fill. Positions are baked to WORLD space on the CPU during aggregation; the vertex
// shader applies only a single static Projection (the one driver-safe form on this Turing - no per-instance matrix).
// Slang bodies. Row-vector convention (matches the engine's other effects).
//
// This one effect now holds the whole retained batch/instancing family as separate PASSES: RectBatch (the SDF rounded-
// rect instancing above) and InstancedFill (general geometry instancing - a SHARED local mesh drawn N times, per-instance
// world transform + colour fetched from a StructuredBuffer by SV_InstanceID; docs/RENDER_CACHE_REDESIGN.md sec. 4h/4j).

// In dependency order - each header builds on the ones above it. NOTHING after the path on these lines: a trailing
// comment on an #include makes the preprocessor stop with "unexpected tokens after directive", and the failure then
// arrives as every shader in the file falling back to DXC.
#include "Includes/CommonData.fxh"
#include "Includes/ClipMath.fxh"
#include "Includes/ShapeMath.fxh"
#include "Includes/StrokeMath.fxh"
#include "Includes/NoiseMath.fxh"





struct PSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment position relative to the rect CENTRE (SDF space)
    float2 Half     : TEXCOORD1;   // rect half-size
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    float4 Color    : COLOR0;
    float4 StrokeColor : COLOR1;
    float4 Stroke0  : TEXCOORD3;
    float4 Stroke1  : TEXCOORD4;
    float  Crisp    : TEXCOORD5;   // 1 = no fringe (see CompositeFillStroke)
    float4 Dash     : TEXCOORD6;   // dash runs 2..5 (device px)
    float4 Inset    : TEXCOORD7;   // border thickness per side (device px)
    // The rounded clip cutting this instance, FETCHED IN THE VERTEX STAGE (see ClipShapeBox): xy/zw = its rect in device
    // px, zw = 0 when there is none; and its four radii.
    nointerpolation float4 ClipBox   : TEXCOORD8;
    nointerpolation float4 ClipRadii : TEXCOORD9;
};

[shader("fragment")]
float4 RectBatchPS(PSInput input) : SV_Target
{
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = min(input.Radii, float4(lim, lim, lim, lim));   // a corner cannot exceed half the smaller side
    int joinType = int(fmod(floor(input.Stroke1.w / 4096.0), 8.0));  // 0 miter, 1 bevel, 2 round
    float d = SdRoundRectJoin(input.Local, input.Half, r4, joinType);

    // A BORDER (per-side thickness) instead of a pen: fill and ring composite from the two outlines in one go. Told apart
    // by Inset alone - a pen and a border never ride in the same instance (RectBatchCollector.WantsBatch).
    // The ROUNDED clip of an ancestor, applied as coverage - see ClipCoverage. 1 when there is none, so the plain case
    // costs one compare.
    float clip = ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);

    if (any(input.Inset > 0.0))
    {
        float4 bordered = CompositeFillBorder(d, input.Local, input.Half, r4, input.Inset, joinType,
                                              input.Color, input.StrokeColor, input.Crisp);
        return float4(bordered.rgb, bordered.a * clip);
    }

    float mask = 1.0;
    if (input.Stroke0.z > 0.0 || input.Stroke1.y > 0.0 || input.Stroke1.z < 1.0)   // dashed or trimmed -> arc length
    {
        float halfW = input.Stroke0.x * 0.5;
        float perim;
        float s = RoundRectArc(input.Local, input.Half, r4, perim);
        float dPerp = d - input.Stroke0.y * halfW;
        float capScl = ArcCapScale(RoundRectCurvRadius(input.Local, input.Half, r4), dPerp);
        // A CONCAVE cap may not eat past the middle of its dash, or a dash shorter than a thickness is consumed by
        // its own two caps and leaves only the slivers at the ribbon edges (bow-ties at every corner on a thick
        // stroke). The reach is homogeneous in (dPerp, halfW), so the limit rides in on the same scale. Convex caps
        // are untouched - their bulge is what makes a zero-length dot a circle. Codes 4 and 5 are the concave forms.
        float dashCaps = fmod(input.Stroke1.w, 64.0);
        bool concaveCap = max(fmod(dashCaps, 8.0), floor(dashCaps / 8.0)) >= 4.0;
        float bite = (concaveCap && input.Stroke0.z > 0.0) ? 0.5 * input.Stroke0.z / max(halfW, 1e-3) : 1e9;
        capScl = min(capScl, bite);
        // Dash on the CONTINUOUS centreline arc-length through corners too (like the ellipse), so a dash flows around a
        // corner uniformly instead of the whole corner snapping to one on/off state at its midpoint - the latter cut a
        // dash short at the corner (a stub that wandered with the dash phase) when the run ended inside the corner arc.
        // No corner special-casing here on purpose. The nearest-point arc is DISCONTINUOUS at a corner's bisector, and
        // every attempt to patch that from inside this mask - picking the "more on" of the two edges, unioning their two
        // coverages - trades one artifact for another, because the honest question is a DISTANCE to the dashed path and
        // no sampling of the arc answers it. Instead the batch now DECLINES a dashed stroke thicker than its corner is
        // round (RectBatchCollector.IsPenBatchable) and the compute expander takes it, which builds the dash pieces as
        // real geometry. What is left here is the case this model is exact for: corners at least as round as the stroke.
        mask = DashTrimMaskCapped(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
                            input.Stroke1.z, dPerp * capScl, halfW * capScl, input.Stroke1.w, input.Dash);
    }
    float4 painted = CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask, input.Crisp);
    return float4(painted.rgb, painted.a * clip);
}

// ---- InstancedFill pass: general retained geometry instancing (sec. 4h/4j) --------------------------------------------
// A SHARED local mesh (bound as the only vertex buffer) drawn instanceCount times; each instance's world transform and
// colour are fetched from this StructuredBuffer by SV_InstanceID. So N identical shapes = ONE instanced draw, and a
// move/resize/recolour is a patch of one record - no per-frame re-record. Matches Retained/GeometryInstance.cs.
struct GeometryInstance
{
    float4x4 Local;   // element local -> SLOT space. Matches Matrix4x4F Local.
    float4 Color;     // straight-alpha RGBA (element/brush opacity folded into .w by the producer)
    float4 Params;    // .x = transform-table slot; .y = opacity slot; .z = ROUNDED CLIP slot (-1 = none); .w spare
};

struct FillPSInput
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    nointerpolation float4 ClipBox   : TEXCOORD0;   // see PSInput: the clip's shape, fetched in the vertex stage
    nointerpolation float4 ClipRadii : TEXCOORD1;
};

[shader("vertex")]
FillPSInput InstancedFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    GeometryInstance* instances = (GeometryInstance*)InstancesAddress;
    GeometryInstance inst = instances[instanceId];
    // local -> slot space -> world. The slot matrix lives in the transform table, so a node move rewrites 64 bytes there
    // and every instance under it follows without this buffer being touched (same scheme as the SDF batches).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), inst.Local), nodes[(uint)inst.Params.x].World);
    FillPSInput o;
    o.Position = mul(world, Projection);
    int fillFadeSlot = int(inst.Params.y);
    float fillFade = lerp(1.0, nodes[max(fillFadeSlot, 0)].Params.x, step(0.0, float(fillFadeSlot)));
    o.Color = float4(inst.Color.rgb, inst.Color.a * fillFade);
    o.ClipBox   = ClipShapeBox(inst.Params.z);
    o.ClipRadii = ClipShapeRadii(inst.Params.z);
    return o;
}

[shader("fragment")]
float4 InstancedFillPS(FillPSInput input) : SV_Target
{
    // Solid fill (straight alpha, drawn with AlphaBlend); the fringe pass below feathers its edge. A rounded ancestor
    // clip is the one thing a MESH cannot get from its own geometry, so it comes from the slot like everywhere else.
    float clip = ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(input.Color.rgb, input.Color.a * clip);
}


[shader("vertex")]
FringePSInput InstancedFringeVS(FringeVertex v, uint instanceId : SV_InstanceID)
{
    GeometryInstance* instances = (GeometryInstance*)InstancesAddress;
    GeometryInstance inst = instances[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 m = mul(mul(inst.Local, nodes[(uint)inst.Params.x].World), Projection);

    FringePSInput o;
    float coverage;
    o.Position = ExpandFringe(v, m, coverage);
    int fillFadeSlot = int(inst.Params.y);
    float fillFade = lerp(1.0, nodes[max(fillFadeSlot, 0)].Params.x, step(0.0, float(fillFadeSlot)));
    o.Color = float4(inst.Color.rgb, inst.Color.a * fillFade);
    o.Coverage = coverage;
    o.ClipBox   = ClipShapeBox(inst.Params.z);
    o.ClipRadii = ClipShapeRadii(inst.Params.z);
    return o;
}

// Coverage -> alpha. THE flat fringe stage for the whole application: every ring that is one flat colour ends here, and
// there is exactly one of it, because the effect pool merges shaders by BYTECODE and refuses to give one shader two
// owning effects. That is also why the PATTERN's ring is drawn from this file rather than from BrushEffect - it takes
// the brush's low colour without evaluating anything, so it is this same pass.
[shader("fragment")]
float4 InstancedFringePS(FringePSInput input) : SV_Target
{
    float4 c = input.Color;
    c.a *= saturate(input.Coverage);   // 1 at the contour -> 0 at the outer edge = analytic edge coverage
    c.a *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return c;
}

// The analytic-AA fringe of the PATTERN/NOISE instances: the same shared ring and the same instance buffer as their
// body, so N elements cost one draw instead of N. The ring is one pixel wide, so it does not evaluate the pattern - it
// takes the brush's LOW colour, exactly as the per-unit fringe did (a procedural field is mostly its background, so an
// edge blends into Color1 rather than ringing a bright midpoint). The BODY of those instances is a brush and lives in
// BrushEffect; this ring is not, and reads the record from the shared header.
[shader("vertex")]
FringePSInput InstancedPatternFringeVS(FringeVertex v, uint instanceId : SV_InstanceID)
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 m = mul(mul(it.Local, nodes[(uint)it.Params.w].World), Projection);

    FringePSInput o;
    float coverage;
    o.Position = ExpandFringe(v, m, coverage);
    int patFringeSlot = int(it.Params.x);
    float patFringeFade = lerp(1.0, nodes[max(patFringeSlot, 0)].Params.x, step(0.0, float(patFringeSlot)));
    o.Color = float4(it.Color1.rgb, it.Color1.a * patFringeFade);
    o.Coverage = coverage;
    // PatternGeomData has no spare field for a clip slot yet - see the pattern body in BrushEffect.
    o.ClipBox   = float4(0.0, 0.0, 0.0, 0.0);
    o.ClipRadii = float4(0.0, 0.0, 0.0, 0.0);
    return o;
}

// ---- RectBatchInstanced: the SAME SDF rounded-rect batch, but per-instance RectItem read from a BDA STORAGE buffer by
// SV_InstanceID (like InstancedFill) instead of a per-instance VERTEX buffer. This lets the instance data be RETAINED +
// patched only over its dirty range (no full re-upload each frame) and, with tiles baked in a stable space, a scroll
// updates one offset uniform instead of re-baking N instances. Plain (no vertex semantics) struct matching the CPU
// RectItem's Vector4F layout; the quad still comes from SV_VertexID. Pixel shader is the shared RectBatchPS.
struct RectData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x = LARGEST corner radius; .y = transform-table slot; .z = no-fringe flag; .w = fade slot
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke); the BORDER's colour when Inset is non-zero
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
    float4 Inset;        // border thickness per side in device px: x left, y top, z right, w bottom (all 0 = no border)
    float4 Clip;         // .x = the ROUNDED CLIP's slot, or -1; .yzw spare
    int OwnerTag;        // CPU bookkeeping (which paint group baked this instance) - never read here, but it is part of
                         // the record, so the layout has to know about it or every instance after the first would be
                         // read from the wrong offset
};

[shader("vertex")]
PSInput RectBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    RectData* items = (RectData*)InstancesAddress;
    RectData item = items[instanceId];

    PSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    // Node-local corner -> world via the instance's transform-table matrix (slot 0 = identity for legacy world bakes).
    // The SDF inputs (Local/Half) keep the RECT's own ORIENTATION, so rounded corners + strokes are correct under
    // rotation, but are measured in DEVICE PIXELS (SlotPixelScale) so one AA width fits both axes even when the slot
    // scales them differently. A slot with no scale gives (1,1) and this is the identity of the old code.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)item.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);   // stroke width / radius / dashes are one number: an anisotropic slot has no exact answer

    float widthPx = item.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = item.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * item.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = item.Radii * iso;
    // The slot's alpha multiplies BOTH fill and stroke: fading a node fades what it draws, its outline included. Read
    // from the SAME record the matrix came from - no second buffer, no second address.
    // The element's fade, read from its opacity slot. Written INLINE, with an unsigned index and no helper taking the
    // pointer: the same read wrapped in a function that took `NodeSlot*` and a signed index left the window blank -
    // measured, and this driver is documented right below as going device-lost on shapes it dislikes.
    // .w < 0 means nothing above this element fades, and the select keeps that branch-free.
    float slotAlpha = nodes[(uint)max(item.Params.w, 0.0)].Params.x;
    slotAlpha = lerp(1.0, slotAlpha, step(0.0, item.Params.w));
    o.Color  = float4(item.Color.rgb, item.Color.a * slotAlpha);
    o.StrokeColor = float4(item.StrokeColor.rgb, item.StrokeColor.a * slotAlpha);
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    // Each side follows the axis it sits on: left/right take the horizontal pixel scale, top/bottom the vertical. `iso`
    // (the smaller of the two) is the right answer for a stroke WIDTH, which has no axis, and the wrong one here - under
    // a squashed slot a border would come out thicker on one pair of sides than it was asked for.
    o.Inset = item.Inset * float4(px.x, px.y, px.x, px.y);
    o.ClipBox   = ClipShapeBox(item.Clip.x);
    o.ClipRadii = ClipShapeRadii(item.Clip.x);
    o.Crisp = item.Params.z;
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
    float4 Dash     : TEXCOORD4;   // dash runs 2..5 (device px)
    float4 Arc      : TEXCOORD5;   // angular cut: start, end (radians), kind
    nointerpolation float4 ClipBox   : TEXCOORD6;   // see PSInput: the clip's shape, fetched in the vertex stage
    nointerpolation float4 ClipRadii : TEXCOORD7;
};

float EllipseCutDistance(float2 p, float2 h, float a0, float a1, float kind)
{
    float2 p0 = float2(h.x * cos(a0), h.y * sin(a0));   // where the arc starts, in device px
    float2 p1 = float2(h.x * cos(a1), h.y * sin(a1));   // ...and where it ends

    // Is the fragment inside the swept range? Its parametric angle, wrapped into [0, 2pi) from the start.
    float t = atan2(p.y * h.x, p.x * h.y);
    float from = t - a0;
    from -= 6.28318530718 * floor(from / 6.28318530718);
    bool within = from <= (a1 - a0);

    if (kind < 1.5)
    {
        // SECTOR: the boundary is the two radii. Distance to a SEGMENT (not to the infinite line): past the rim the
        // nearest point of the boundary is the endpoint itself, which is what keeps the corner where the arc meets the
        // radius from bleeding outward.
        float d0 = length(p - p0 * saturate(dot(p, p0) / max(dot(p0, p0), 1e-6)));
        float d1 = length(p - p1 * saturate(dot(p, p1) / max(dot(p1, p1), 1e-6)));
        float edge = min(d0, d1);
        return within ? -edge : edge;
    }

    // EDGE-TO-EDGE: the boundary is the chord, and the shape is the ellipse on the ARC's side of it. A half-plane, so the
    // infinite line is the honest distance - the chord's own ends sit on the rim, where the ellipse takes over.
    float2 chord = p1 - p0;
    float2 n = normalize(float2(chord.y, -chord.x));       // one of the two normals
    float2 mid = float2(h.x * cos((a0 + a1) * 0.5), h.y * sin((a0 + a1) * 0.5));   // a point on the arc, to orient it
    float side = dot(mid - p0, n) >= 0.0 ? 1.0 : -1.0;
    return -side * dot(p - p0, n);
}

[shader("fragment")]
float4 EllipseBatchPS(EllipsePSInput input) : SV_Target
{
    float d = SdEllipse(input.Local, input.Half);

    // A SECTOR or a SEGMENT is this same ellipse with a straight boundary added, so the FILL is the intersection of the
    // two fields. The OUTLINE is a different question, and the two closings answer it differently - the tessellator draws
    // exactly this distinction (`isClosed` is true only for a full ellipse or a Sector):
    //   SECTOR - closed contour: filled inside AND stroked all the way round, radii included. The combined distance is
    //            the outline, so the stroke follows it for free.
    //   EDGE-TO-EDGE - open contour: it is an ARC, and a ribbon along an arc has two ends, not four edges. The stroke
    //            stays on the ELLIPSE and is masked to the swept range, so a ring gauge reads as a ribbon that stops -
    //            not as a wedge outlined across its chord (which is what it looked like before this split).
    float dStroke = d;
    float mask = 1.0;

    // A RING is the same trick turned inward: the field MINUS its own inward offset, so the shape is the band between the
    // outline and a curve `ring` px inside it. That makes a ring gauge geometry rather than a thick stroke - its thickness
    // stops living in the pen, so the pen is free to outline it - and it composes with the cut below into an annular
    // sector without either knowing about the other.
    bool ring = input.Arc.w > 0.0;
    if (ring)
    {
        d = max(d, -(d + input.Arc.w));
    }

    if (input.Arc.z > 0.5)
    {
        d = max(d, EllipseCutDistance(input.Local, input.Half, input.Arc.x, input.Arc.y, input.Arc.z));
        // A ring's contour is CLOSED whichever way the cut closes it (two arcs and two ends), so it is stroked whole. An
        // open arc is the one case where the stroke stays on the ellipse and is masked to the sweep instead.
        if (input.Arc.z > 1.5 && !ring)
        {
            // The ends are cut by the two radii - the same wedge a sector is bounded by, used here only as a mask.
            float dWedge = EllipseCutDistance(input.Local, input.Half, input.Arc.x, input.Arc.y, 1.0);
            mask = saturate(0.5 - dWedge / max(fwidth(dWedge), 1e-5));
        }
        else
        {
            dStroke = d;
        }
    }
    else if (ring)
    {
        dStroke = d;   // a whole ring: two circles, and the stroke follows both
    }

    if (input.Stroke0.z > 0.0 || input.Stroke1.y > 0.0 || input.Stroke1.z < 1.0)   // dashed or trimmed -> arc length
    {
        float perim;
        float s = EllipseArc(input.Local, input.Half, perim);
        float halfW = input.Stroke0.x * 0.5;
        float dPerp = d - input.Stroke0.y * halfW;
        float capScl = ArcCapScale(EllipseCurvRadius(input.Local, input.Half), dPerp);
        mask = DashTrimMaskCapped(s, s, perim, input.Stroke0.z, input.Stroke0.w, input.Stroke1.x, input.Stroke1.y,
                            input.Stroke1.z, dPerp * capScl, halfW * capScl, input.Stroke1.w, input.Dash);
    }
    // The ROUNDED clip of an ancestor, as coverage - see ClipCoverage. 1 when there is none.
    float4 outColor = CompositeFillStrokeSplit(d, dStroke, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask, 0.0);
    return float4(outColor.rgb, outColor.a * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}

// ---- EllipseBatchInstanced: the SAME SDF ellipse batch, per-instance EllipseItem read from a BDA STORAGE buffer by
// SV_InstanceID (mirrors RectBatchInstanced). Plain struct matching the CPU EllipseItem's Vector4F layout; quad from
// SV_VertexID; shared EllipseBatchPS.
struct EllipseData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x = transform-table slot; .y = fade slot (-1 = none); .zw reserved (mirrors EllipseItem)
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
    float4 Arc;          // x = start, y = end (RADIANS of the parametric angle), z = 0 none / 1 sector / 2 edge-to-edge
};

[shader("vertex")]
EllipsePSInput EllipseBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    EllipseData* items = (EllipseData*)InstancesAddress;
    EllipseData item = items[instanceId];

    EllipsePSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    // Node-local -> world via the transform table (slot 0 = identity), and the SDF inputs in DEVICE PIXELS - same
    // scheme, and same reason, as RectBatchInstancedVS (see SlotPixelScale).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)item.Params.x].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float widthPx = item.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = item.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * item.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    // The element's fade, read inline with an unsigned index - see the rect pass for why this is not a helper.
    float fade = nodes[(uint)max(item.Params.y, 0.0)].Params.x;
    fade = lerp(1.0, fade, step(0.0, item.Params.y));
    o.Color  = float4(item.Color.rgb, item.Color.a * fade);
    o.StrokeColor = float4(item.StrokeColor.rgb, item.StrokeColor.a * fade);
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    o.Arc = item.Arc;   // angles are angles: no pixel scale applies to them
    o.ClipBox   = ClipShapeBox(item.Params.z);
    o.ClipRadii = ClipShapeRadii(item.Params.z);
    return o;
}

// ---- RegularPolygon batch: a shape of its own, drawn from its own record. A triangle and a circle differ by ONE number
// here - how many corners - and everything else (fill, stroke, ring, anti-aliasing) follows from the same field, so the
// family that draws chevrons, ticks, diamonds and hexagons costs one instanced draw like the rest.
// Deliberately NOT a flag on the ellipse: they share a shape of record, not a shape.
struct PolygonData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 bakes)
    float4 Params;       // .x = transform-table slot; .y = CORNERS (3 and up); .z = ring thickness in device px; .w = start angle (RADIANS)
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px)
    float4 Clip;         // .x = the ROUNDED CLIP's slot, or -1; .y = the OPACITY slot (-1 = opaque); .zw spare
};

struct PolygonPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the shape's CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // half-extents of the box the polygon is inscribed in
    float4 Color    : COLOR0;
    float4 StrokeColor : COLOR1;
    float4 Stroke0  : TEXCOORD2;
    float4 Stroke1  : TEXCOORD3;
    float4 Dash     : TEXCOORD4;
    float3 Shape    : TEXCOORD5;   // x = corners, y = ring thickness (device px), z = start angle (radians)
    nointerpolation float4 ClipBox   : TEXCOORD6;   // see PSInput: the clip's shape, fetched in the vertex stage
    nointerpolation float4 ClipRadii : TEXCOORD7;
};

[shader("vertex")]
PolygonPSInput PolygonBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    PolygonData* items = (PolygonData*)InstancesAddress;
    PolygonData item = items[instanceId];

    PolygonPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)item.Params.x].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float widthPx = item.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + item.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = item.Bounds.xy + corner * item.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = item.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * item.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;

    // The element's alpha from the OPACITY SLOT, as every other batched family reads it - see PolygonItem.Clip for why
    // it sits in the clip field. -1 means nothing above this element fades.
    // An INT test and a branch, NOT the sibling passes' `nodes[(uint)max(slot, 0.0)]`: that form takes this driver to
    // device-lost from this shader, 3 runs of 3, with the index MEASURED (painted to the screen) as a plain -1 and with
    // a known-good index in its place - so it is the shape of the read, not the value. `min`-clamping the index also
    // cured it, and was rejected: the bound would be an invented constant, and this form needs none.
    int polyFadeSlot = (int)item.Clip.y;
    float polyFade = polyFadeSlot < 0 ? 1.0 : nodes[(uint)polyFadeSlot].Params.x;
    o.Color  = float4(item.Color.rgb, item.Color.a * polyFade);
    o.StrokeColor = float4(item.StrokeColor.rgb, item.StrokeColor.a * polyFade);
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    o.Shape = float3(item.Params.y, item.Params.z * iso, item.Params.w);   // an ANGLE does not scale with the DPI
    o.ClipBox   = ClipShapeBox(item.Clip.x);
    o.ClipRadii = ClipShapeRadii(item.Clip.x);
    return o;
}

[shader("fragment")]
float4 PolygonBatchPS(PolygonPSInput input) : SV_Target
{
    float d = SdRegularPolygon(input.Local, input.Half, max(input.Shape.x, 3.0), input.Shape.z);

    // A RING, exactly as the ellipse does it: the field minus its own inward offset, so the thickness is geometry and the
    // pen stays free. A hollow triangle is a chevron nobody has to draw by hand.
    if (input.Shape.y > 0.0)
    {
        d = max(d, -(d + input.Shape.y));
    }

    // The ROUNDED clip of an ancestor, as coverage - see ClipCoverage. 1 when there is none.
    float4 outColor = CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, 1.0, 0.0);
    return float4(outColor.rgb, outColor.a * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}

// ---- Halo batch: the soft band UNDER a shape - an aura (no direction) or a shadow (offset), which are one arithmetic.
// No offscreen target and no blur kernel: the band IS the shape's own signed distance, read further out and shaped by a
// falloff, so a thousand shadowed cards stay a handful of instanced draws instead of a thousand raster passes.
struct HaloRectData
{
    float4 Bounds;   // the SHAPE's rect in slot units (the drawn quad is grown from it in the VS)
    float4 Params;   // .x corner radius, .y transform slot, .z shape (0 rect, 1 ellipse), .w inner flag
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Band;     // .xy offset, .z spread, .w softness - slot units
    float4 Color;
    float4 Field;    // .x = the distance range a SAMPLED field encodes, slot units (0 for an analytic shape);
                     // .y = the ROUNDED CLIP's slot, .z = the OPACITY slot (-1 = none for either); .w spare
};

struct HaloPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment from the SHAPE's centre, device px
    float2 Half     : TEXCOORD1;   // the shape's half-size, device px
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    float Scale     : TEXCOORD3;
    nointerpolation uint InstId : TEXCOORD4;
    nointerpolation float4 ClipBox   : TEXCOORD5;   // the ancestor's rounded clip, fetched in the VERTEX stage
    nointerpolation float4 ClipRadii : TEXCOORD6;
    // ...and the element's alpha from its opacity slot. Without it a band kept whatever alpha it was BAKED with, so a
    // fading ancestor reached it only through a re-bake - which a replayed frame never asks for. On screen that read as
    // "the glow does not fade until the window is clicked away", because THAT forces the walk that re-bakes it.
    nointerpolation float Fade : TEXCOORD7;
};

[shader("vertex")]
HaloPSInput HaloRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    HaloRectData* items = (HaloRectData*)InstancesAddress;
    HaloRectData it = items[instanceId];

    HaloPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    // The quad has to hold the whole band: how far it is thrown, plus the solid rim, plus the fade. An INNER band never
    // leaves the shape, so it grows by nothing. One pixel on top, for the coverage ramp at the very edge.
    float reach = it.Band.z + it.Band.w + max(abs(it.Band.x), abs(it.Band.y));
    float outsetPx = lerp(max(reach, 0.0) * iso, 0.0, step(0.5, it.Params.w)) + 1.0;

    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = it.Radii * iso;
    o.Scale  = iso;
    o.InstId = instanceId;
    o.ClipBox   = ClipShapeBox(it.Field.y);     // the band is cut by the ancestor's rounding like any other fill
    o.ClipRadii = ClipShapeRadii(it.Field.y);
    int haloFadeSlot = int(it.Field.z);         // int test + branch, not lerp/step - this driver dislikes that form
    o.Fade = haloFadeSlot < 0 ? 1.0 : nodes[(uint)haloFadeSlot].Params.x;
    return o;
}

// The shape, at an inflation: rect and ellipse from the same call so the branch is a lerp, not a jump.
float HaloShapeDistance(float2 p, float2 half, float4 radii, float inflate, float isEllipse)
{
    float2 h = max(half + inflate, float2(0.01, 0.01));
    float lim = min(h.x, h.y);
    float4 r = clamp(radii + inflate, float4(0.0, 0.0, 0.0, 0.0), float4(lim, lim, lim, lim));
    return lerp(SdRoundRectJoin(p, h, r, 2), SdEllipse(p, h), isEllipse);
}

// ARBITRARY geometry has no closed-form distance, so it is READ from a field baked per shape (HaloField). Same units,
// same pass, same falloff as the analytic shapes - which is the whole point of doing it this way rather than widening
// the AA ring: one halo, three kinds of shape.
// The field covers the shape's box grown by `range` on every side; 0.5 is exactly on the outline.
float HaloFieldDistance(float2 p, float2 half, float rangePx, float inflate, out float fade)
{
    float2 padded = half + rangePx;
    float2 uv = (p + padded) / max(padded * 2.0, float2(1e-4, 1e-4));
    float2 uvIn = saturate(uv);
    float enc = SourceTexture.SampleLevel(SourceSampler, uvIn, 0.0).r;

    // AT its range the field stops knowing: it encodes a CLAMP, not a distance. Cutting the band off there with a hard
    // test does not work - the lookup is FILTERED, so texels on the boundary interpolate below the threshold and light
    // up a faint contour of the baked box, which is the ghost rectangle around a big glow. Fade the band out over the
    // last of the range instead, so it always reaches zero inside what the field can answer for.
    fade = saturate((1.0 - enc) / 0.12);

    // OUTSIDE the box the clamped lookup keeps returning the edge texel, whose distance then never grows. Add how far
    // the fragment actually is from the box: a point out there is at least that much further from the shape.
    float outsidePx = length((uv - uvIn) * padded * 2.0);

    return (enc - 0.5) * 2.0 * rangePx - inflate + outsidePx;
}

[shader("fragment")]
float4 HaloRectPS(HaloPSInput input) : SV_Target
{
    HaloRectData* items = (HaloRectData*)InstancesAddress;
    HaloRectData it = items[input.InstId];

    float isEllipse = it.Params.z;
    float inner = it.Params.w;
    float sc = input.Scale;
    float2 offset = it.Band.xy * sc;
    float softness = max(it.Band.w * sc, 0.5);
    // An inner band grows INWARD, so its spread shrinks the source shape instead of inflating it.
    float spread = lerp(it.Band.z, -it.Band.z, step(0.5, inner)) * sc;

    // Shape 2 = a SAMPLED field (arbitrary geometry); 0/1 are the analytic rect and ellipse. Branch-free: both are
    // evaluated and picked, because a ?: in this family has device-lost form on this driver.
    float sampled = step(1.5, isEllipse);
    float analyticShape = saturate(isEllipse);   // 0 rect, 1 ellipse - shape 2 never uses it, but must not extrapolate
    float rangePx = it.Field.x * sc;
    float bandFade, shapeFade;
    float dBand = lerp(HaloShapeDistance(input.Local - offset, input.Half, input.Radii, spread, analyticShape),
                       HaloFieldDistance(input.Local - offset, input.Half, rangePx, spread, bandFade), sampled);
    float dShape = lerp(HaloShapeDistance(input.Local, input.Half, input.Radii, 0.0, analyticShape),
                        HaloFieldDistance(input.Local, input.Half, rangePx, 0.0, shapeFade), sampled);

    // Outer: full strength inside the thrown shape, faded to nothing one softness outside it. Inner: the mirror.
    // The curve is QUADRATIC, not a smoothstep: a smoothstep keeps too much brightness half-way out, and at a large
    // radius that blooms a detailed silhouette into round blobs - a star turns into a snowflake. Falling off faster
    // makes the band hug the outline it belongs to.
    float tOuter = saturate(dBand / softness);
    float tInner = saturate(-dBand / softness);
    float aOuter = (1.0 - tOuter) * (1.0 - tOuter);
    float aInner = (1.0 - tInner) * (1.0 - tInner);
    float a = lerp(aOuter, aInner, step(0.5, inner));

    // Clipped to the correct side of the REAL outline, as CSS clips a box-shadow: an outer band is not painted beneath
    // the shape (or a translucent card would darken itself), an inner one never leaves it.
    float aa = max(fwidth(dShape), 1e-4);
    a *= lerp(saturate(dShape / aa + 0.5), saturate(-dShape / aa + 0.5), step(0.5, inner));

    // A sampled band also fades out as the field runs out of range - see HaloFieldDistance.
    a *= lerp(1.0, bandFade, sampled);

    float4 color = it.Color;
    color.a *= saturate(a) * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return color;
}

// ---- Living halo: an aura whose REACH wanders along the outline and drifts over time, travelling a palette. A biofield
// rather than a rim of colour. Its own pass on purpose: the noise below is real ALU, and a plain shadow - which is most
// of what this family draws - must neither pay for it nor risk a heavier shader on a driver with this one's history.
//
// The wander is sampled in the coordinates that BELONG to an outline: ANGLE around the shape (along it) and DISTANCE
// from it (away). Sampling in screen space instead would shimmer independently of the shape, which reads as noise laid
// over a glow rather than as a glow that is alive.
struct HaloLivingData
{
    float4 Bounds;
    float4 Params;    // .x radius, .y slot, .z shape (0 rect, 1 ellipse, 2 field), .w inner
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Band;      // .z spread, .w softness - slot units
    float4 Field;     // .x field range, .y turbulence, .z flow, .w detail
    float4 Color;     // used when the palette is empty
    float4 Ramp;      // .x = valid palette stops; .y = the ROUNDED CLIP's slot, or -1; .zw spare
    float4 Stop0; float4 Stop1; float4 Stop2; float4 Stop3;
    float4 Stop4; float4 Stop5; float4 Stop6; float4 Stop7;
    float4 Offsets0; float4 Offsets1;
};

[shader("vertex")]
HaloPSInput HaloLivingVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    HaloLivingData* items = (HaloLivingData*)InstancesAddress;
    HaloLivingData it = items[instanceId];

    HaloPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    // The wander ADDS to the reach, so the quad has to hold more than a still band of the same radius would.
    float reach = it.Band.z + it.Band.w * (1.0 + it.Field.y);
    float outsetPx = lerp(max(reach, 0.0) * iso, 0.0, step(0.5, it.Params.w)) + 1.0;

    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = it.Radii * iso;
    o.Scale  = iso;
    o.InstId = instanceId;
    o.ClipBox   = ClipShapeBox(it.Ramp.y);    // Field is full here, so the living band's clip slot rides in Ramp.y
    o.ClipRadii = ClipShapeRadii(it.Ramp.y);
    int liveFadeSlot = int(it.Ramp.z);        // ...and its opacity slot in Ramp.z
    o.Fade = liveFadeSlot < 0 ? 1.0 : nodes[(uint)liveFadeSlot].Params.x;
    return o;
}

// The palette, sampled by the WANDER rather than by any direction - which is what makes it read as alive instead of as
// a gradient laid over the glow. Same stop layout every gradient in this file uses.
float4 LivingPalette(HaloLivingData it, float t)
{
    float count = it.Ramp.x;
    float4 colours[8] = { it.Stop0, it.Stop1, it.Stop2, it.Stop3, it.Stop4, it.Stop5, it.Stop6, it.Stop7 };
    float offsets[8] = { it.Offsets0.x, it.Offsets0.y, it.Offsets0.z, it.Offsets0.w,
                         it.Offsets1.x, it.Offsets1.y, it.Offsets1.z, it.Offsets1.w };

    float4 c = colours[0];
    for (int i = 1; i < 8; i++)
    {
        float active = step((float)i + 0.5, count);                  // this stop exists
        float span = max(offsets[i] - offsets[i - 1], 1e-4);
        float k = saturate((t - offsets[i - 1]) / span);
        c = lerp(c, lerp(colours[i - 1], colours[i], k), active * step(offsets[i - 1], t));
    }
    return c;
}

[shader("fragment")]
float4 HaloLivingPS(HaloPSInput input) : SV_Target
{
    HaloLivingData* items = (HaloLivingData*)InstancesAddress;
    HaloLivingData it = items[input.InstId];

    float isEllipse = it.Params.z;
    float inner = it.Params.w;
    float sc = input.Scale;
    float softness = max(it.Band.w * sc, 0.5);
    float spread = lerp(it.Band.z, -it.Band.z, step(0.5, inner)) * sc;

    float sampled = step(1.5, isEllipse);
    float analyticShape = saturate(isEllipse);
    float rangePx = it.Field.x * sc;

    float bandFade, shapeFade;
    float dBand = lerp(HaloShapeDistance(input.Local, input.Half, input.Radii, spread, analyticShape),
                       HaloFieldDistance(input.Local, input.Half, rangePx, spread, bandFade), sampled);
    float dShape = lerp(HaloShapeDistance(input.Local, input.Half, input.Radii, 0.0, analyticShape),
                        HaloFieldDistance(input.Local, input.Half, rangePx, 0.0, shapeFade), sampled);

    // ALONG the outline and AWAY from it. The angle wraps, so the noise is sampled on a CIRCLE in its own space - a
    // straight atan2 fed to a plane would tear where it wraps from +pi to -pi, and the tear would sit still while
    // everything else drifted.
    float ang = atan2(input.Local.y, max(length(input.Half), 1.0) * 0.0 + input.Local.x);
    float detail = it.Field.w;
    float2 ring = float2(cos(ang), sin(ang)) * detail;
    float away = dBand / max(softness, 1.0);
    float t = Time * it.Field.z;

    float n = SimplexNoise(ring + float2(t, -t * 0.7));
    float n2 = SimplexNoise(ring * 1.9 + float2(-t * 0.6, t * 0.4) + float2(away * 1.5, 0.0));
    float wander = (n * 0.65 + n2 * 0.35);            // ~[-1,1]

    // The reach breathes: the band's own distance is pulled in and pushed out along the outline.
    float dLive = dBand - wander * it.Field.y * softness;

    float tOuter = saturate(dLive / softness);
    float tInner = saturate(-dLive / softness);
    float aOuter = (1.0 - tOuter) * (1.0 - tOuter);
    float aInner = (1.0 - tInner) * (1.0 - tInner);
    float a = lerp(aOuter, aInner, step(0.5, inner));

    float aa = max(fwidth(dShape), 1e-4);
    a *= lerp(saturate(dShape / aa + 0.5), saturate(-dShape / aa + 0.5), step(0.5, inner));
    a *= lerp(1.0, bandFade, sampled);

    // The hue rides its OWN sample, not the wander. Colouring by the wander ties each end of the palette to a fixed
    // brightness - the far end always lands where the band has already faded - so one colour is never really seen.
    // Decorrelated, the hues travel across the band independently of how far it happens to be reaching.
    float hue = SimplexNoise(ring * 0.8 + float2(-t * 0.35, t * 0.9)) * 0.5 + 0.5;
    float4 colour = lerp(it.Color, LivingPalette(it, saturate(hue)), step(1.5, it.Ramp.x));
    colour.a *= saturate(a) * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return colour;
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
        Profile = 6.6;
        VertexShader = RectBatchInstancedVS;
        PixelShader = RectBatchPS;
    }

    // General geometry instancing - a shared local mesh drawn N times, per-instance world+colour from a BDA buffer.
    pass Fill
    {
        Profile = 6.6;
        VertexShader = InstancedFillVS;
        PixelShader = InstancedFillPS;
    }

    // The analytic-AA fringe of those same instances: one shared scale-free ring, the same instance buffer, one draw.
    pass Fringe
    {
        Profile = 6.6;
        VertexShader = InstancedFringeVS;
        PixelShader = InstancedFringePS;
    }

    // The flat ring of the PATTERN/NOISE instances - their fill is a brush (BrushEffect), their ring is not.
    pass PatternFringe
    {
        Profile = 6.6;
        VertexShader = InstancedPatternFringeVS;
        PixelShader = InstancedFringePS;
    }

    pass Halo
    {
        Profile = 5.1;
        VertexShader = HaloRectInstancedVS;
        PixelShader = HaloRectPS;
    }

    // An aura that BREATHES: its reach wanders along the outline and drifts over time, travelling a palette. Kept out of
    // the plain Halo pass so a still band pays nothing for the noise.
    pass HaloLiving
    {
        Profile = 5.1;
        VertexShader = HaloLivingVS;
        PixelShader = HaloLivingPS;
    }

    // SDF ellipse/circle fills - per-instance EllipseData from a BDA storage buffer by SV_InstanceID; quad from SV_VertexID.
    pass Ellipse
    {
        Profile = 6.6;
        VertexShader = EllipseBatchInstancedVS;
        PixelShader = EllipseBatchPS;
    }

    // Regular polygons - a triangle and a circle differ by one number, the corner count.
    pass Polygon
    {
        Profile = 6.6;
        VertexShader = PolygonBatchInstancedVS;
        PixelShader = PolygonBatchPS;
    }
}
