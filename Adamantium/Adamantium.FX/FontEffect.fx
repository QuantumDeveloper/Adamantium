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
// Outline effect (RenderMsdfOutline pass). OutlineWidth is the ring thickness in normalized field units
// (sd is 0..1 with the contour at 0.5 and the field edge at 0, so 0.25 ~= PxRange*0.25 texels outside the
// glyph). The ring is only visible because the field carries real distance that far out (wide PxRange) -
// it doubles as a check that the widened field works beyond the contour.
float OutlineWidth;
float4 OutlineColor;

// True-SDF blend band, in atlas texels per screen pixel. Below SdfBlendLo the glyph is magnified -> use the
// MSDF median (keeps sharp corners). Above SdfBlendHi it is minified -> use the single-channel true SDF
// (alpha), which stays crisp where median(bilinear) softens (its only cost, rounded corners, is sub-pixel
// at that size). Smoothly blended in between so there is no pop across the size threshold.
float SdfBlendLo;
float SdfBlendHi;

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

// Coverage source for the glyph body: MSDF median when magnified (sharp corners), true-SDF alpha when
// minified (crisp where median(bilinear) softens), blended by the minification factor = the max UV
// derivative in atlas texels (the standard texture-LOD metric). With SdfBlendLo >= SdfBlendHi (or both very
// large) it stays pure MSDF, so the blend can be disabled purely via the uniforms - no hardcoded switch.
float SampleGlyphCoverage(float4 samp, float2 uv)
{
    float msdf = Median(samp.r, samp.g, samp.b);
    float texelsPerPx = max(length(ddx(uv) * MSDFAtlasSize), length(ddy(uv) * MSDFAtlasSize));
    float t = smoothstep(SdfBlendLo, SdfBlendHi, texelsPerPx);
    return lerp(msdf, samp.a, t);
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
        float4 samp = Texture.Sample(TextureSampler, input.UV);

        // Signed distance (normalized, contour at 0.5). FontWeight shifts the contour for thinner/thicker
        // stems. Coverage source switches MSDF -> true-SDF with minification (see SampleGlyphCoverage).
        float sigDist = SampleGlyphCoverage(samp, input.UV) - 0.5f + FontWeight;

        // Anti-alias over ~1 screen pixel. The MAGNITUDE comes from the geometry derivatives (Jdx/Jdy = how
        // many atlas texels map to one screen pixel) - smooth and robust, so thin stems don't drop out; the
        // field derivatives give only the edge DIRECTION (their magnitude aliases on sub-pixel stems). The
        // 0.5/PxRange factor is half a screen pixel and scales with PxRange - the old code hardcoded it to
        // ~0.5/6, so at PxRange=16 the edge was ~2.8x too soft and clamped to the max -> permanent blur.
        float2 gradDir = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
        float2 grad = float2(gradDir.x * Jdx.x + gradDir.y * Jdy.x, gradDir.x * Jdx.y + gradDir.y * Jdy.y);
        float afWidth = min(0.5f / PxRange * length(grad), 0.5f);
        float opacity = smoothstep(-afWidth, afWidth, sigDist);
        
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
    float4 samp = Texture.Sample(TextureSampler, input.UV);
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(screenPxRange(input.UV) * (sd - 0.5f + FontWeight) + 0.5f, 0.0f, 1.0f);
    // Gamma-boost the coverage (same as the gradient pass): raises partial opacities so thin stems keep
    // their colour instead of washing out toward the background. The engine blends in sRGB, so this also
    // compensates the perceptual lightening of un-gamma-corrected coverage AA.
    float alpha = pow(ForegroundColor.a * opacity, 1.0f / 2.2f);
    return float4(ForegroundColor.rgb * alpha, alpha);
}

// MSDF + outline. Reconstructs two coverages from the SAME field: the glyph body (contour at 0.5) and an
// outer shape that extends OutlineWidth (normalized) OUTSIDE the contour. The ring between them is drawn in
// OutlineColor. This is the verification effect: a visible outline at a real offset proves the distance
// field is valid beyond the contour (only possible with the widened PxRange - a thin field saturates to
// black a couple texels out and the ring collapses).
float4 FontPixelShaderMsdfOutline(PSInput input) : SV_TARGET
{
    float3 samp = Texture.Sample(TextureSampler, input.UV).rgb;
    float sd = Median(samp.r, samp.g, samp.b);
    float screenPx = screenPxRange(input.UV);

    // Body coverage (same as the plain MSDF pass) and the outer (body + outline) coverage.
    float fill = clamp(screenPx * (sd - 0.5f + FontWeight) + 0.5f, 0.0f, 1.0f);
    float outer = clamp(screenPx * (sd - 0.5f + OutlineWidth + FontWeight) + 0.5f, 0.0f, 1.0f);

    // Inside -> foreground, the ring (outer but not fill) -> outline colour.
    float3 rgb = lerp(OutlineColor.rgb, ForegroundColor.rgb, fill);
    float alpha = pow(ForegroundColor.a * outer, 1.0f / 2.2f);
    return float4(rgb * alpha, alpha);
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
        float4 samp = Texture.Sample(TextureSampler, input.UV);

        // Same coverage source as the plain text pass: MSDF when magnified, true-SDF when minified.
        float coverage = SampleGlyphCoverage(samp, input.UV);
        float sigDist = coverage - 0.5f;
        const float strokeThickness = 0.250f * 0.75f;
        float strokeDist = -(abs(coverage - 0.25f - strokeThickness) - strokeThickness);

        // Same screen-pixel AA as the plain text pass: magnitude from the geometry derivatives (Jdx/Jdy) so
        // thin stems survive, scaled by 0.5/PxRange (half a screen pixel, scales with PxRange - no hardcoded
        // constant). The field derivatives give only the edge direction.
        float2 gradDir = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
        float2 grad = float2(gradDir.x * Jdx.x + gradDir.y * Jdy.x, gradDir.x * Jdx.y + gradDir.y * Jdy.y);
        float afWidth = min(0.5f / PxRange * length(grad), 0.5f);
        float opacity = smoothstep(-afWidth, afWidth, sigDist);
        float strokeOpacity = smoothstep(-afWidth, afWidth, strokeDist);

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

    pass RenderMsdfOutline
    {
        EffectName = "FontEffectMsdfOutline";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        GeometryShader = FontItemGenerationGS;
        PixelShader = FontPixelShaderMsdfOutline;
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