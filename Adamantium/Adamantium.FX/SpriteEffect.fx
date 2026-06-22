const static float EPSILON = 1.401298E-45;
const static int VERTICES_PER_SPRITE = 4;

// Per-instance sprite data (one element = one sprite quad). The quad corners come from SV_VertexID, so the old
// geometry-shader expansion is gone: this runs as plain instanced rendering (4-vertex triangle strip x N instances),
// which is portable (no GS on Metal/MoltenVK) and dodges the NVIDIA Turing GS NVVM bug.
struct SpriteItem
{
    float4 Destination: Position;
    float4 Source: TEXCOORD0;
    float2 Origin:TEXCOORD1;
    float Depth : PSIZE0;
    float Rotation : PSIZE1;
    float4 Color: COLOR0;
    int4 TextureInfo : BINORMAL0;
    int SpriteEffect : BLENDINDICES0;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
    int TextureId : FOG;
};

Texture2D Texture;
SamplerState TextureSampler;
float2 TextureCornerCoords[4];
matrix MatrixTransform;

PSInput SpriteVertexShader(SpriteItem item, uint vertexId : SV_VertexID)
{
    PSInput vertex;
    int corner = (int)vertexId;   // 0..3, one quad corner per strip vertex
    float2 origin = item.Origin;

    // Normalize the source rectangle by the texture size here (it used to live in a separate VS to keep the geometry
    // shader divide-free; there is no GS now, so do it directly).
    float2 inv = 1.0 / (float2)item.TextureInfo.xy;
    float2 sourceXY = item.Source.xy * inv;
    float2 sourceZW = item.Source.zw * inv;

    float2 rotation = float2(cos(item.Rotation), sin(item.Rotation));
    float2 cornerCoord = TextureCornerCoords[corner];

    // Size of the sprite at this corner, then the corner's position relative to the rotation origin.
    float2 size = cornerCoord * item.Destination.zw;
    float2 position = size - origin;

    [flatten]
    if (item.Rotation != 0.0)
    {
        vertex.Position.x = item.Destination.x + (position.x * rotation.x) - (position.y * rotation.y);
        vertex.Position.y = item.Destination.y + (position.x * rotation.y) + (position.y * rotation.x);

        // We subtracted the origin above; move the point back to its original place.
        vertex.Position.xy += origin;
    }
    else
    {
        vertex.Position.xy = item.Destination.xy + size;
    }

    vertex.Position.z = item.Depth;
    vertex.Position.w = 1;
    vertex.Color = item.Color;

    float2 uvCorner = TextureCornerCoords[corner ^ item.SpriteEffect];
    vertex.UV = sourceXY + uvCorner * sourceZW;

    vertex.TextureId = item.TextureInfo.z;

    vertex.Position = mul(vertex.Position, MatrixTransform);
    return vertex;
}

float4 SpritePixelShader(PSInput input) : SV_TARGET
{
    switch (input.TextureId)
    {
        case -1:
            return input.Color;
        default:
            return Texture.Sample(TextureSampler, input.UV) * input.Color;
    }
}

technique SpriteBatch
{
    pass Render
    {
        EffectName = "SpriteEffect";
        Profile = 5.1;
        VertexShader = SpriteVertexShader;
        PixelShader = SpritePixelShader;
    }
}
