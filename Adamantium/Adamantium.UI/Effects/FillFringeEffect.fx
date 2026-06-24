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
struct PSInput { float4 Position : SV_Position; float Coverage : TEXCOORD0; };

[shader("vertex")]
PSInput FillFringeVS(VSInput input)
{
    PSInput o;
    o.Position = mul(float4(input.Position, 0.0, 1.0), Projection);   // row-vector convention (matches engine effects)
    o.Coverage = input.Coverage;
    return o;
}

[shader("fragment")]
float4 FillFringePS(PSInput input) : SV_Target
{
    float4 c = FillColor;
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
