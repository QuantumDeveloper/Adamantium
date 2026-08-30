// BRUSH-ONLY maths: what a computed or sampled fill needs and a solid one never asks for.
//
// Small on purpose. Measured rather than guessed: of everything the two effects share, only these three are used by
// BrushEffect alone - the shape distance a brush picks per instance, the scaling of those shape numbers to device
// pixels, and the uncapped dash mask. The rest (the SDFs themselves, the stroke compositing, the fringe expansion) is
// used by BOTH, so it stays in ShapeMath/StrokeMath rather than being renamed into a brush file it does not belong to.
//
// Include LAST, after CommonData + ShapeMath + StrokeMath: every function here is built from those.

// THE shape a BRUSH pass paints on. Three passes (gradient, pattern, texture) each draw a rounded rect, an ellipse or a
// regular polygon and differ only in where the COLOUR comes from - so which shape that is gets stated once, here, rather
// than three times in three pixel shaders.
//
// A polygon carries no corner radii, so its own numbers ride in exactly that field: .x corners, .y start angle in
// radians, .z ring thickness in device px. The shape selector is the pass's own (a negative baked radius for the pattern
// and texture passes, Geom1.z for the gradient one), resolved to 0 rect / 1 ellipse / 2 polygon before the call.
// The shape numbers, taken to device pixels. A rect's four are RADII and all scale; a POLYGON's are not radii at all -
// .x is a corner COUNT, .y an ANGLE in radians, and only .z (the ring) is a length. Scaling the whole vector turned
// three corners into four and a half and swung the start angle with the DPI, which is why a tiled brush drew nonsense
// on a polygon while the very same shape with a solid fill was right: only the three brush passes share this field.
float4 ScaleShapeNumbers(float4 radii, float iso, float isPolygon)
{
    return lerp(radii * iso, float4(radii.x, radii.y, radii.z * iso, radii.w), isPolygon);
}

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

