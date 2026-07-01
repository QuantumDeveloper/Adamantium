// Item-background batch (docs/TEXT_GLYPH_BATCH_PLAN.md - the "подложки" instancing). Draws MANY solid rounded-rect
// fills (ItemsControl item backgrounds, and any solid rounded-rect fill) in ONE instanced draw: each fill is one
// per-instance RectItem, expanded to a quad in the vertex stage (corner from SV_VertexID), and the pixel shader
// reconstructs the rounded-rect coverage ANALYTICALLY from a signed-distance field - self-anti-aliasing, so there is
// no separate AA fringe unit per fill. Positions are baked to WORLD space on the CPU during aggregation; the vertex
// shader applies only a single static Projection (the one driver-safe form on this Turing - no per-instance matrix).
// Slang bodies. Row-vector convention (matches the engine's other effects).

float4x4 Projection;

struct RectItem
{
    float4 Bounds : Position;    // world-space x, y, w, h (baked on the CPU)
    float4 Params : TEXCOORD0;   // .x = corner radius (uniform); .yzw reserved
    float4 Color  : COLOR0;      // straight (non-premultiplied) RGBA, opacity already folded in
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment position relative to the rect CENTRE (SDF space)
    float2 Half     : TEXCOORD1;   // rect half-size
    float  Radius   : TEXCOORD2;   // corner radius
    float4 Color    : COLOR0;
};

[shader("vertex")]
PSInput RectBatchVS(RectItem item, uint vertexId : SV_VertexID)
{
    PSInput o;
    // 4-vertex triangle strip: corner = (0,0),(1,0),(0,1),(1,1) from the two low bits of the vertex id.
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float2 worldPos = item.Bounds.xy + corner * item.Bounds.zw;
    o.Position = mul(float4(worldPos, 0.0, 1.0), Projection);
    o.Half   = item.Bounds.zw * 0.5;
    o.Local  = (corner - 0.5) * item.Bounds.zw;
    o.Radius = item.Params.x;
    o.Color  = item.Color;
    return o;
}

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
    float d = SdRoundBox(input.Local, input.Half, r);
    // Coverage across ~1 screen pixel of the distance field: 1 inside, ramps to 0 across the edge = analytic AA.
    float aa = max(fwidth(d), 1e-5);
    float coverage = saturate(0.5 - d / aa);
    float4 c = input.Color;
    c.a *= coverage;                 // straight-alpha output, drawn with a straight AlphaBlend (matches solid fills)
    return c;
}

technique RectBatch
{
    pass Draw
    {
        EffectName = "RectBatchEffect";
        Profile = 6.6;
        VertexShader = RectBatchVS;
        PixelShader = RectBatchPS;
    }
}
