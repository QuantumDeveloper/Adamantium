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

// The source of the TEXTURED pass - ONE texture per segment, bound by TexRectCollector before its draw (the text batch
// binds its atlas the same way). Not an array and not bindless: this driver's shader-object compiler is documented to
// fall over on richer texture use, and a segment break per texture costs a handful of draws per frame in UI.
// t2/s2, NOT t1/s1: the glyph atlas of FontEffect.fx sits at t1, and the descriptor slots are shared across the frame -
// bound at t1 this sampler read the atlas and every nine-slice came out drawn with letters.
Texture2D SourceTexture : register(t2);
SamplerState SourceSampler : register(s2);

// GPU-resident TRANSFORM TABLE (see Rendering/TransformTable.cs): one world matrix per MOTION NODE (a scrolled panel, an
// animating tile), fetched by the per-instance slot index. Slot 0 is ALWAYS identity, so world-baked instances (index 0)
// render unchanged - the migration path: content moves to node-LOCAL bounds + a real slot incrementally, and from then on
// moving a node costs ONE 64-byte matrix write instead of re-baking its instances. Full matrices also keep ROTATED/3D
// instances inside the batch (the old axis-aligned world bake had to reject them to per-unit draws).
uint64_t TransformsAddress;

// One entry of that table. Alpha lives HERE, beside the matrix, rather than in a second table with a second address:
// adding one more global uint64_t to this effect stopped shader creation outright (measured - a declaration alone, used
// by nothing, killed startup 3 times out of 3, while the same build without it started 3 of 3). The parameter block is
// evidently at its limit, and one slot's matrix and alpha are one node's state anyway, so they belong together.
// Params.x = alpha (1 = opaque); .yzw reserved. Padded to 16 bytes so the struct stays 16-byte aligned.
struct NodeSlot
{
    float4x4 World;
    float4   Params;
};


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
// How many DEVICE PIXELS one unit of a slot's space spans, per axis. The SDF shapes are baked in the space of their
// transform-table slot, and that space is not the screen's: an element whose own scale lives in the slot (anything that
// drives its own transform - an animated one is exactly that) reaches the shader with, say, a 1-unit-wide rect and a
// scale of 125 in the matrix. Distances then mean 125 px along X and 1 px along Y, and ONE scalar AA width cannot serve
// both - which is what smeared the tab-selection bar along its whole length. Callers convert their SDF inputs with this,
// so `d` comes out in pixels and fwidth(d) is ~1 on every axis.
// NB single exit, no early return: an early `return` in a .fx body has repeatedly tripped an AV inside NVVM here.
float2 SlotPixelScale(float4x4 nodeWorld)
{
    float4x4 m = mul(nodeWorld, Projection);
    float2 halfVp = ViewportSize * 0.5;
    float2 ax = mul(float4(1.0, 0.0, 0.0, 0.0), m).xy * halfVp;
    float2 ay = mul(float4(0.0, 1.0, 0.0, 0.0), m).xy * halfVp;
    float2 scale = max(float2(length(ax), length(ay)), float2(1e-4, 1e-4));
    return ViewportSize.x < 1.0 ? float2(1.0, 1.0) : scale;   // no viewport supplied: leave the bake untouched
}

// `crisp` (0/1) takes the edges HARD instead of fading them over a pixel. An axis-aligned rectangle sitting on whole
// pixels needs no fade: coverage is exactly a half ON the edge, so two abutting rectangles compose to about three
// quarters and leave a dark hairline down their join. Off by default - a curve or a slanted edge still needs the fade.
// Takes the fill's distance and the STROKE's separately, because they are not always the same curve. One case needs it:
// an ellipse cut edge-to-edge is filled as a SEGMENT (closed by its chord) but stroked as an ARC - an open ribbon, since a
// ring gauge has two ends and not four edges. Everything else passes the one distance twice, through the wrapper below,
// and compiles to what it did before.
float4 CompositeFillStrokeSplit(float dFill, float dStroke, float4 fill, float4 stroke, float width, float align,
    float strokeMask, float crisp)
{
    float aa = max(fwidth(dFill), 1e-5);
    float covFill = saturate(0.5 - dFill / aa);

    float aaS = max(fwidth(dStroke), 1e-5);
    float halfW = width * 0.5;
    float dRing = abs(dStroke - align * halfW) - halfW;      // signed distance to the stroke ring
    float covStroke = (width > 0.0) ? saturate(0.5 - dRing / aaS) * saturate(strokeMask) : 0.0;

    // The crisp answer OVERWRITES the two coverages rather than being folded into their expressions: the anti-aliased
    // path then compiles to exactly what it did before this option existed.
    if (crisp > 0.5)
    {
        covFill = step(dFill, 0.0);
        covStroke = (width > 0.0) ? step(dRing, 0.0) * saturate(strokeMask) : 0.0;
    }

    float fa = fill.a * covFill;                             // fill effective (straight) alpha
    float sa = stroke.a * covStroke;                         // stroke effective (straight) alpha
    float outA = sa + fa * (1.0 - sa);                       // stroke OVER fill, straight compositing
    float3 outRGB = (outA > 1e-6) ? (stroke.rgb * sa + fill.rgb * fa * (1.0 - sa)) / outA : float3(0.0, 0.0, 0.0);
    return float4(outRGB, outA);
}

// What every pass but one calls: fill and outline are the same curve.
float4 CompositeFillStroke(float d, float4 fill, float4 stroke, float width, float align, float strokeMask, float crisp)
{
    return CompositeFillStrokeSplit(d, d, fill, stroke, width, align, strokeMask, crisp);
}

// A BORDER of its own thickness per side: the ring between the shape's outline and an INNER outline inset by (left, top,
// right, bottom). Composited in ONE call with the fill, because the two share that inner outline - drawn as two shapes,
// both would anti-alias it and the two halves would compose into a dark hairline all the way round (which is exactly
// what the old CombinedGeometry ring did).
//
// The inner box is not concentric: insetting different amounts moves the centre by half their difference. Its corners
// shrink by the THICKER of the two sides meeting there, the same rule the tessellated ring used (Border's
// DeflateCornerRadius) - a scalar corner cannot stay parallel to the outer one under unequal sides, and taking the
// thicker of the pair keeps the inner arc from bulging out past the border on the heavier side.
float4 CompositeFillBorder(float dOuter, float2 p, float2 half, float4 radii, float4 inset, int joinType,
    float4 fill, float4 border, float crisp)
{
    float2 shift = float2(inset.x - inset.z, inset.y - inset.w) * 0.5;
    float2 halfIn = max(half - float2(inset.x + inset.z, inset.y + inset.w) * 0.5, float2(0.0, 0.0));
    float lim = min(halfIn.x, halfIn.y);
    float4 radiiIn = clamp(radii - float4(max(inset.x, inset.y), max(inset.y, inset.z),
                                          max(inset.z, inset.w), max(inset.w, inset.x)),
                           float4(0.0, 0.0, 0.0, 0.0), float4(lim, lim, lim, lim));
    float dInner = SdRoundRectJoin(p - shift, halfIn, radiiIn, joinType);

    float aaO = max(fwidth(dOuter), 1e-5);
    float aaI = max(fwidth(dInner), 1e-5);
    float covOuter = (crisp > 0.5) ? step(dOuter, 0.0) : saturate(0.5 - dOuter / aaO);
    float covFill  = (crisp > 0.5) ? step(dInner, 0.0) : saturate(0.5 - dInner / aaI);
    // The border is what the shape covers and the fill does not. Written as a DIFFERENCE rather than as its own distance
    // field so the two coverages always add up to the shape's own - no seam to over- or under-blend where they meet.
    float covBorder = max(covOuter - covFill, 0.0);

    float ba = border.a * covBorder;
    float fa = fill.a * covFill;
    float outA = ba + fa * (1.0 - ba);
    float3 outRGB = (outA > 1e-6) ? (border.rgb * ba + fill.rgb * fa * (1.0 - ba)) / outA : float3(0.0, 0.0, 0.0);
    return float4(outRGB, outA);
}

// Cap "reach" along the contour, at a fragment whose PERPENDICULAR distance from the stroke centreline is dPerp: how far
// past a piece end the stroke still paints (SIGNED - negative reach cuts the end INWARD, giving the concave caps). All six
// PenLineCaps analytically, no cap geometry, matching the geometry stroker's codes (StrokeEffect.fx CapSd): 0 flat,
// 1 square, 2 convex round, 3 convex triangle, 4 concave triangle, 5 concave round. Convex/concave are mirrors (+/-).
float CapReach(int cap, float dPerp, float halfW)
{
    if (cap == 1) return halfW;                                            // square: rectangular nub
    if (cap == 2) return sqrt(max(halfW * halfW - dPerp * dPerp, 0.0));    // convex round: semicircle out
    if (cap == 3) return halfW - abs(dPerp);                              // convex triangle: tip out at the centre
    if (cap == 4) return abs(dPerp) - halfW;                              // concave triangle: V notch cut in
    if (cap == 5) return -sqrt(max(halfW * halfW - dPerp * dPerp, 0.0));   // concave round: semicircular bite in
    return 0.0;                                                            // flat: hard cut
}

// Physical px -> units of centreline arc length at a fragment `dPerp` across the stroke, on a contour whose radius of
// curvature there is `curvRadius` (1e9 on a straight edge -> 1.0).
// The fragment's own radius is curvRadius + dPerp, and the correction only means anything while that is a real radius:
// on the inner side of a bend tighter than the stroke is thick it passes through the centre of curvature and the
// arc-length field folds over itself. There, correct NOTHING - a big ratio there does not deepen a cap correctly, it
// just eats pixel-sized holes out of the stroke.
// Only the OUTER side of a bend is corrected (`max(dPerp, 0)`), which is also the only side that needs it: there one
// unit of arc covers more pixels, so a cap's reach in arc units must shrink. The inner side is left alone - a bend
// tighter than the stroke is thick sends the fragment's own radius through zero there, and amplifying by that ratio
// deepened a concave bite past the whole corner and ate a hole out of it. Written so the ratio cannot exceed 1 by
// construction: an explicit upper clamp of 1.0 was enough to make the driver's NVVM AV in vkCreateShadersEXT, as was a
// single ternary. Nothing in here may branch.
float ArcCapScale(float curvRadius, float dPerp)
{
    return curvRadius / max(curvRadius + max(dPerp, 0.0), 0.5);
}

// Dash + trim coverage (0..1) at arc-length `s` (device px) along a contour of length `perimeter`. Makes dashes/trim
// ANALYTIC (per-fragment, no cut geometry) so a dashed/trimmed stroke still BATCHES. dashOn<=0 = solid. Piece ends are
// shaped by their caps (packed base-8 into capFlags: dashStart + 8*dashEnd + 64*start + 512*end). dPerp = signed
// perpendicular distance from the stroke centreline; halfW = half the stroke width. ~1px AA everywhere.
// sTrim cuts the trim window; sDash phases the dashes. Both are the fragment's continuous centreline arc-length (callers
// pass the same value) - corners included, since the corner arc-length is angle-based and thus uniform across the width.
//
// A visible PIECE is one dash run clipped to the trim window, and each of its two ends wears exactly ONE cap: the dash
// cap, or the line cap where the trim window cuts first. Masking dashes and trim separately and multiplying (what this
// did) stamped BOTH onto the first and last dash - a line cap and a dash cap fighting over one end.
//
// capScale converts a cap's PHYSICAL reach into units of `s`. They are not the same unit: `s` is the CENTRELINE arc,
// deliberately uniform across the stroke width so a dash boundary stays radial through a corner (see RoundRectArc),
// while a cap reaches a real number of pixels. On a straight edge capScale is 1; on a bend of curvature radius R it is
// R / (R + dPerp), because one unit of s spans that much more at the outer radius. Adding the two raw made a concave
// cap bite several times deeper at a corner than on a straight edge - it ate the dash there and left a hair-thin arc
// along the outer edge, and only at corners.
// Total length of a dash pattern of `count` runs: the first two ride in Stroke0.zw, the rest (up to four more) in Dash.
// A pattern is always an alternating ON, OFF, ON, OFF... and always an EVEN number of runs, so it tiles seamlessly.
float DashPatternLength(float dashOn, float dashGap, float4 rest, int count)
{
    float total = dashOn + dashGap;
    if (count > 2) total += rest.x + rest.y;
    if (count > 4) total += rest.z + rest.w;
    return total;
}

// The dash run this fragment's phase falls in, as (distance from its START, distance to its END) - both positive inside
// an ON run, and in a GAP the nearer neighbouring run's edge as a NEGATIVE distance with the other left "nowhere near".
// That asymmetry is what lets a convex cap bulge into the gap it faces while the far side stays off.
// Returned as a float2 rather than through out-parameters: a second out-parameter in this family is what made the
// driver's NVVM compiler AV in vkCreateShadersEXT (see the note above DashTrimMaskCapped).
float2 DashPiece(float ph, float dashOn, float dashGap, float4 rest, int count)
{
    float dS = 1e9;
    float dE = 1e9;
    float a = 0.0;
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        if (i >= count) continue;
        float len = (i == 0) ? dashOn : (i == 1) ? dashGap : (i == 2) ? rest.x : (i == 3) ? rest.y : (i == 4) ? rest.z : rest.w;
        float b = a + len;
        if (ph >= a && ph < b)
        {
            bool on = (i - 2 * (i / 2)) == 0;   // even index = an ON run
            if (on)                       { dS = ph - a; dE = b - ph; }
            else if (ph - a <= b - ph)    { dE = a - ph; }   // just past the previous run's END
            else                          { dS = ph - b; }   // just before the next run's START
        }
        a = b;
    }
    return float2(dS, dE);
}

float DashTrimMask(float sTrim, float sDash, float perimeter, float dashOn, float dashGap, float dashOffset,
    float trimStart, float trimEnd, float dPerp, float halfW, float capFlags, float4 dashRest)
{
    int dashStartCap = int(fmod(capFlags, 8.0));
    int dashEndCap   = int(fmod(floor(capFlags / 8.0), 8.0));
    int startCap     = int(fmod(floor(capFlags / 64.0), 8.0));
    int endCap       = int(fmod(floor(capFlags / 512.0), 8.0));   // base-8 mask: else the JOIN above them leaks in

    // Distance to the trim window's two ends. Untrimmed = "nowhere near", so the dash edges are the only ends there is.
    float tS = 1e9;
    float tE = 1e9;
    if (trimStart > 0.0 || trimEnd < 1.0)
    {
        tS = sTrim - trimStart * perimeter;
        tE = trimEnd * perimeter - sTrim;
    }

    // Distance to the two ends of the dash run this fragment belongs to. Inside a run it is that run; inside a gap it is
    // whichever neighbouring run is nearer, so a convex cap still bulges out into the gap it faces.
    float dS = 1e9;
    float dE = 1e9;
    int dashCount = int(floor(capFlags / 32768.0));   // how many runs the pattern has; 2 is the plain ON/GAP
    float period = DashPatternLength(dashOn, dashGap, dashRest, dashCount);
    if (dashOn > 0.0 && period > 0.0)
    {
        float ph = frac((sDash + dashOffset) / period) * period;   // 0..period
        float2 de = DashPiece(ph, dashOn, dashGap, dashRest, dashCount);
        dS = de.x;
        dE = de.y;
    }

    float sdStart = (dS < tS) ? dS + CapReach(dashStartCap, dPerp, halfW)
                              : tS + CapReach(startCap, dPerp, halfW);
    float sdEnd   = (dE < tE) ? dE + CapReach(dashEndCap, dPerp, halfW)
                              : tE + CapReach(endCap, dPerp, halfW);
    return saturate(min(sdStart, sdEnd) + 0.5);
}

// Same as DashTrimMask, but the TRIM window wraps the fragment's signed arc offset to [-P/2, P/2] so a CONVEX cap at the
// contour seam (trimStart 0 -> start at s=0) bulges into the gap on the OTHER side of s=0 instead of clipping flat. Kept
// SEPARATE from DashTrimMask on purpose: this wrapped form miscompiled the driver's GRADIENT/pattern/fractal pixel-shader
// objects (they inline the helper too) into a device-lost, while the SOLID rect/ellipse stroke shaders compile it fine -
// so ONLY those two call it; the fill shaders stay on the plain DashTrimMask. Do NOT re-merge them.
float DashTrimMaskCapped(float sTrim, float sDash, float perimeter, float dashOn, float dashGap, float dashOffset,
    float trimStart, float trimEnd, float dPerp, float halfW, float capFlags, float4 dashRest)
{
    int dashStartCap = int(fmod(capFlags, 8.0));
    int dashEndCap   = int(fmod(floor(capFlags / 8.0), 8.0));
    int startCap     = int(fmod(floor(capFlags / 64.0), 8.0));
    int endCap       = int(fmod(floor(capFlags / 512.0), 8.0));

    float windowOpen = 1.0;
    float tS = 1e9;
    float tE = 1e9;
    if (trimStart > 0.0 || trimEnd < 1.0)
    {
        float a = trimStart * perimeter;
        float b = trimEnd * perimeter;
        windowOpen = (b > a) ? 1.0 : 0.0;
        float centre = (a + b) * 0.5;
        float halfLen = (b - a) * 0.5;
        float ds = sTrim - centre;
        ds -= perimeter * floor(ds / perimeter + 0.5);   // wrapped: a convex cap at the seam bulges across s=0
        tS = halfLen + ds;
        tE = halfLen - ds;
    }

    float dS = 1e9;
    float dE = 1e9;
    int dashCount = int(floor(capFlags / 32768.0));
    float period = DashPatternLength(dashOn, dashGap, dashRest, dashCount);
    if (dashOn > 0.0 && period > 0.0)
    {
        float ph = frac((sDash + dashOffset) / period) * period;
        float2 de = DashPiece(ph, dashOn, dashGap, dashRest, dashCount);
        dS = de.x;
        dE = de.y;
    }

    float sdStart = (dS < tS) ? dS + CapReach(dashStartCap, dPerp, halfW)
                              : tS + CapReach(startCap, dPerp, halfW);
    float sdEnd   = (dE < tE) ? dE + CapReach(dashEndCap, dPerp, halfW)
                              : tE + CapReach(endCap, dPerp, halfW);
    return windowOpen * saturate(min(sdStart, sdEnd) + 0.5);
}

// The corner this fragment belongs to, out of the four (x = TL, y = TR, z = BR, w = BL - the CPU CornerRadius order).
// SDF space has y DOWN (the quad's corner 0 is the TOP-left), so a negative Local.y is the top half. Every rounded-rect
// helper below picks its radius through this one function, which is what keeps the four corners INDEPENDENT: the field
// stays continuous across the axes because the +r/-r of the offset cancels on a straight edge, so neighbouring corners
// never have to agree.
float CornerRadiusAt(float2 p, float4 radii)
{
    return p.x < 0.0 ? (p.y < 0.0 ? radii.x : radii.w)
                     : (p.y < 0.0 ? radii.y : radii.z);
}

// Arc-length `s` (device px) of the point on the ROUNDED-RECT contour nearest `p`, and the perimeter. Exact/closed-form.
// Traversal CCW from the start of the top-right arc: TR arc, top edge, TL arc, left edge, BL arc, bottom edge, BR arc,
// right edge. (Start point is arbitrary for dashes; dashOffset shifts the phase.)
float RoundRectArc(float2 p, float2 b, float4 radii, out float perimeter)
{
    // Dashes are measured on the CENTRELINE. In the corner, s = corner-start + phi*r uses the fragment's ANGLE (phi),
    // not its radius, so it's already uniform across the stroke width AND continuous with the edges - the dash flows
    // around the corner exactly like on a straight edge (and like the ellipse). No per-corner "single state" is needed.
    // With four INDEPENDENT radii every arc and every edge has its own length, so the traversal is accumulated corner by
    // corner instead of multiplying one quarter-arc by four. Order (CCW in SDF space, y down): BR arc, bottom edge,
    // BL arc, left edge, TL arc, top edge, TR arc, right edge.
    float rTL = radii.x, rTR = radii.y, rBR = radii.z, rBL = radii.w;
    const float HALF_PI = 1.5707963268;
    float aBR = HALF_PI * rBR, aBL = HALF_PI * rBL, aTL = HALF_PI * rTL, aTR = HALF_PI * rTR;
    float eBottom = 2.0 * b.x - rBR - rBL;
    float eLeft   = 2.0 * b.y - rBL - rTL;
    float eTop    = 2.0 * b.x - rTL - rTR;
    float eRight  = 2.0 * b.y - rTR - rBR;
    perimeter = aBR + eBottom + aBL + eLeft + aTL + eTop + aTR + eRight;

    float sBottom = aBR;                      // where each segment STARTS along the traversal
    float sBL     = sBottom + eBottom;
    float sLeft   = sBL + aBL;
    float sTL     = sLeft + eLeft;
    float sTop    = sTL + aTL;
    float sTR     = sTop + eTop;
    float sRight  = sTR + aTR;

    float r = CornerRadiusAt(p, radii);
    float bx = b.x - r, by = b.y - r;
    float ax = abs(p.x), ay = abs(p.y);
    float cx = ax - bx, cy = ay - by;

    if (cx > 0.0 && cy > 0.0)                                   // corner -> arc-length by angle (phi), uniform + continuous
    {
        float phi = atan2(cy, cx);
        float s;
        if      (p.x >= 0.0 && p.y >= 0.0) s = phi * rBR;
        else if (p.x <  0.0 && p.y >= 0.0) s = sBL + (HALF_PI - phi) * rBL;
        else if (p.x <  0.0 && p.y <  0.0) s = sTL + phi * rTL;
        else                               s = sTR + (HALF_PI - phi) * rTR;
        return s;
    }
    // Classify by the NEAREST edge, not just cx/cy vs the CORNER radius r: a thick stroke reaches MORE than r inside, so
    // the inner part of a top/bottom-edge stroke has cy<=0 (inside the inner box) yet is still nearest the horizontal
    // edge. Deciding by cx/cy alone mis-routed that inner sliver (width halfW-r) to the VERTICAL-edge arc-length -> a thin
    // mis-dashed line on the inner edge that only showed when halfW > r (thick stroke / small corner).
    // An edge is anchored at the corner it STARTS from, which is not necessarily the corner nearest this fragment.
    bool horizontal = (cx <= 0.0) && (cy > 0.0 || (b.y - ay) < (b.x - ax));
    float s;
    if (horizontal) s = (p.y >= 0.0) ? sBottom + ((b.x - rBR) - p.x) : sTop + (p.x + (b.x - rTL));
    else            s = (p.x >= 0.0) ? sRight + (p.y + (b.y - rTR)) : sLeft + ((b.y - rBL) - p.y);
    return s;
}

// Signed distance (device px) to a rounded rect with a selectable outer-corner JOIN: 0 = miter (sharp, Chebyshev outer
// corner), 1 = bevel (45-deg chamfer), 2 = round (Euclidean, the natural offset). Straight edges are identical across all
// three; only the corner (both q>0) differs. This is what gives stroke-join parity with the pen (PenLineJoin).
float SdRoundRectJoin(float2 p, float2 b, float4 radii, int joinType)
{
    float r = CornerRadiusAt(p, radii);
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

// Radius of curvature of the ellipse at the fragment's parametric angle: |r'|^3 / (rx*ry) for x = rx cos t, y = ry sin t.
// Kept as its own single-expression helper - the arc functions above have early returns, and giving one of THOSE a second
// out-parameter made the NVIDIA NVVM compiler AV in vkCreateShadersEXT (see the note on DashTrimMaskCapped).
float EllipseCurvRadius(float2 p, float2 h)
{
    float t = atan2(p.y * h.x, p.x * h.y);
    float st = sin(t), ct = cos(t);
    float e = sqrt(h.x * h.x * st * st + h.y * h.y * ct * ct);
    return (e * e * e) / max(h.x * h.y, 1e-6);
}

// Same for a rounded rect: the corner arcs bend with the corner radius, the four edges are straight.
float RoundRectCurvRadius(float2 p, float2 b, float4 radii)
{
    float r = CornerRadiusAt(p, radii);
    float2 q = abs(p) - (b - r);
    return (q.x > 0.0 && q.y > 0.0 && r > 0.5) ? r : 1e9;
}

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
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = min(input.Radii, float4(lim, lim, lim, lim));   // a corner cannot exceed half the smaller side
    int joinType = int(fmod(floor(input.Stroke1.w / 4096.0), 8.0));  // 0 miter, 1 bevel, 2 round
    float d = SdRoundRectJoin(input.Local, input.Half, r4, joinType);

    // A BORDER (per-side thickness) instead of a pen: fill and ring composite from the two outlines in one go. Told apart
    // by Inset alone - a pen and a border never ride in the same instance (RectBatchCollector.WantsBatch).
    if (any(input.Inset > 0.0))
    {
        return CompositeFillBorder(d, input.Local, input.Half, r4, input.Inset, joinType,
                                   input.Color, input.StrokeColor, input.Crisp);
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
    return CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask, input.Crisp);
}

// ---- InstancedFill pass: general retained geometry instancing (§4h/§4j) --------------------------------------------
// A SHARED local mesh (bound as the only vertex buffer) drawn instanceCount times; each instance's world transform and
// colour are fetched from this StructuredBuffer by SV_InstanceID. So N identical shapes = ONE instanced draw, and a
// move/resize/recolour is a patch of one record - no per-frame re-record. Matches Retained/GeometryInstance.cs.
struct GeometryInstance
{
    float4x4 Local;   // element local -> SLOT space. Matches Matrix4x4F Local.
    float4 Color;     // straight-alpha RGBA (element/brush opacity folded into .w by the producer)
    float4 Params;    // .x = transform-table slot; .yzw spare
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
    // local -> slot space -> world. The slot matrix lives in the transform table, so a node move rewrites 64 bytes there
    // and every instance under it follows without this buffer being touched (same scheme as the SDF batches).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), inst.Local), nodes[(uint)inst.Params.x].World);
    FillPSInput o;
    o.Position = mul(world, Projection);
    o.Color = inst.Color;
    return o;
}

[shader("fragment")]
float4 InstancedFillPS(FillPSInput input) : SV_Target
{
    return input.Color;   // solid fill (straight alpha, drawn with AlphaBlend); the fringe pass below feathers its edge
}

// ---- InstancedFringe pass: the analytic-AA fringe of the SAME instances, as one instanced draw --------------------
// The ring (Rendering/FringeGeometry.cs) is scale-free - a contour point plus, on the outer edge, the two adjacent edge
// DIRECTIONS - so every instance of a mesh shares ONE ring buffer and reads its own transform/colour from the SAME
// GeometryInstance buffer the body pass used. That is what replaces the old per-element fringe draw (which cost one
// pipeline switch + one uniform matrix per element and dominated the frame). The width is applied HERE, in device
// pixels, so it stays one pixel at any zoom.
float2 ViewportSize;      // render target size in DEVICE pixels - the NDC <-> pixel basis for the fringe offset
float FringePixels;       // fringe width in DEVICE pixels

struct FringeVertex
{
    float2 Position : POSITION;
    float2 Dir0     : TEXCOORD0;   // incoming edge direction, Winding folded into its sign; zero on the contour itself
    float2 Dir1     : TEXCOORD1;   // outgoing edge direction
};

struct FringePSInput
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float  Coverage : TEXCOORD0;
};

// The ring's vertex, expanded. Shared by every fringe pass (solid / pattern / gradient): they differ only in WHICH
// record supplies the matrix and the colour, and the expansion itself must stay one definition - it is the thing that
// makes the ring exactly one device pixel wide. `coverage` comes out 1 on the contour and 0 on the outer edge.
float4 ExpandFringe(FringeVertex v, float4x4 m, out float coverage)
{
    float4 clip = mul(float4(v.Position, 0.0, 1.0), m);
    float outer = dot(v.Dir0, v.Dir0) + dot(v.Dir1, v.Dir1);
    if (outer > 0.0)
    {
        // Edge directions -> PIXEL space (w = 0 drops the translation), so the miter is perpendicular to the edge as the
        // rasterizer sees it - correct under anisotropic scale, skew and rotation alike.
        float2 halfVp = max(ViewportSize, float2(1.0, 1.0)) * 0.5;
        float w = max(clip.w, 1e-6);
        float2 e0 = mul(float4(v.Dir0, 0.0, 0.0), m).xy / w * halfVp;
        float2 e1 = mul(float4(v.Dir1, 0.0, 0.0), m).xy / w * halfVp;
        e0 = length(e0) > 1e-9 ? normalize(e0) : float2(0.0, 0.0);
        e1 = length(e1) > 1e-9 ? normalize(e1) : float2(0.0, 0.0);
        float2 n0 = float2(-e0.y, e0.x);
        float2 n1 = float2(-e1.y, e1.x);
        float2 sum = n0 + n1;
        float2 miter = length(sum) > 1e-4 ? normalize(sum) : n0;   // a 180-degree reversal has no bisector: use one normal
        float denom = max(dot(miter, n0), 0.25);                   // clamp the corner spike to <= 4x
        clip.xy += miter * (FringePixels / denom) / halfVp * w;
    }
    coverage = outer > 0.0 ? 0.0 : 1.0;
    return clip;
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
    o.Color = inst.Color;
    o.Coverage = coverage;
    return o;
}

[shader("fragment")]
float4 InstancedFringePS(FringePSInput input) : SV_Target
{
    float4 c = input.Color;
    c.a *= saturate(input.Coverage);   // 1 at the contour -> 0 at the outer edge = analytic edge coverage
    return c;
}

// ---- RectBatchInstanced: the SAME SDF rounded-rect batch, but per-instance RectItem read from a BDA STORAGE buffer by
// SV_InstanceID (like InstancedFill) instead of a per-instance VERTEX buffer. This lets the instance data be RETAINED +
// patched only over its dirty range (no full re-upload each frame) and, with tiles baked in a stable space, a scroll
// updates one offset uniform instead of re-baking N instances. Plain (no vertex semantics) struct matching the CPU
// RectItem's Vector4F layout; the quad still comes from SV_VertexID. Pixel shader is the shared RectBatchPS.
struct RectData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x = LARGEST corner radius; .y = transform-table slot; .z = no-fringe flag; .w unused
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Color;        // straight RGBA, opacity folded in
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke); the BORDER's colour when Inset is non-zero
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
    float4 Inset;        // border thickness per side in device px: x left, y top, z right, w bottom (all 0 = no border)
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
    float slotAlpha = nodes[(uint)item.Params.y].Params.x;
    o.Color  = float4(item.Color.rgb, item.Color.a * slotAlpha);
    o.StrokeColor = float4(item.StrokeColor.rgb, item.StrokeColor.a * slotAlpha);
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    // Each side follows the axis it sits on: left/right take the horizontal pixel scale, top/bottom the vertical. `iso`
    // (the smaller of the two) is the right answer for a stroke WIDTH, which has no axis, and the wrong one here - under
    // a squashed slot a border would come out thicker on one pair of sides than it was asked for.
    o.Inset = item.Inset * float4(px.x, px.y, px.x, px.y);
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
};

// A REGULAR POLYGON, in the same family as the ellipse and for the same reason: it is one field, and the only thing that
// separates a triangle from a circle is how many corners you ask for (large N is a circle to the pixel). Exact, so it
// self-anti-aliases and strokes like everything else here.
//
// `p` and `half` are the same as the ellipse's, and the shape is INSCRIBED in that box: the maths runs in normalised
// space (circumradius 1) and the distance is scaled back by the smaller half-axis - the same first-order treatment of an
// anisotropic box that SdEllipse gives, so a squashed polygon behaves like a squashed circle.
//
// The first vertex sits at angle 0, on the +x axis, because that is where the tessellator puts it (Shapes.Polygon walks
// 2*pi*i/N from there) - a polygon that batches must be the same polygon that the fallback tessellates, rotation
// included.
float SdRegularPolygon(float2 p, float2 half, float n, float startAngle)
{
    float2 h = max(half, float2(1e-6, 1e-6));
    float2 q = p / h;                       // normalised: the shape is the unit circumradius polygon

    // Turn the shape by rolling the SAMPLE the other way - and do it HERE, in normalised space, where the corners sit on
    // a unit circle. Rotating the fragment before the divide would rotate the box too, so a squashed hexagon would swing
    // out of the slot it is inscribed in; rotating after it moves the corners along the ellipse the box inscribes, which
    // is exactly what Shapes.Polygon does with the same angle (radii * cos/sin of start + 2*pi*i/N).
    float ca = cos(startAngle);
    float sa = sin(startAngle);
    q = float2(q.x * ca + q.y * sa, q.y * ca - q.x * sa);

    float an = 3.14159265 / n;              // half of one sector

    // Fold into a single half-sector, measured from the +x axis so that vertex 0 lands on it. What is left is a point
    // whose x runs along the apothem and whose y is its (positive) offset along the edge.
    float a = atan2(q.y, q.x);
    float wrapped = a - 2.0 * an * floor(a / (2.0 * an) + 0.5);   // into [-an, an], centred on a VERTEX
    float2 folded = length(q) * float2(cos(wrapped), abs(sin(wrapped)));

    // Distance to the edge running from that vertex to the next: a segment, so a point past the vertex measures to the
    // vertex itself rather than to the edge's infinite line.
    float2 v0 = float2(1.0, 0.0);
    float2 v1 = float2(cos(2.0 * an), sin(2.0 * an));
    float2 e = v1 - v0;
    float2 w = folded - v0;
    float2 d = w - e * saturate(dot(w, e) / max(dot(e, e), 1e-9));

    // Inside is the side the centre is on. cross(e, w) changes sign exactly across the edge's line.
    float side = (e.x * w.y - e.y * w.x) > 0.0 ? -1.0 : 1.0;
    return length(d) * side * min(h.x, h.y);
}

// The ANGULAR CUT that turns a whole ellipse into a sector or a segment, as a signed distance (device px, negative
// inside) to the STRAIGHT part of that shape's outline. Intersected with the ellipse's own field it gives the whole
// shape - fill, anti-aliasing and stroke all follow from the combined distance, which is why neither needs a mesh, a
// second pass or a collector of its own.
//
// Two things have to be right, and they are different things:
//  - WHICH fragments are in: decided by the ellipse's own PARAMETRIC angle (x = rx cos t, y = ry sin t), because that is
//    the angle the tessellator sweeps. For a circle it equals the geometric angle; for anything else it does not, and
//    using atan2(y, x) would put the cut in a visibly different place than the per-unit path puts it.
//  - HOW FAR the fragment is from the cut: measured in device px against the straight edges themselves - the two radii
//    of a sector, the chord of a segment - so the fade across them is one pixel wide like every other edge here.
// `kind`: 1 = sector (closed through the centre), 2 = edge-to-edge (closed by the chord). Anything else = no cut.
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

// THE shape a BRUSH pass paints on. Three passes (gradient, pattern, texture) each draw a rounded rect, an ellipse or a
// regular polygon and differ only in where the COLOUR comes from - so which shape that is gets stated once, here, rather
// than three times in three pixel shaders.
//
// A polygon carries no corner radii, so its own numbers ride in exactly that field: .x corners, .y start angle in
// radians, .z ring thickness in device px. The shape selector is the pass's own (a negative baked radius for the pattern
// and texture passes, Geom1.z for the gradient one), resolved to 0 rect / 1 ellipse / 2 polygon before the call.
float BrushShapeDistance(float2 p, float2 half, float4 radii, int joinType, float shape)
{
    // Branch-FREE, and not as a matter of taste: a ?: in the textured pass has device-lost form on this driver (see
    // TexRectPS), and this function is now shared by that pass. Both other distances are cheap; the polygon's trig is
    // the only real cost, and only shapes that ask for it reach this function at all.
    float dPoly = SdRegularPolygon(p, half, max(radii.x, 3.0), radii.y);
    dPoly = lerp(dPoly, max(dPoly, -(dPoly + radii.z)), step(0.0001, radii.z));   // a RING, exactly as in pass Polygon

    float dRect = SdRoundRectJoin(p, half, radii, joinType);
    float dEllipse = SdEllipse(p, half);
    return lerp(lerp(dRect, dEllipse, step(0.5, shape)), dPoly, step(1.5, shape));
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
    return CompositeFillStrokeSplit(d, dStroke, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, mask, 0.0);
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
    o.Color  = item.Color;
    o.StrokeColor = item.StrokeColor;
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    o.Arc = item.Arc;   // angles are angles: no pixel scale applies to them
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

    float slotAlpha = nodes[(uint)item.Params.x].Params.x;
    o.Color  = float4(item.Color.rgb, item.Color.a * slotAlpha);
    o.StrokeColor = float4(item.StrokeColor.rgb, item.StrokeColor.a * slotAlpha);
    o.Stroke0 = float4(widthPx, item.Stroke0.y, item.Stroke0.z * iso, item.Stroke0.w * iso);
    o.Stroke1 = float4(item.Stroke1.x * iso, item.Stroke1.y, item.Stroke1.z, item.Stroke1.w);
    o.Dash = item.Dash * iso;
    o.Shape = float3(item.Params.y, item.Params.z * iso, item.Params.w);   // an ANGLE does not scale with the DPI
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

    return CompositeFillStroke(d, input.Color, input.StrokeColor, input.Stroke0.x, input.Stroke0.y, 1.0, 0.0);
}

// ---- GradientRect: the SAME SDF rounded-rect batch, but the FILL is a LINEAR or RADIAL gradient (up to 8 stops)
// evaluated per fragment, instead of one solid colour. Per-instance GradientRectData from the BDA storage buffer by
// SV_InstanceID; the pixel shader reads the record (BDA) to get the gradient geometry + stops. Solid rects stay in the
// cheaper RectBatch untouched - this is a sibling pass only rects with a gradient fill route to. Matches CPU
// GradientRectItem. Fill+stroke are composited by the shared CompositeFillStroke, so a gradient tile still strokes.
struct GradientRectData
{
    float4 Bounds;       // world x, y, w, h
    float4 Params;       // .x LARGEST corner radius, .y type (1 linear/2 radial/3 conic), .z stop count, .w spread (0 pad/1 reflect/2 repeat)
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Geom0;        // LOCAL 0..1: linear (startXY, endXY) | radial (centerXY, radiusXY)
    float4 Geom1;        // radial focal (originXY, _, _); unused for linear
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
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
    float2 Half     : TEXCOORD1;   // rect half-size (device px)
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read GradientRectData in the PS for its gradient
    nointerpolation float Scale : TEXCOORD4;   // slot unit -> device px: the PS re-reads the stroke record, which is
                                               // baked in slot units, and has to match a pixel-space SDF
};

[shader("vertex")]
GradPSInput GradientRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    GradientRectData* items = (GradientRectData*)InstancesAddress;
    GradientRectData it = items[instanceId];

    GradPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    // Node-local -> world via the transform table (slot in Geom1.w - Geom1.z is the shape flag; slot 0 = identity),
    // and the SDF inputs in DEVICE PIXELS, same as RectBatchInstancedVS. The gradient uv is Local/Half - a RATIO - so
    // the change of unit leaves the gradient itself untouched; the STROKE record is not a ratio, hence Scale.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Geom1.w].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float widthPx = it.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = it.Radii * iso;
    o.InstId = instanceId;
    o.Scale  = iso;
    return o;
}

// ONE gradient pixel shader for BOTH shapes (rounded rect + ellipse), branched on the per-instance shape flag (Geom1.z:
// >=0.5 = ellipse). A single gradient shader instead of two - so there is not a second near-identical BDA-reading shader.
[shader("fragment")]
float4 GradientPS(GradPSInput input) : SV_Target
{
    GradientRectData* items = (GradientRectData*)InstancesAddress;
    GradientRectData it = items[input.InstId];

    float shape = it.Geom1.z;          // 0 rect, 1 ellipse, 2 regular polygon (its numbers ride in Radii)
    bool ellipse = shape >= 0.5 && shape < 1.5;
    bool polygon = shape >= 1.5;
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, step(1.5, shape));
    int joinType = int(fmod(floor(it.Stroke1.w / 4096.0), 8.0));
    float d = BrushShapeDistance(input.Local, input.Half, r4, joinType, shape);

    float2 uv = input.Local / max(input.Half * 2.0, float2(1e-4, 1e-4)) + 0.5;   // 0..1 across the bounds
    int packedW = int(it.Params.w + 0.5);                       // Params.w packs spread (low 3 bits) + interp mode (>> 3)
    float gt = GradSpread(GradParam(it, uv), packedW & 7);
    // Wrap-aware AA width: at a conic/repeat seam gt jumps 1->0 so fwidth(gt) spikes to ~1 (the whole gradient collapses to
    // hard-stop ramps -> a coloured line). Shifting by half a turn moves the discontinuity to the far side, so min() picks
    // the TRUE small derivative everywhere. Harmless for linear/radial (min keeps the real value).
    float4 grad = GradColor(it, gt, min(fwidth(gt), fwidth(frac(gt + 0.5))), packedW >> 3);
    // MESH gradient (type 4): four CORNER colours blended bilinearly across the shape - no axis, no stops, so the
    // gradient maths above has nothing meaningful to chew on for it (GradientBake packs zero geometry). The corners ride
    // the stop slots. Selected BRANCH-FREE: this pass has a history of device-losing on a ?:, so both are computed.
    float4 mesh = lerp(lerp(it.Stop0, it.Stop1, uv.x), lerp(it.Stop2, it.Stop3, uv.x), uv.y);
    float4 fill = lerp(grad, mesh, step(3.5, it.Params.y));

    // The stroke record is baked in SLOT units; the SDF above is in device pixels, so its LENGTHS convert (align, trim
    // and the packed cap/join flags are unitless). Arc length `perim` already comes out in pixels, so dashes match.
    float sc = input.Scale;
    float widthPx = it.Stroke0.x * sc;
    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = widthPx * 0.5;
        float perim;
        float s = ellipse ? EllipseArc(input.Local, input.Half, perim)
                          : RoundRectArc(input.Local, input.Half, r4, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        float capScl = ArcCapScale(ellipse ? EllipseCurvRadius(input.Local, input.Half) : RoundRectCurvRadius(input.Local, input.Half, r4), dPerp);
        mask = DashTrimMask(s, s, perim, it.Stroke0.z * sc, it.Stroke0.w * sc, it.Stroke1.x * sc, it.Stroke1.y,
                            it.Stroke1.z, dPerp * capScl, halfW * capScl, it.Stroke1.w, it.Dash * sc);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, widthPx, it.Stroke0.y, mask, 0.0);
}

// ---- GradientFill: general instanced geometry (a shared tessellated mesh drawn N times) whose FILL is a LINEAR/RADIAL
// gradient - the gradient sibling of InstancedFill. Per-instance GradientGeometryInstance from a BDA storage buffer by
// SV_InstanceID. Mirrors the PROVEN-STABLE unified GradientPS profile: the PIXEL shader re-reads the record by BDA and
// only a FEW interpolators cross the stage (the fragment's local mesh position + the instance id). Passing the whole
// gradient (15 float4) as interpolators was a much heavier shader signature and tripped the driver's shader-object flake
// far more often; BDA-in-PS with a light signature is what the stable rect/ellipse gradient already does.
struct GradGeomData
{
    float4x4 Local;      // element local -> SLOT space (the slot's matrix is applied on top, from the transform table)
    float4 Params;       // .x type (1 linear/2 radial), .y spread, .z stop count, .w interp mode (0 sRGB/1 OKLab)
    float4 Geom0;        // LOCAL 0..1: linear (startXY, endXY) | radial (centerXY, radiusXY)
    float4 Geom1;        // radial focal (originXY, _); .w = transform-table slot
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
    // local -> slot space -> world, as InstancedFillVS: the slot matrix lives in the transform table, so a node move
    // rewrites 64 bytes there and every instance under it follows without this buffer being touched.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), it.Local), nodes[(uint)it.Geom1.w].World);

    GradFillPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    return o;
}

// The gradient colour at a LOCAL mesh position, for instanced geometry. The fill and its analytic-AA fringe both call
// this, so the ring is coloured by exactly the same gradient as the body it feathers.
float4 GradGeomColor(GradGeomData it, float2 local)
{
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

    float2 uv = (local - it.LocalBounds.xy) / max(it.LocalBounds.zw, float2(1e-4, 1e-4));
    float gt = GradSpread(GradParam(gd, uv), int(gd.Params.w));
    float4 grad = GradColor(gd, gt, min(fwidth(gt), fwidth(frac(gt + 0.5))), int(it.Params.w));   // wrap-aware AA (conic/repeat seam)
    // MESH (type 4) here too: a mesh brush has NO axis geometry, so without this branch the maths above runs on zeros and
    // walks the stop table with a meaningless parameter. Same branch-free select as the rect pass.
    float4 mesh = lerp(lerp(gd.Stop0, gd.Stop1, uv.x), lerp(gd.Stop2, gd.Stop3, uv.x), uv.y);
    return lerp(grad, mesh, step(3.5, gd.Params.y));
}

[shader("fragment")]
float4 GradientFillPS(GradFillPSInput input) : SV_Target
{
    GradGeomData* items = (GradGeomData*)InstancesAddress;
    return GradGeomColor(items[input.InstId], input.Local);
}

// The analytic-AA fringe of those gradient instances: same shared ring, same instance buffer, one draw. Unlike the
// solid and pattern fringes it needs its own PS - the ring's colour varies per fragment, so it cannot be resolved in
// the vertex stage and handed over as one colour.
struct GradFringePSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // LOCAL mesh position -> the gradient uv
    float  Coverage : TEXCOORD1;
    nointerpolation uint InstId : TEXCOORD2;
};

[shader("vertex")]
GradFringePSInput InstancedGradientFringeVS(FringeVertex v, uint instanceId : SV_InstanceID)
{
    GradGeomData* items = (GradGeomData*)InstancesAddress;
    GradGeomData it = items[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 m = mul(mul(it.Local, nodes[(uint)it.Geom1.w].World), Projection);

    GradFringePSInput o;
    float coverage;
    o.Position = ExpandFringe(v, m, coverage);
    o.Local = v.Position;
    o.Coverage = coverage;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 InstancedGradientFringePS(GradFringePSInput input) : SV_Target
{
    GradGeomData* items = (GradGeomData*)InstancesAddress;
    float4 c = GradGeomColor(items[input.InstId], input.Local);
    c.a *= saturate(input.Coverage);   // 1 at the contour -> 0 at the outer edge
    return c;
}

// ---- Pattern batch: the SAME SDF rounded-rect (self-AA shape + the shared stroke), but the FILL is a PROCEDURAL two-colour
// PATTERN (checkerboard/stripes/dots/grid) evaluated per fragment - resolution-independent, no texture. Per-instance
// PatternRectData from a BDA storage buffer by SV_InstanceID; the PS re-reads the record (light interpolator signature, like
// GradientPS) and mixes Color1/Color2 by the pattern. Solid/gradient rects stay in their own passes.
struct PatternRectData
{
    float4 Bounds;       // NODE-local x, y, w, h (world for slot-0 legacy bakes - identity matrix)
    float4 Params;       // .x corner radius, .y pattern type (0 checker/1 stripes/2 dots/3 grid/4 FBM noise), .z cell (px), .w slot
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Color1;       // straight RGBA, opacity folded
    float4 Color2;       // straight RGBA, opacity folded
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
    float4 Noise;        // FBM noise (type 4 only): x octaves, y seed, z lacunarity, w gain
    float4 Color3;       // optional MID colour for a 3-colour noise gradient-map (Color1->Color3->Color2); .w==0 = off
    float4 Anim;         // .x = offset subtracted from the clock while animating, .y = the phase held while paused
};

struct PatternPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read PatternRectData in the PS
    nointerpolation float Scale : TEXCOORD4;   // slot unit -> device px. The PS re-reads the record, whose stroke AND
                                               // cell size (PatternBrush.CellSize / NoiseBrush.Scale, one field) are
                                               // absolute lengths in slot units - they can't ride a ratio like uv.
};

[shader("vertex")]
PatternPSInput PatternRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    PatternRectData* items = (PatternRectData*)InstancesAddress;
    PatternRectData it = items[instanceId];

    PatternPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    // SDF inputs in DEVICE PIXELS, same scheme as RectBatchInstancedVS (see SlotPixelScale).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.w].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float widthPx = it.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = it.Radii * iso;
    o.InstId = instanceId;
    o.Scale  = iso;
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
// The phase this instance flows at. The clock is SHARED and keeps running while any brush animates, so an animating
// instance rides it minus its own offset (anim.x), and a paused one holds the phase it stopped at (anim.y). Reading the
// raw clock instead makes a pause leak: the field would still advance while stopped, and resuming would jump it forward
// by the whole length of the pause. Branch-free: a ?: in this pass has device-lost form on this driver.
float NoisePhase(float octavesSigned, float2 anim)
{
    return lerp(anim.y, Time - anim.x, step(octavesSigned, -0.0001));
}

float PatternMix(int type, float2 p, float cell, float4 noise, float2 anim)
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
        float phase = NoisePhase(noise.x, anim);
        float2 np = g + noise.y;                                         // base noise domain + seed offset
        float n = fbm(np, oct, max(noise.z, 1.0), noise.w, basis, phase);   // Color1 (low) -> Color2 (high); phase drives flow
        return saturate(n * 0.5 + 0.5);
    }
    if (type == 10 || type == 11)   // ridged (10) / turbulence (11): FBM folds over simplex, already ~[0,1]
    {
        int mode = 0;                              // turbulence
        if (type == 10) mode = 1;                  // ridged
        int oct = int(abs(noise.x));
        float phase = NoisePhase(noise.x, anim);
        float2 np = g + noise.y;
        float n = fbmFold(np, oct, max(noise.z, 1.0), noise.w, mode, phase);
        if (type == 11) n = n * 1.6;               // turbulence is dimmer (averaged |noise|) - lift it for contrast
        return saturate(n);
    }
    if (type == 12)   // Voronoi BORDER network (iq Xd23Dh): thin bright cell walls, morphing under Animate
    {
        float ph = NoisePhase(noise.x, anim);
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
        float time = NoisePhase(it.Noise.x, it.Anim.xy);
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
        float k = PatternMix(ptype, pTopLeft, it.Params.z, it.Noise, it.Anim.xy);
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

// ---- TEXTURED rounded rect: the first fill of this batch whose colour is SAMPLED rather than computed. Deliberately the
// SHORTEST pixel shader here - SDF, one uv wrap, one sample, one multiply - because this driver flakes on
// vkCreateShadersEXT the moment a pass grows (see the MeshGradient note above). No stroke: a textured fill with a pen
// falls back to the per-unit path rather than dragging the stroke machinery in.
struct TexRectData
{
    float4 Bounds;     // NODE-local x, y, w, h - the SHAPE, which never shrinks with the picture
    float4 Params;     // .x corner radius, .y transform slot, .z repeat flag, .w mirror flags (1 = X, 2 = Y, 3 = both)
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Tile;       // tile grid over the bounds: tiles per axis (.xy), grid origin in tiles (.zw)
    float4 Rotation;   // 2x2 mapping a fragment back into the unturned grid, row-major (identity = 1,0,0,1)
    float4 Drawn;      // the content's rect inside ONE tile: offsetXY, scaleXY, both in 0..1 of the tile
    float4 UvRect;     // sub-rectangle of the source: x, y, w, h (normalised)
    float4 Tint;       // multiplied into the sample, straight RGBA
};

struct TexPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    nointerpolation uint InstId : TEXCOORD3;
};

[shader("vertex")]
TexPSInput TexRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    TexRectData* items = (TexRectData*)InstancesAddress;
    TexRectData it = items[instanceId];

    TexPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    // One pixel of outset so the analytic edge has room to feather (no stroke, so no width to account for).
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (1.0 / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0);
    o.Radii = it.Radii * iso;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 TexRectPS(TexPSInput input) : SV_Target
{
    TexRectData* items = (TexRectData*)InstancesAddress;
    TexRectData it = items[input.InstId];

    // A NEGATIVE baked corner radius is the ELLIPSE shape flag (a rect passes radius >= 0) - same signal the pattern
    // pass uses. Branch-free (a ?: in this pass has device-lost form on this driver): both distances, picked by a step.
    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    // round join: the plain Euclidean offset, no stroke here
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    // 0..1 across the shape -> TILE space -> the content's rect inside one tile -> the source's sub-rectangle. The
    // REPEAT is done here with frac() rather than by a wrapping sampler: the sampler would wrap the WHOLE texture, and
    // a slice of it needs its own strip wrapped.
    float2 t = input.Local / max(input.Half * 2.0, float2(1e-4, 1e-4)) + 0.5;
    // Back into the UNTURNED grid: one 2x2, with the inverse, the aspect and the turn centre already folded in.
    float2 g = float2(t.x * it.Rotation.x + t.y * it.Rotation.y, t.x * it.Rotation.z + t.y * it.Rotation.w);
    float2 n = g * it.Tile.xy - it.Tile.zw;
    // A SINGLE copy never wraps: frac() would send its far edge (n = 1) back to 0, drawing one column of the opposite
    // edge. Past that edge there is nothing at all - which is what the coverage test below states.
    float2 tileLocal = lerp(n, frac(n), it.Params.z);
    // MIRRORED repeat: every other copy runs backwards, so a picture that was never drawn to tile still meets its own
    // reflection at the seam. A triangle wave, not a branch.
    float2 mirrored = abs(frac(n * 0.5) * 2.0 - 1.0);
    // Branch-free (a ?: in this family has device-lost form): flag 1 = mirror X, 2 = mirror Y, 3 = both.
    float flags = it.Params.w;
    float2 pick = float2(step(0.5, fmod(flags, 2.0)), step(0.5, floor(flags * 0.5)));
    float2 inTile = lerp(tileLocal, mirrored, pick);
    // Where the content sits inside its tile - Stretch and the alignment, already folded into one rect per tile.
    float2 inContent = (inTile - it.Drawn.xy) / max(it.Drawn.zw, float2(1e-4, 1e-4));
    float2 uv = it.UvRect.xy + saturate(inContent) * it.UvRect.zw;

    // SampleLevel, not Sample: frac() above makes uv DISCONTINUOUS at every tile seam, so the hardware's derivative -
    // which is what Sample picks a mip level by - spikes there and that one column of pixels is drawn from the smallest
    // mip. That is the thin line down each seam. The level is explicit here because the footprint is ours to state.
    float4 fill = SourceTexture.SampleLevel(SourceSampler, uv, 0.0) * it.Tint;

    // A SQUARE piece is drawn CRISP, not feathered. Nine-slice cuts a picture into nine quads that share edges, and a
    // coverage ramp puts 0.5 on both sides of every shared edge - alpha-composited that is ~0.75, a dark hairline down
    // every joint. Feathering is for a shape with a curve to it, so only a corner radius earns the ramp.
    // Branch-free (a ?: in this pass has device-lost form on this driver): pick between the two with a step on the radius.
    float aa = max(fwidth(d), 1e-4);
    float ramp = saturate(0.5 - d / aa);
    float crisp = step(d, 0.0);
    fill.a *= lerp(crisp, ramp, max(step(0.001, max(max(r4.x, r4.y), max(r4.z, r4.w))), isEllipse));   // an ellipse is ALL curve, so it always earns the ramp
    // Outside the content's rect there is nothing to paint - the gap a Uniform fit leaves around EVERY tile, and the
    // whole of the shape a single copy does not reach.
    float inside = step(0.0, inContent.x) * step(inContent.x, 1.0) * step(0.0, inContent.y) * step(inContent.y, 1.0);
    fill.a *= inside;
    return fill;
}

[shader("fragment")]
float4 PatternPS(PatternPSInput input) : SV_Target
{
    PatternRectData* items = (PatternRectData*)InstancesAddress;
    PatternRectData it = items[input.InstId];

    // The baked corner radius is the shape flag: >= 0 a rect, -1 an ellipse, -2 a regular polygon (whose corners, start
    // angle and ring ride in the Radii it has no use for otherwise).
    float isPolygon = step(it.Params.x, -1.5);
    float shape = isPolygon * 2.0 + step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    bool ellipse = shape > 0.5 && shape < 1.5;   // the arc-length branch below still needs to know which curve it is
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, step(1.5, shape));
    int joinType = int(fmod(floor(it.Stroke1.w / 4096.0), 8.0));
    float d = BrushShapeDistance(input.Local, input.Half, r4, joinType, shape);

    // The record is baked in SLOT units, the SDF above is in device pixels - so the CELL (PatternBrush.CellSize /
    // NoiseBrush.Scale share this field) converts too, or the pattern's cell / the noise's grain would change size with
    // the slot's scale. The noise's own knobs (octaves, seed, lacunarity, gain) are unitless and stay put.
    float sc = input.Scale;
    PatternRectData itPx = it;
    itPx.Params.z = it.Params.z * sc;

    float2 p = input.Local + input.Half;   // fragment from the shape's TOP-LEFT (stable pattern origin at the corner)
    float4 fill = PatternFillColor(itPx, p, input.Local, input.Half.y);

    float widthPx = it.Stroke0.x * sc;
    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = widthPx * 0.5;
        float perim;
        float s = ellipse ? EllipseArc(input.Local, input.Half, perim)
                          : RoundRectArc(input.Local, input.Half, r4, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        float capScl = ArcCapScale(ellipse ? EllipseCurvRadius(input.Local, input.Half) : RoundRectCurvRadius(input.Local, input.Half, r4), dPerp);
        mask = DashTrimMask(s, s, perim, it.Stroke0.z * sc, it.Stroke0.w * sc, it.Stroke1.x * sc, it.Stroke1.y,
                            it.Stroke1.z, dPerp * capScl, halfW * capScl, it.Stroke1.w, it.Dash * sc);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, widthPx, it.Stroke0.y, mask, 0.0);
}

// ---- PatternFill: general instanced geometry (a shared tessellated mesh drawn N times) whose FILL is a PROCEDURAL
// pattern/noise brush - the pattern sibling of GradientFill, so pattern/noise work on ANY geometry (Path/Polygon/glyphs),
// not just the SDF rect. Per-instance PatGeomData from a BDA buffer by SV_InstanceID; the PS reconstructs a PatternRectData
// and calls the SAME PatternFillColor the SDF rect pattern PS uses (fed the fragment's LOCAL mesh position).
struct PatGeomData
{
    float4x4 Local;      // element local -> SLOT space (the slot's matrix is applied on top, from the transform table)
    float4 Params;       // .y pattern type, .z cell (LOCAL units), .w transform-table slot. .x unused
    float4 LocalBounds;  // shape local bounds: minXY, sizeXY
    float4 Color1;
    float4 Color2;
    float4 Color3;
    float4 Noise;        // x octaves (sign=animate), y seed, z lacunarity, w gain (or combustible fire-palette flag)
    float4 Anim;         // .x = offset subtracted from the clock while animating, .y = the phase held while paused
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
    // local -> slot space -> world, as InstancedFillVS / GradientFillVS.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), it.Local), nodes[(uint)it.Params.w].World);

    PatFillPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    return o;
}

// The analytic-AA fringe of those pattern/noise instances: the SAME shared ring and the SAME instance buffer as the
// body, so N elements cost one draw instead of N. The ring is one pixel wide, so it does not evaluate the pattern -
// it takes the brush's LOW colour, exactly as the per-unit fringe did (a procedural field is mostly its background,
// so an edge blends into Color1 rather than ringing a bright midpoint). Reuses InstancedFringePS.
[shader("vertex")]
FringePSInput InstancedPatternFringeVS(FringeVertex v, uint instanceId : SV_InstanceID)
{
    PatGeomData* items = (PatGeomData*)InstancesAddress;
    PatGeomData it = items[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 m = mul(mul(it.Local, nodes[(uint)it.Params.w].World), Projection);

    FringePSInput o;
    float coverage;
    o.Position = ExpandFringe(v, m, coverage);
    o.Color = it.Color1;
    o.Coverage = coverage;
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
    pd.Anim = it.Anim;

    float2 pTopLeft = input.Local - it.LocalBounds.xy;                                   // fragment from the shape top-left
    float2 centerRel = input.Local - (it.LocalBounds.xy + it.LocalBounds.zw * 0.5);      // fragment from the shape centre
    return PatternFillColor(pd, pTopLeft, centerRel, max(it.LocalBounds.w * 0.5, 1.0));
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
    float4 Field;    // .x = the distance range a SAMPLED field encodes, slot units (0 for an analytic shape)
};

struct HaloPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment from the SHAPE's centre, device px
    float2 Half     : TEXCOORD1;   // the shape's half-size, device px
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    float Scale     : TEXCOORD3;
    nointerpolation uint InstId : TEXCOORD4;
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
    color.a *= saturate(a);
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
    float4 Ramp;      // .x = valid palette stops
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

    float n = snoise(ring + float2(t, -t * 0.7));
    float n2 = snoise(ring * 1.9 + float2(-t * 0.6, t * 0.4) + float2(away * 1.5, 0.0));
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
    float hue = snoise(ring * 0.8 + float2(-t * 0.35, t * 0.9)) * 0.5 + 0.5;
    float4 colour = lerp(it.Color, LivingPalette(it, saturate(hue)), step(1.5, it.Ramp.x));
    colour.a *= saturate(a);
    return colour;
}

// ---- TexFill: general instanced geometry (a shared tessellated mesh drawn N times) whose FILL is SAMPLED from a
// texture - the textured sibling of GradientFill/PatternFill, so an ImageBrush works on ANY geometry (Path/Polygon) and
// N such shapes cost ONE draw instead of N. A tessellated mesh carries neither an SDF nor a usable uv0, so the picture
// is mapped across the shape's own LOCAL bounding box, with the same tiling arithmetic the SDF textured batch uses.
// WHICH texture is not in the record: one texture is bound per DRAW, exactly as TexRectCollector does per segment.
struct TexGeomData
{
    float4x4 Local;      // element local -> SLOT space (the slot's matrix is applied on top, from the transform table)
    float4 Params;       // .x repeat flag, .y mirror flags (1 = X, 2 = Y, 3 = both), .w transform slot
    float4 LocalBounds;  // shape local bounds: minXY, sizeXY - the box the picture is mapped across
    float4 Tile;         // tile grid over that box: tiles per axis (.xy), grid origin in tiles (.zw)
    float4 Rotation;     // 2x2 mapping a fragment back into the unturned grid, row-major (identity = 1,0,0,1)
    float4 Drawn;        // the content's rect inside ONE tile: offsetXY, scaleXY, both in 0..1 of the tile
    float4 UvRect;       // the sub-rectangle of the source one copy samples
    float4 Tint;
};

struct TexFillPSInput
{
    float4 Position : SV_Position;
    float2 Local : TEXCOORD0;                   // varying: fragment's local mesh xy
    nointerpolation uint InstId : TEXCOORD1;    // instance -> re-read TexGeomData in the PS (light signature)
};

[shader("vertex")]
TexFillPSInput TexFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    TexGeomData* items = (TexGeomData*)InstancesAddress;
    TexGeomData it = items[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), it.Local), nodes[(uint)it.Params.w].World);

    TexFillPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 TexFillPS(TexFillPSInput input) : SV_Target
{
    TexGeomData* items = (TexGeomData*)InstancesAddress;
    TexGeomData it = items[input.InstId];

    // 0..1 across the shape's box -> TILE space -> the content's rect inside one tile -> the source's sub-rectangle.
    float2 t = (input.Local - it.LocalBounds.xy) / max(it.LocalBounds.zw, float2(1e-4, 1e-4));
    float2 g = float2(t.x * it.Rotation.x + t.y * it.Rotation.y, t.x * it.Rotation.z + t.y * it.Rotation.w);
    float2 nn = g * it.Tile.xy - it.Tile.zw;
    // A SINGLE copy never wraps - frac() would send its far edge back to the opposite one (see TexRectPS).
    float2 tileLocal = lerp(nn, frac(nn), it.Params.x);
    float2 mirrored = abs(frac(nn * 0.5) * 2.0 - 1.0);
    float2 pick = float2(step(0.5, fmod(it.Params.y, 2.0)), step(0.5, floor(it.Params.y * 0.5)));
    float2 inTile = lerp(tileLocal, mirrored, pick);
    float2 n = (inTile - it.Drawn.xy) / max(it.Drawn.zw, float2(1e-4, 1e-4));
    float2 uv = it.UvRect.xy + saturate(n) * it.UvRect.zw;

    // SampleLevel, not Sample: frac() makes uv discontinuous at every tile seam, and the derivative Sample picks its mip
    // by spikes there - one column of pixels from the smallest mip, i.e. a thin line down each seam.
    float4 color = SourceTexture.SampleLevel(SourceSampler, uv, 0.0) * it.Tint;

    // Outside the content's rect inside its tile there is nothing to paint - the gap a Uniform fit leaves.
    float inside = step(0.0, n.x) * step(n.x, 1.0) * step(0.0, n.y) * step(n.y, 1.0);
    color.a *= inside;

    return color;
}

struct TexFringePSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;
    float Coverage  : TEXCOORD1;
    nointerpolation uint InstId : TEXCOORD2;
};

// The analytic-AA fringe of those textured instances: the SAME shared ring and the SAME instance buffer as the body, so
// N elements cost one draw instead of N. Unlike the pattern fringe - which takes the brush's low colour - a picture has
// no single edge colour, so the ring SAMPLES it, which is what makes the edge of a textured shape read as that shape's
// own edge rather than a coloured halo.
[shader("vertex")]
TexFringePSInput InstancedTexFringeVS(FringeVertex v, uint instanceId : SV_InstanceID)
{
    TexGeomData* items = (TexGeomData*)InstancesAddress;
    TexGeomData it = items[instanceId];
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 m = mul(mul(it.Local, nodes[(uint)it.Params.w].World), Projection);

    TexFringePSInput o;
    float coverage;
    o.Position = ExpandFringe(v, m, coverage);
    o.Local = v.Position;
    o.Coverage = coverage;
    o.InstId = instanceId;
    return o;
}

[shader("fragment")]
float4 TexFringePS(TexFringePSInput input) : SV_Target
{
    TexGeomData* items = (TexGeomData*)InstancesAddress;
    TexGeomData it = items[input.InstId];

    // The ring is expanded a pixel OUTWARD, so its outer edge lies just outside the shape's box: clamp before mapping,
    // or the band would sample past the picture (and the single-copy clip below would erase the fringe entirely).
    float2 t = saturate((input.Local - it.LocalBounds.xy) / max(it.LocalBounds.zw, float2(1e-4, 1e-4)));
    float2 g = float2(t.x * it.Rotation.x + t.y * it.Rotation.y, t.x * it.Rotation.z + t.y * it.Rotation.w);
    float2 nn = g * it.Tile.xy - it.Tile.zw;
    float2 tileLocal = lerp(nn, frac(nn), it.Params.x);
    float2 mirrored = abs(frac(nn * 0.5) * 2.0 - 1.0);
    float2 pick = float2(step(0.5, fmod(it.Params.y, 2.0)), step(0.5, floor(it.Params.y * 0.5)));
    float2 inTile = lerp(tileLocal, mirrored, pick);
    float2 n = (inTile - it.Drawn.xy) / max(it.Drawn.zw, float2(1e-4, 1e-4));
    float2 uv = it.UvRect.xy + saturate(n) * it.UvRect.zw;

    float4 color = SourceTexture.SampleLevel(SourceSampler, uv, 0.0) * it.Tint;
    float inside = step(0.0, n.x) * step(n.x, 1.0) * step(0.0, n.y) * step(n.y, 1.0);
    color.a *= inside * input.Coverage;

    return color;
}

// ---- Fractal batch: the SAME SDF rounded-rect (self-AA shape + shared stroke), but the FILL is an escape-time FRACTAL
// (Julia/Mandelbrot) iterated per fragment - resolution-independent, no texture. Per-instance FractalRectData from a BDA
// storage buffer by SV_InstanceID; the PS re-reads the record, maps the fragment to the complex plane, iterates z=z²+c and
// colours by the smooth escape count. With the animate flag set, a Julia's C drifts on a Lissajous over the global Time.
struct FractalRectData
{
    float4 Bounds;       // NODE-local x, y, w, h
    float4 Params;       // .x corner radius, .y type (0 Julia/1 Mandelbrot), .z transform slot, .w max iterations
    float4 Radii;        // corner radii: x = TL, y = TR, z = BR, w = BL
    float4 Geom;         // .x/.y complex-plane centre, .z zoom, .w morph speed
    float4 Julia;        // .x/.y Julia constant C, .z animate flag, .w reserved
    float4 Color1;       // straight RGBA, opacity folded
    float4 Color2;       // straight RGBA, opacity folded
    float4 StrokeColor;  // straight stroke RGBA (.w == 0 -> no stroke)
    float4 Stroke0;      // width_px, align, dashOn, dashGap
    float4 Stroke1;      // dashOffset, trimStart, trimEnd, flags
    float4 Dash;         // dash runs 2..5 (device px); runs 0 and 1 ride in Stroke0.zw, the count in Stroke1.w
    float4 Ref;          // perturbation: .x orbit start index (into OrbitAddress), .y orbit length, .z deep flag (1=use), .w reserved
};

struct FractalPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the rect CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;   // rect half-size
    float4 Radii    : TEXCOORD2;   // corner radii (TL, TR, BR, BL) in device px
    nointerpolation uint InstId : TEXCOORD3;   // instance -> re-read FractalRectData in the PS
    nointerpolation float Scale : TEXCOORD4;   // slot unit -> device px, for the stroke record the PS re-reads
};

[shader("vertex")]
FractalPSInput FractalRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    FractalRectData* items = (FractalRectData*)InstancesAddress;
    FractalRectData it = items[instanceId];

    FractalPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    // SDF inputs in DEVICE PIXELS, same scheme as RectBatchInstancedVS. The fractal plane is Local/Half - a RATIO - so
    // the change of unit leaves the image itself untouched; only the stroke record needs Scale.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.z].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float widthPx = it.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii = it.Radii * iso;
    o.InstId = instanceId;
    o.Scale  = iso;
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

    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = min(input.Radii, float4(lim, lim, lim, lim));
    int joinType = int(fmod(floor(it.Stroke1.w / 4096.0), 8.0));
    float d = SdRoundRectJoin(input.Local, input.Half, r4, joinType);

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

    // Stroke lengths: slot units -> device pixels, to match the SDF above (align/trim/flags are unitless).
    float sc = input.Scale;
    float widthPx = it.Stroke0.x * sc;
    float mask = 1.0;
    if (it.Stroke0.z > 0.0 || it.Stroke1.y > 0.0 || it.Stroke1.z < 1.0)
    {
        float halfW = widthPx * 0.5;
        float perim;
        float s = RoundRectArc(input.Local, input.Half, r4, perim);
        float dPerp = d - it.Stroke0.y * halfW;
        float capScl = ArcCapScale(RoundRectCurvRadius(input.Local, input.Half, r4), dPerp);
        mask = DashTrimMask(s, s, perim, it.Stroke0.z * sc, it.Stroke0.w * sc, it.Stroke1.x * sc, it.Stroke1.y,
                            it.Stroke1.z, dPerp * capScl, halfW * capScl, it.Stroke1.w, it.Dash * sc);
    }
    return CompositeFillStroke(d, fill, it.StrokeColor, widthPx, it.Stroke0.y, mask, 0.0);
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

    // The analytic-AA fringe of those same instances: one shared scale-free ring, the same instance buffer, one draw.
    pass Fringe
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = InstancedFringeVS;
        PixelShader = InstancedFringePS;
    }

    // The same, for PATTERN/NOISE instances - the ring is coloured by the brush's low colour (see the VS).
    pass PatternFringe
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = InstancedPatternFringeVS;
        PixelShader = InstancedFringePS;
    }

    // The same, for GRADIENT instances - the ring is coloured by the gradient per fragment, so it has its own PS.
    pass GradientFringe
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = InstancedGradientFringeVS;
        PixelShader = InstancedGradientFringePS;
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

    // The soft band under a shape (aura / shadow). Drawn BEFORE every fill in its clip group, so it lands under them.
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

    // Instanced TEXTURED fill on arbitrary geometry: the shared mesh drawn N times, each instance sampling the bound
    // texture across its own local box. Solid/gradient/pattern fills use pass Fill/GradientFill/PatternFill.
    pass TexFill
    {
        Profile = 5.1;
        VertexShader = TexFillVS;
        PixelShader = TexFillPS;
    }

    pass TexFringe
    {
        Profile = 5.1;
        VertexShader = InstancedTexFringeVS;
        PixelShader = TexFringePS;
    }

    // SDF ellipse/circle fills - per-instance EllipseData from a BDA storage buffer by SV_InstanceID; quad from SV_VertexID.
    pass Ellipse
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = EllipseBatchInstancedVS;
        PixelShader = EllipseBatchPS;
    }

    // Regular polygons - a triangle and a circle differ by one number, the corner count.
    pass Polygon
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = PolygonBatchInstancedVS;
        PixelShader = PolygonBatchPS;
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

    // SDF rounded-rect fills whose colour is SAMPLED from a texture (per-instance TexRectData; ONE texture bound per
    // segment). An ImageBrush is one instance; a NineSliceBrush is nine, so a whole skinned frame is still one draw.
    pass TexRect
    {
        EffectName = "BatchEffect";
        Profile = 6.6;
        VertexShader = TexRectInstancedVS;
        PixelShader = TexRectPS;
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
