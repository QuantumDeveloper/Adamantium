// GPU stroke effect (line-rendering Phase B). One compute technique (StrokeExpand) turns a polyline + half-thickness
// into a miter-joined triangle-STRIP ribbon written straight into a vertex buffer via a BDA device address, and one
// graphics technique (StrokeDraw) rasterizes that ribbon. Both live in a single .fx so the generator emits one Effect
// class (StrokeEffect) - no C# wrapper needed. Shader bodies are Slang.
//
//   expander output layout (triangle strip):  [ p0+, p0-, p1+, p1-, ... ]   -> PointCount * 2 vertices
//
// Endpoints use the single adjacent segment's normal (flat ends; round/square caps come later). The miter length is
// halfThickness / dot(miter, segmentNormal), clamped so a sharp corner can't shoot the tip to infinity.

// --- StrokeExpand (compute) globals ---
uint64_t PointsAddress;   // float2[] polyline points (PointCount of them)
uint64_t OutputAddress;   // float2[] output vertices (PointCount * 2, triangle strip)
uint PointCount;
float HalfThickness;

// --- StrokeDraw (graphics) globals ---
float4x4 Projection;
float4 StrokeColor;

[shader("compute")]
[numthreads(64, 1, 1)]
void StrokeExpandCS(uint3 tid : SV_DispatchThreadID)
{
    if (tid.x >= PointCount)
        return;

    float2* points = (float2*)PointsAddress;
    float2* outVerts = (float2*)OutputAddress;
    uint i = tid.x;
    float2 p = points[i];

    float2 miter;
    float miterLen;

    if (i == 0)
    {
        float2 d = normalize(points[1] - points[0]);
        miter = float2(-d.y, d.x);
        miterLen = HalfThickness;
    }
    else if (i + 1 == PointCount)
    {
        float2 d = normalize(points[i] - points[i - 1]);
        miter = float2(-d.y, d.x);
        miterLen = HalfThickness;
    }
    else
    {
        float2 d0 = normalize(p - points[i - 1]);
        float2 d1 = normalize(points[i + 1] - p);
        float2 n0 = float2(-d0.y, d0.x);
        float2 n1 = float2(-d1.y, d1.x);
        miter = normalize(n0 + n1);
        float denom = max(dot(miter, n0), 0.25);   // clamp -> miter length capped at 4*half on sharp corners
        miterLen = HalfThickness / denom;
    }

    uint o = i * 2;
    outVerts[o + 0] = p + miter * miterLen;
    outVerts[o + 1] = p - miter * miterLen;
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
    // Expand (compute): polyline -> miter-joined triangle-strip ribbon, written via BDA into the vertex buffer.
    pass Expand
    {
        // Slang ignores this (targets spirv_1_6); kept non-zero for the parser + SM 6.6 for the DXC fallback.
        EffectName = "StrokeEffect";
        Profile = 6.6;
        ComputeShader = StrokeExpandCS;
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
