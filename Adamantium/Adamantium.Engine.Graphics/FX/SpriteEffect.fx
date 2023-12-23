const static float EPSILON = 1.401298E-45;
const static int VERTICES_PER_SPRITE = 4;

struct SpriteItem
{
    float4 Destination: SV_Position;
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

void GenerateSprite(SpriteItem item, inout TriangleStream<PSInput> triStream)
{
    PSInput vertex;
    float2 origin = item.Origin;

    float2 sourceXY = item.Source.xy / item.TextureInfo.xy;
    float2 sourceZW = item.Source.zw / item.TextureInfo.xy;
    float2 rotation = float2(cos(item.Rotation), sin(item.Rotation));

    for (int i = 0; i < VERTICES_PER_SPRITE; i++)
    {
        // Gets the corner and take into account the Flip mode.
        float2 corner = TextureCornerCoords[i];

        //Calculate size of sprite in current point 
        float2 size = corner * item.Destination.zw;
        //origin of sprite for current point
        float2 position = size - origin;

        [flatten]
        if (item.Rotation != 0.0)
        {
            vertex.Position.x = item.Destination.x + (position.x * rotation.x) - (position.y * rotation.y);
            vertex.Position.y = item.Destination.y + (position.x * rotation.y) + (position.y * rotation.x);

            //Because earlier we made "position - origin", now we move point back to its original position 
            vertex.Position.xy += origin;
        }
        else
        {
            vertex.Position.xy = item.Destination.xy + size;
        }

        vertex.Position.z = item.Depth;
        vertex.Position.w = 1;
        vertex.Color = item.Color;

        corner = TextureCornerCoords[i ^ item.SpriteEffect];
        vertex.UV = sourceXY + corner * sourceZW;

        vertex.TextureId = item.TextureInfo.z;

        float4 pos = mul(vertex.Position, MatrixTransform);
        vertex.Position = pos;

        triStream.Append(vertex);
    }
}

void SpriteVertexShader(inout SpriteItem input) {}

[maxvertexcount(4)]
void SpriteGenerationGS(point SpriteItem input[1], inout TriangleStream<PSInput> triStream)
{
    GenerateSprite(input[0], triStream);
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
        GeometryShader = SpriteGenerationGS;
        PixelShader = SpritePixelShader;
    }
}