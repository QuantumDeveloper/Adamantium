// GPU fill anti-aliasing (Analytic AA, Phase 1). Reuses the line-rendering GPU infra: a compute technique
// (FillFringeExpand) turns a CLOSED fill contour into a ~1px coverage fringe RING around it - inner edge on the contour
// (coverage = 1, meets the solid body), outer edge pushed out by FringeWidth (coverage = 0) - written as a triangle
// LIST via a BDA device address. A graphics technique (FillFringeDraw) rasterizes it with the fill colour and
// alpha *= coverage, feathering the edge analytically (no MSAA), drawn on top of the CPU-triangulated solid body.
// Fills are closed loops, so there are no caps/ends here. Shader bodies are Slang. Single .fx -> one Effect class.

// --- FillFringeExpand (compute) globals ---
uint64_t PointsAddress;   // float2[] contour points (PointCount), CLOSED loop (point[PointCount-1] -> point[0])
uint64_t OutputAddress;   // float3[] output vertices: (x, y, coverage)
uint PointCount;
float FringeWidth;        // outward fringe width in geometry units (CPU sets ~1 device px / current scale)
float Winding;            // +1 / -1 chosen on the CPU (from the contour's signed area) so the miter points OUTWARD

// --- FillFringeDraw (graphics) globals ---
float4x4 Projection;
float4 FillColor;

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

// Outward miter offset direction*length at contour point i: bisector of the two adjacent edge normals, length clamped
// so a sharp corner can't shoot the fringe to infinity, oriented outward by Winding. Closed loop => i always has both
// neighbours.
float2 OutwardMiter(uint i)
{
    float2* points = (float2*)PointsAddress;
    uint prev = (i + PointCount - 1u) % PointCount;
    uint next = (i + 1u) % PointCount;
    float2 d0 = normalize(points[i] - points[prev]);   // incoming edge dir
    float2 d1 = normalize(points[next] - points[i]);   // outgoing edge dir
    float2 n0 = float2(-d0.y, d0.x);
    float2 n1 = float2(-d1.y, d1.x);
    float2 miter = normalize(n0 + n1);
    float denom = max(dot(miter, n0), 0.25);           // clamp the corner spike to <= 4x
    return miter * (Winding / denom);
}

void WriteVert(float3* outv, uint idx, float2 pos, float cov)
{
    outv[idx] = float3(pos, cov);
}

// One thread per contour SEGMENT (i -> next). Emits the ring quad (2 triangles, 6 verts) between the contour edge
// (coverage 1) and the outward-offset fringe edge (coverage 0). UI draws cull-none, so triangle winding is irrelevant.
[shader("compute")]
[numthreads(64, 1, 1)]
void FillFringeExpandCS(uint3 tid : SV_DispatchThreadID)
{
    float2* points = (float2*)PointsAddress;
    float3* outVerts = (float3*)OutputAddress;

    uint i = tid.x;
    if (i >= PointCount) return;

    uint ni = (i + 1u) % PointCount;
    float2 inA = points[i];
    float2 inB = points[ni];
    float2 outA = inA + OutwardMiter(i) * FringeWidth;
    float2 outB = inB + OutwardMiter(ni) * FringeWidth;

    uint baseV = i * 6u;
    WriteVert(outVerts, baseV + 0u, inA,  1.0);
    WriteVert(outVerts, baseV + 1u, outA, 0.0);
    WriteVert(outVerts, baseV + 2u, inB,  1.0);
    WriteVert(outVerts, baseV + 3u, outA, 0.0);
    WriteVert(outVerts, baseV + 4u, outB, 0.0);
    WriteVert(outVerts, baseV + 5u, inB,  1.0);
}

struct VSInput { float2 Position : POSITION; float Coverage : TEXCOORD0; };
struct PSInput { float4 Position : SV_Position; float Coverage : TEXCOORD0; float2 Local : TEXCOORD1; };

[shader("vertex")]
PSInput FillFringeVS(VSInput input)
{
    PSInput o;
    o.Position = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    o.Coverage = input.Coverage;
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
    // Expand (compute): closed contour -> coverage fringe ring, written via BDA. Plain Draw.
    pass Expand
    {
        EffectName = "FillFringeEffect";
        Profile = 6.6;
        ComputeShader = FillFringeExpandCS;
    }

    // Draw (graphics): rasterize the fringe ring; alpha ramps with coverage.
    pass Draw
    {
        EffectName = "FillFringeEffect";
        Profile = 6.6;
        VertexShader = FillFringeVS;
        PixelShader = FillFringePS;
    }
}
