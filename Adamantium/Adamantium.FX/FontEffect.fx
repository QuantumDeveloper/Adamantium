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

Texture2D Texture : register(t1);
SamplerState TextureSampler : register(s1);

matrix MatrixTransform;
float2 TextureCornerCoords[4];
float4 ForegroundColor;
float FontSize;
float FontSizeThreshold;
float FontWeight;
float PxRange;
float2 MSDFAtlasSize;
float4 StrokeColor;

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

float Median(float r, float g, float b)
{
    return max(min(r,g), min(max(r,g), b));
}

float screenPxRange(float2 uv) 
{
//    uint2 textureSize;
//    Texture.GetDimensions(textureSize.x, textureSize.y);
    float2 unitRange = float2(PxRange, PxRange) / MSDFAtlasSize;
    float2 screenTexSize = float2(1.0, 1.0)/fwidth(uv);
    return max(0.5*dot(unitRange, screenTexSize), 1.0);
}

FontItem FontVertexShader(FontItem input) 
{
    return input;
}

[maxvertexcount(4)]
void FontItemGenerationGS(point FontItem input[1], inout TriangleStream<PSInput> triStream)
{
    GenerateSprite(input[0], triStream);
}


float2 SafeNormalize(in float2 v)
{
	float len = length(v);
	len = (len > 0.0) ? 1.0 / len : 0.0;
	return v * len;
}

float4 FontPixelShader(PSInput input) : SV_TARGET
{
    /*
    // Large text
    if (FontSize > FontSizeThreshold)
    {
        float2 msdfUnit = PxRange / MSDFAtlasSize;
        float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;
    
        float sigDist = Median(samp.r, samp.g, samp.b) - 0.5f;
        sigDist = sigDist * dot(msdfUnit, 0.5f / fwidth(input.UV));
    
        float opacity = clamp(sigDist + 0.5f, 0.0f, 1.0f);
        return input.Color * opacity;
    }
    else // Small text
    */
    {
        float2 uv = input.UV * MSDFAtlasSize;
        float2 Jdx = ddx(uv);
        float2 Jdy = ddy(uv);
        float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;
    
        // Calculate the signed distance (in texels). FontWeight shifts the contour (0.5) to make stems
        // thinner/thicker; it's a constant, so it drops out of the ddx/ddy gradient below.
        float sigDist = Median(samp.r, samp.g, samp.b) - 0.5f + FontWeight;

        // For proper anti-aliasing we need to calculate the signed distance in pixels.
        // We do this using the derivatives.
        float2 gradDist = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
        float2 grad = float2(gradDist.x * Jdx.x + gradDist.y * Jdy.x, gradDist.x * Jdx.y + gradDist.y * Jdy.y);
        
        // Apply anti-aliasing
        const float thickness = 0.125f;
        const float normalization = thickness * 0.5f * sqrt(2.0f);
        
        float afWidth = min(normalization * length(grad), 0.5f);
        float opacity = smoothstep(0.0f - afWidth, 0.0f + afWidth, sigDist);
        
        // Apply pre-multiplied alpha with gamma correction
        
        float4 color;
        color.a = pow(abs(ForegroundColor.a * opacity), 1.0f / 2.2f);
        color.rgb = ForegroundColor.rgb * color.a;
        return color;
    }
}

// Canonical MSDF reconstruction (Chlumsky). screenPxRange() gives the field slope in screen pixels.
// FontWeight shifts the 0.5 contour INSIDE the screenPxRange term (a true distance bias, not an opacity
// add), so it makes stems thinner/thicker without hazing the background. Selected via the RenderMsdf pass,
// toggled from FontRenderer.UseCanonicalMsdf.
float4 FontPixelShaderMsdf(PSInput input) : SV_TARGET
{
    float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;
    float sd = Median(samp.r, samp.g, samp.b);
    float opacity = clamp(screenPxRange(input.UV) * (sd - 0.5f + FontWeight) + 0.5f, 0.0f, 1.0f);
    // Gamma-boost the coverage (same as the gradient pass): raises partial opacities so thin stems keep
    // their colour instead of washing out toward the background. The engine blends in sRGB, so this also
    // compensates the perceptual lightening of un-gamma-corrected coverage AA.
    float alpha = pow(ForegroundColor.a * opacity, 1.0f / 2.2f);
    return float4(ForegroundColor.rgb * alpha, alpha);
}

float4 StrokedTextPS(PSInput input) : SV_TARGET
{
    if (FontSize > FontSizeThreshold) // large stroke
    {
        float2 msdfUnit = PxRange / MSDFAtlasSize;
        float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;

        float sigDist = Median(samp.r, samp.g, samp.b) - 0.5f;
        sigDist = sigDist * dot(msdfUnit, 0.5f / fwidth(input.UV));
        const float strokeThickness = 0.250f * 0.75f;
        float strokeDist = Median(samp.r, samp.g, samp.b) - 0.25f - strokeThickness;
        strokeDist = -(abs(strokeDist) - strokeThickness);
        strokeDist = strokeDist * dot(msdfUnit, 0.5f / fwidth(input.UV));

        float opacity = clamp(sigDist + 0.5f, 0.0f, 1.0f);
        float strokeOpacity = clamp(strokeDist + 0.5f, 0.0f, 1.0f);
        return lerp(StrokeColor, ForegroundColor, opacity) * max(opacity, strokeOpacity);
    }
    else // small stroked text
    {
        float2 uv = input.UV * MSDFAtlasSize;
        float2 Jdx = ddx(uv);
        float2 Jdy = ddy(uv);
        float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;

	    // Calculate the signed distance (in texels)
        const float strokeThickness = 0.250f * 0.75f;
        float StrokeDist = Median(samp.r, samp.g, samp.b) - 0.25f - strokeThickness;
        StrokeDist = -(abs(StrokeDist) - strokeThickness);
        float sigDist = Median(samp.r, samp.g, samp.b) - 0.5f;

	    // For proper anti-aliasing we need to calculate the signed distance in pixels.
	    // We do this using the derivatives.
        float2 gradDist = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
        float2 grad = float2(gradDist.x * Jdx.x + gradDist.y * Jdy.x, gradDist.x * Jdx.y + gradDist.y * Jdy.y);
        const float thickness = 0.125f;
        const float normalization = thickness * 0.5f * sqrt(2.0f);
        float afWidth = min(normalization * length(grad), 0.5f);
        float opacity = smoothstep(0.0f - afWidth, 0.0f + afWidth, sigDist);
        float strokeOpacity = smoothstep(0.0f - afWidth, 0.0f + afWidth, StrokeDist);
	
        return lerp(StrokeColor, ForegroundColor, opacity) * max(opacity, strokeOpacity);
    }
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

    pass RenderMsdf
    {
        EffectName = "FontEffectMsdf";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        GeometryShader = FontItemGenerationGS;
        PixelShader = FontPixelShaderMsdf;
    }

    pass StrokedText
    {
        EffectName = "StrokedFontEffect";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        GeometryShader = FontItemGenerationGS;
        PixelShader = StrokedTextPS;
    }
}