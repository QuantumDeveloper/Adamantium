// GPU stroke effect (line-rendering Phase B). One compute technique (StrokeExpand) turns a polyline + half-thickness
// into a miter-joined triangle-STRIP ribbon written straight into a vertex buffer via a BDA device address, and one
// graphics technique (StrokeDraw) rasterizes that ribbon. Both live in a single .fx so the generator emits one Effect
// class (StrokeEffect) - no C# wrapper needed. Shader bodies are Slang.
//
//   open   output layout (triangle strip):  [ p0+, p0-, p1+, p1-, ... ]            -> PointCount * 2 vertices
//   closed output layout (triangle strip):  [ p0+, p0-, ..., p(n-1)+, p(n-1)-, p0+, p0- ] -> (PointCount + 1) * 2
//
// For an OPEN polyline the endpoints use the single adjacent segment's normal (flat ends; round/square caps come
// later). For a CLOSED loop every point - including the first/last - uses the wrap-around miter, and one extra closing
// pair (a copy of point 0's offsets) is appended so the strip seals the last segment back to the first. The miter
// length is halfThickness / dot(miter, segmentNormal), clamped so a sharp corner can't shoot the tip to infinity.

// --- StrokeExpand (compute) globals ---
uint64_t PointsAddress;   // float2[] polyline points (PointCount of them)
uint64_t OutputAddress;   // float2[] output vertices
uint PointCount;
uint IsClosed;            // 0 = open polyline (flat ends), 1 = closed loop (wrap-around miters + closing pair)
uint StartCap;            // open-polyline start cap: 0 = flat, 1 = square (round/triangle handled on the CPU path)
uint EndCap;              // open-polyline end cap:   0 = flat, 1 = square
float HalfThickness;

// --- StrokeDraw (graphics) globals ---
float4x4 Projection;
float4 StrokeColor;

// Unit normal of the segment a->b (perp of the normalized direction).
float2 SegmentNormal(float2 a, float2 b)
{
    float2 d = normalize(b - a);
    return float2(-d.y, d.x);
}

[shader("compute")]
[numthreads(64, 1, 1)]
void StrokeExpandCS(uint3 tid : SV_DispatchThreadID)
{
    // Closed loops emit one extra pair (the closing copy of point 0), so the strip reconnects to the start.
    uint pairCount = PointCount + (IsClosed != 0 ? 1u : 0u);
    if (tid.x >= pairCount)
        return;

    float2* points = (float2*)PointsAddress;
    float2* outVerts = (float2*)OutputAddress;

    uint i = tid.x % PointCount;   // logical point (the closing thread re-emits point 0)
    float2 p = points[i];

    float2 miter;
    float miterLen;

    if (IsClosed == 0 && i == 0)
    {
        float2 dir = normalize(points[1] - points[0]);
        if (StartCap == 1) p -= dir * HalfThickness;   // square cap: push the start edge half a thickness outward
        miter = float2(-dir.y, dir.x);
        miterLen = HalfThickness;
    }
    else if (IsClosed == 0 && i + 1 == PointCount)
    {
        float2 dir = normalize(points[i] - points[i - 1]);
        if (EndCap == 1) p += dir * HalfThickness;     // square cap: push the end edge half a thickness outward
        miter = float2(-dir.y, dir.x);
        miterLen = HalfThickness;
    }
    else
    {
        // Interior point (or any point of a closed loop): bisector of the two adjacent segment normals. For a closed
        // loop the neighbours wrap around the ends.
        uint prev = (i + PointCount - 1) % PointCount;
        uint next = (i + 1) % PointCount;
        float2 n0 = SegmentNormal(points[prev], p);
        float2 n1 = SegmentNormal(p, points[next]);
        miter = normalize(n0 + n1);
        float denom = max(dot(miter, n0), 0.25);   // clamp -> miter length capped at 4*half on sharp corners
        miterLen = HalfThickness / denom;
    }

    uint o = tid.x * 2;   // thread index, so the closing pair lands at the strip's end
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
