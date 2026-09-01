// STROKE MATHS - how a fill and its outline become one colour, and how a dash/trim pattern masks that outline.
//
// The whole family works off ONE signed distance: a solid stroke is `abs(d - align*halfW) - halfW`, and dashes and
// trims modulate it through a mask computed from arc length. No geometry is built for any of it, which is what lets a
// dashed, trimmed, capped stroke stay inside an instanced batch.
//
// Include AFTER ShapeMath.fxh: the border compositor cuts its inner outline with the same joins.

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

