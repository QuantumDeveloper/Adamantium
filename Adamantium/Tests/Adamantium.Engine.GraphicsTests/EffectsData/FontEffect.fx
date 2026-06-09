const static float EPSILON = 1.401298E-45;
const static int VERTICES_PER_SPRITE = 4;

struct FontItem
{
    float4 Destination: Position;
    float4 Source: TEXCOORD0;
    float2 Origin: TEXCOORD1;
    float Depth : PSIZE0;
    float Rotation : PSIZE1;
    float4 Color: COLOR0;
    int SpriteEffect : BLENDINDICES0;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
};

Texture2D Texture;
SamplerState TextureSampler;
float2 TextureCornerCoords[4];
matrix MatrixTransform;
float4 ForegroundColor;

void GenerateSprite(FontItem item, inout TriangleStream<PSInput> triStream)
{
    PSInput vertex;
    float2 origin = item.Origin;

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
        vertex.UV = item.Source.xy + corner * item.Source.zw;

        float4 pos = mul(vertex.Position, MatrixTransform);
        vertex.Position = pos;

        triStream.Append(vertex);
    }
}

float median(float a, float b, float c)
{
    return max(min(a,b), min(max(a,b), c));
}

void FontVertexShader(inout FontItem input) {}

[maxvertexcount(4)]
void FontItemGenerationGS(point FontItem input[1], inout TriangleStream<PSInput> triStream)
{
    GenerateSprite(input[0], triStream);
}

float4 FontPixelShader(PSInput input) : SV_TARGET
{
    float3 sample = Texture.Sample(TextureSampler, input.UV).rgb;
    int2 sz;
    Texture.GetDimensions(sz.x, sz.y);
    float dx = ddx( input.UV.x ) * sz.x;
    float dy = ddy( input.UV.y ) * sz.y;
    float toPixels = 5.0 * rsqrt( dx * dx + dy * dy );
    float sigDist = median( sample.r, sample.g, sample.b ) - 0.5;
    float opacity = clamp( sigDist * toPixels + 0.5, 0.0, 1.0 );
    
    float4 color = float4(ForegroundColor.rgb, opacity);
    
    return color;
}

technique FontBatch
{
    pass Render
    {
        EffectName = "FontEffect";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        GeometryShader = FontItemGenerationGS;
        PixelShader = FontPixelShader;
    }
}