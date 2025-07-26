struct UI_VERTEX
{
    float4 position : SV_POSITION;
    float3 normal: NORMAL;
    float2 uv0: TEXCOORD0;
    float2 uv1: TEXCOORD1;
};

struct VERTEX_OUTPUT
{
    float4 position : SV_POSITION;
    float3 normal: NORMAL;
    float2 uv0: TEXCOORD0;
    float2 uv1: TEXCOORD1;
};