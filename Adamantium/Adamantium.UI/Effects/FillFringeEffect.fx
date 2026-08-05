// GPU fill anti-aliasing (Analytic AA). Rasterizes a coverage fringe RING around a CLOSED fill contour - inner edge on
// the contour (coverage = 1, meets the solid body), outer edge (coverage = 0) - with the fill colour and alpha *=
// coverage, feathering the edge analytically (no MSAA), drawn on top of the CPU-triangulated solid body. Fills are
// closed loops, so there are no caps/ends here. Shader bodies are Slang. Single .fx -> one Effect class.
//
// The ring's TRIANGLES come from the CPU (Rendering/FringeGeometry.cs) - the same builder the instanced fringe uses, so
// the ring has one definition. A vertex holds the contour point plus, on the outer edge, the two adjacent EDGE
// DIRECTIONS; the VS turns those into a screen-space miter and pushes the vertex FringePixels out. So the buffer holds
// no scale anywhere: it is built once per shape and stays correct at any zoom. (It used to be expanded by a compute
// pass at a LOCAL width of 1px/scale, which made every scale a different buffer - that pass is gone, since a ring that
// never changes has nothing to re-expand.) Building the miter from the screen edge directions also keeps the width
// honest under anisotropic scale, skew and rotation, where a local-space miter mapped to screen is not perpendicular
// to the screen edge.

float4x4 Projection;
float4 FillColor;
float2 ViewportSize;      // render target size in DEVICE pixels - the NDC <-> pixel basis for the fringe offset
float FringePixels;       // fringe width in DEVICE pixels (1 = the analytic-AA edge is exactly one pixel wide)

// Gradient-aware fringe: when IsGradient != 0 the ring is coloured by the SAME linear/radial gradient as the fill (so the
// feathered edge matches the fill colour there, not one flat colour) - the fix for aliased gradient-shape edges. The
// gradient is passed as plain uniforms (per-draw, no BDA / heavy interpolators). Mirrors BatchEffect's GradParam/GradColor.
int IsGradient;
float4 GParams;       // (_, type[1 linear/2 radial], stopCount, spread)
float4 GGeom0;        // LOCAL 0..1: linear (startXY,endXY) | radial (centerXY,radiusXY)
float4 GGeom1;        // radial focal (originXY,_,_)
float4 GLocalBounds;  // shape local bounds: minXY, sizeXY (uv = (local-min)/size)
float4 GS0; float4 GS1; float4 GS2; float4 GS3; float4 GS4; float4 GS5; float4 GS6; float4 GS7;
float4 GOff0; float4 GOff1;

float FringeGradSpread(float t, int spread)
{
    if (spread == 1) { float m = fmod(abs(t), 2.0); return m > 1.0 ? 2.0 - m : m; }   // reflect
    if (spread == 2) { return frac(t); }                                              // repeat
    return saturate(t);                                                               // pad
}

float FringeGradParam(float2 uv)
{
    if (int(GParams.y) == 2)
    {
        float2 center = GGeom0.xy;
        float2 radius = max(GGeom0.zw, float2(1e-4, 1e-4));
        float2 focal = (GGeom1.xy - center) / radius;
        float2 q = (uv - center) / radius;
        float2 dir = q - focal;
        float dlen = length(dir);
        if (dlen < 1e-6) return 0.0;
        float2 dn = dir / dlen;
        float b = dot(focal, dn);
        float c = dot(focal, focal) - 1.0;
        float sEdge = -b + sqrt(max(b * b - c, 0.0));
        return (sEdge > 1e-6) ? (dlen / sEdge) : 0.0;
    }
    float2 axis = GGeom0.zw - GGeom0.xy;
    float denom = dot(axis, axis);
    if (denom < 1e-9) return 0.0;
    return dot(uv - GGeom0.xy, axis) / denom;
}

float4 FringeGradColor(float t)
{
    int n = int(GParams.z);
    if (n <= 0) return float4(0.0, 0.0, 0.0, 0.0);
    float offs[8];
    offs[0]=GOff0.x; offs[1]=GOff0.y; offs[2]=GOff0.z; offs[3]=GOff0.w;
    offs[4]=GOff1.x; offs[5]=GOff1.y; offs[6]=GOff1.z; offs[7]=GOff1.w;
    float4 cols[8];
    cols[0]=GS0; cols[1]=GS1; cols[2]=GS2; cols[3]=GS3; cols[4]=GS4; cols[5]=GS5; cols[6]=GS6; cols[7]=GS7;
    if (t <= offs[0]) return cols[0];
    for (int i = 1; i < n; i++)
        if (t <= offs[i]) { float seg = max(offs[i] - offs[i-1], 1e-6); return lerp(cols[i-1], cols[i], saturate((t - offs[i-1]) / seg)); }
    return cols[n - 1];
}

float2 SafeDir(float2 v)
{
    float len = length(v);
    return len > 1e-9 ? v / len : float2(0.0, 0.0);   // a degenerate edge would otherwise produce a NaN direction
}

// An INNER vertex (on the contour, coverage 1) carries zero directions and is never offset; an OUTER vertex carries the
// two adjacent edge directions, from which the VS builds the screen-space miter. Winding is folded into their SIGN by
// the builder (reversing an edge direction reverses its 90-degree normal), so a hole's inward fringe is just a sign in
// these vectors. Matches RenderUnits/FringeVertex.cs.
struct VSInput { float2 Position : POSITION; float2 Dir0 : TEXCOORD0; float2 Dir1 : TEXCOORD1; };
struct PSInput { float4 Position : SV_Position; float Coverage : TEXCOORD0; float2 Local : TEXCOORD1; };

[shader("vertex")]
PSInput FillFringeVS(VSInput input)
{
    PSInput o;
    float4 clip = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    float outer = dot(input.Dir0, input.Dir0) + dot(input.Dir1, input.Dir1);
    if (outer > 0.0)
    {
        // Edge directions -> PIXEL space (w = 0 drops the projection's translation), then the miter is built THERE, so
        // it is perpendicular to the edge as the rasterizer sees it and the width is exactly FringePixels pixels.
        float2 halfVp = max(ViewportSize, float2(1.0, 1.0)) * 0.5;
        float w = max(clip.w, 1e-6);
        float2 e0 = SafeDir(mul(float4(input.Dir0, 0.0, 0.0), Projection).xy / w * halfVp);
        float2 e1 = SafeDir(mul(float4(input.Dir1, 0.0, 0.0), Projection).xy / w * halfVp);
        float2 n0 = float2(-e0.y, e0.x);
        float2 n1 = float2(-e1.y, e1.x);
        float2 sum = n0 + n1;
        float2 miter = length(sum) > 1e-4 ? normalize(sum) : n0;   // a 180-degree reversal has no bisector: use one normal
        float denom = max(dot(miter, n0), 0.25);                   // clamp the corner spike to <= 4x
        clip.xy += miter * (FringePixels / denom) / halfVp * w;
    }
    o.Position = clip;
    o.Coverage = outer > 0.0 ? 0.0 : 1.0;   // outer edge fades to 0; the contour vertex meets the solid body at 1
    o.Local = input.Position;   // LOCAL geometry position -> the PS maps it to the gradient uv
    return o;
}

[shader("fragment")]
float4 FillFringePS(PSInput input) : SV_Target
{
    // Gradient fills colour the ring by the gradient at this fragment (matching the fill); solid fills use FillColor.
    float4 c;
    if (IsGradient != 0)
    {
        float2 uv = (input.Local - GLocalBounds.xy) / max(GLocalBounds.zw, float2(1e-4, 1e-4));
        c = FringeGradColor(FringeGradSpread(FringeGradParam(uv), int(GParams.w)));
    }
    else
    {
        c = FillColor;
    }
    c.a *= saturate(input.Coverage);   // 1 at the contour -> 0 at the outer fringe = analytic edge coverage
    return c;
}

technique FillFringe
{
    // Draw (graphics): rasterize the fringe ring; alpha ramps with coverage.
    pass Draw
    {
        EffectName = "FillFringeEffect";
        Profile = 6.6;
        VertexShader = FillFringeVS;
        PixelShader = FillFringePS;
    }
}
