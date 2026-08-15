static const float EPSILON = 1.401298E-45;
static const int VERTICES_PER_SPRITE = 4;

struct FontItem
{
    float4 Destination: Position;
    float4 Source: TEXCOORD0;
    float2 Origin: TEXCOORD1;
    float Depth : PSIZE0;
    float Rotation : PSIZE1;
    float4 Color: COLOR0;
    int SpriteEffect : BLENDINDICES0;
    // The C# FontItem sends the atlas array slice here (per glyph), but the shader does NOT read it yet - see the
    // DRIVER-BUG note below. Kept so the pipeline is ready to go dynamic without a vertex-format change.
    float Layer : PSIZE2;
};

// ============================================================================================================
// DRIVER BUG WORKAROUND (NVIDIA Quadro RTX 4000, VK_EXT_shader_object). This driver's shader-object compiler AVs
// (vkCreateShadersEXT, 0xC0000005) on ANY non-trivial Texture2DArray use in this shader: a runtime layer index, an
// extra VS->PS varying, OR a switch of constant-index samples ALL crash it. Bisection proved the ONLY form that
// compiles is a SINGLE sample with a COMPILE-TIME-CONSTANT layer (validation-clean, so a pure driver compiler bug).
// So for now we sample layer 0 (constant) and carry NO layer varying. The atlas IS a real Texture2DArray and the C#
// side packs glyphs across layers, so when the driver is fixed, GO DYNAMIC by:
//   1) add `nointerpolation float Layer : TEXCOORD2;` to PSInput
//   2) in ExpandGlyphCorner: `vertex.Layer = item.Layer;`
//   3) replace each `float3(input.UV, 0)` below with `float3(input.UV, input.Layer)`
// Until then only layer-0 glyphs render correctly (~one 1024x1024 layer's worth); glyphs the packer spills to layers
// 1+ will mis-sample (they read layer 0) - NOT a crash. Effective capacity today ~= the old single-layer atlas.
// ============================================================================================================

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
};

Texture2DArray Texture : register(t1);
SamplerState TextureSampler : register(s1);

float4x4 MatrixTransform;
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

// BDA (buffer device address) storage for the INSTANCED glyph batch (pass RenderMsdfBatchInstanced) - the SAME proven
// pattern BatchEffect.fx uses for rect/ellipse fills. GlyphInstancesAddress -> the per-instance GlyphData buffer;
// TransformsAddress -> the transform table the VS indexes by each glyph's slot (slot 0 = identity). This lets glyph rects
// be uploaded ONCE in node-local space and transformed to world on the GPU, so a scrolling block moves via one matrix
// write instead of a per-glyph CPU re-bake (and the text batch becomes node-aware). See docs/RENDER_THREAD_PLAN.md.
uint64_t GlyphInstancesAddress;
uint64_t TransformsAddress;

// One entry of the transform table (see Adamantium.UI/Rendering/TransformTable.cs). It carries the node's ALPHA beside
// its matrix - both are one node's state, and this shader must know the layout even though it only reads the matrix,
// or it would stride through the buffer wrong.
struct NodeSlot
{
    float4x4 World;
    float4   Params;   // .x = alpha (1 = opaque); .yzw reserved
};

// Per-glyph quad expansion, now in the VERTEX stage (corner from SV_VertexID), so the geometry shader is gone:
// plain instanced rendering (4-vertex triangle strip x N glyphs), portable to Metal/MoltenVK and free of the
// NVIDIA Turing GS NVVM bug.
PSInput ExpandGlyphCorner(FontItem item, int corner)
{
    PSInput vertex;
    float2 origin = item.Origin;
    float2 rotation = float2(cos(item.Rotation), sin(item.Rotation));

    float2 cornerCoord = TextureCornerCoords[corner];
    float2 size = cornerCoord * item.Destination.zw;
    float2 position = size - origin;

    [flatten]
    if (item.Rotation != 0.0)
    {
        vertex.Position.x = item.Destination.x + (position.x * rotation.x) - (position.y * rotation.y);
        vertex.Position.y = item.Destination.y + (position.x * rotation.y) + (position.y * rotation.x);
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
    vertex.UV = item.Source.xy + uvCorner * item.Source.zw;

    vertex.Position = mul(vertex.Position, MatrixTransform);
    return vertex;
}

float Median(float r, float g, float b)
{
    return max(min(r, g), min(max(r, g), b));
}

float ScreenPxRange(float2 uv)
{
    float2 unitRange = float2(PxRange, PxRange) / MSDFAtlasSize;
    float2 screenTexSize = float2(1.0, 1.0) / fwidth(uv);
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
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

float2 SafeNormalize(float2 v)
{
    float len = length(v);
    len = (len > 0.0) ? 1.0 / len : 0.0;
    return v * len;
}

[shader("vertex")]
PSInput FontVertexShader(FontItem item, uint vertexId : SV_VertexID)
{
    return ExpandGlyphCorner(item, (int)vertexId);   // vertexId 0..3 = strip corner
}

[shader("fragment")]
float4 FontPixelShader(PSInput input) : SV_Target
{
    float2 uv = input.UV * MSDFAtlasSize;
    float2 Jdx = ddx(uv);
    float2 Jdy = ddy(uv);
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, 0));

    // Signed distance (normalized, contour at 0.5). FontWeight shifts the contour for thinner/thicker
    // stems. Coverage source switches MSDF -> true-SDF with minification (see SampleGlyphCoverage).
    float sigDist = SampleGlyphCoverage(samp, input.UV) - 0.5 + FontWeight;

    // Anti-alias over ~1 screen pixel. The MAGNITUDE comes from the geometry derivatives (Jdx/Jdy = how
    // many atlas texels map to one screen pixel) - smooth and robust, so thin stems don't drop out; the
    // field derivatives give only the edge DIRECTION (their magnitude aliases on sub-pixel stems). The
    // 0.5/PxRange factor is half a screen pixel and scales with PxRange - the old code hardcoded it to
    // ~0.5/6, so at PxRange=16 the edge was ~2.8x too soft and clamped to the max -> permanent blur.
    float2 gradDir = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
    float2 grad = float2(gradDir.x * Jdx.x + gradDir.y * Jdy.x, gradDir.x * Jdx.y + gradDir.y * Jdy.y);
    float afWidth = min(0.5 / PxRange * length(grad), 0.5);
    float opacity = smoothstep(-afWidth, afWidth, sigDist);

    // Pre-multiplied alpha with gamma correction.
    float4 color;
    color.a = pow(abs(ForegroundColor.a * opacity), 1.0 / 2.2);
    color.rgb = ForegroundColor.rgb * color.a;
    return color;
}

// Canonical MSDF reconstruction (Chlumsky). ScreenPxRange() gives the field slope in screen pixels.
// FontWeight shifts the 0.5 contour INSIDE the ScreenPxRange term (a true distance bias, not an opacity
// add), so it makes stems thinner/thicker without hazing the background. Selected via the RenderMsdf pass,
// toggled from FontRenderer.UseCanonicalMsdf.
[shader("fragment")]
float4 FontPixelShaderMsdf(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, 0));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight) + 0.5, 0.0, 1.0);
    // Gamma-boost the coverage (same as the gradient pass): raises partial opacities so thin stems keep
    // their colour instead of washing out toward the background. The engine blends in sRGB, so this also
    // compensates the perceptual lightening of un-gamma-corrected coverage AA.
    float alpha = pow(ForegroundColor.a * opacity, 1.0 / 2.2);
    return float4(ForegroundColor.rgb * alpha, alpha);
}

// Batch variant of FontPixelShaderMsdf: the foreground comes from the per-instance vertex colour (input.Color,
// baked per glyph on the CPU) instead of the ForegroundColor uniform, so ONE instanced draw can render glyphs
// of many text blocks - each its own colour - from the shared atlas. Identical MSDF reconstruction otherwise.
// Used by the CPU pre-transform text batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2). input.Color is a plain
// interpolated attribute (no matrix), so it is driver-safe on this Turing.
[shader("fragment")]
float4 FontPixelShaderMsdfBatch(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, 0));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight) + 0.5, 0.0, 1.0);
    float alpha = pow(input.Color.a * opacity, 1.0 / 2.2);
    return float4(input.Color.rgb * alpha, alpha);
}

// MSDF + outline. Reconstructs two coverages from the SAME field: the glyph body (contour at 0.5) and an
// outer shape that extends OutlineWidth (normalized) OUTSIDE the contour. The ring between them is drawn in
// OutlineColor. This is the verification effect: a visible outline at a real offset proves the distance
// field is valid beyond the contour (only possible with the widened PxRange - a thin field saturates to
// black a couple texels out and the ring collapses).
[shader("fragment")]
float4 FontPixelShaderMsdfOutline(PSInput input) : SV_Target
{
    float3 samp = Texture.Sample(TextureSampler, float3(input.UV, 0)).rgb;
    float sd = Median(samp.r, samp.g, samp.b);
    float screenPx = ScreenPxRange(input.UV);

    // Body coverage (same as the plain MSDF pass) and the outer (body + outline) coverage.
    float fill = clamp(screenPx * (sd - 0.5 + FontWeight) + 0.5, 0.0, 1.0);
    float outer = clamp(screenPx * (sd - 0.5 + OutlineWidth + FontWeight) + 0.5, 0.0, 1.0);

    // Inside -> foreground, the ring (outer but not fill) -> outline colour.
    float3 rgb = lerp(OutlineColor.rgb, ForegroundColor.rgb, fill);
    float alpha = pow(ForegroundColor.a * outer, 1.0 / 2.2);
    return float4(rgb * alpha, alpha);
}

[shader("fragment")]
float4 StrokedTextPS(PSInput input) : SV_Target
{
    if (FontSize > FontSizeThreshold) // large stroke
    {
        float2 msdfUnit = PxRange / MSDFAtlasSize;
        float3 samp = Texture.Sample(TextureSampler, float3(input.UV, 0)).rgb;

        float sigDist = Median(samp.r, samp.g, samp.b) - 0.5;
        sigDist = sigDist * dot(msdfUnit, 0.5 / fwidth(input.UV));
        const float strokeThickness = 0.250 * 0.75;
        float strokeDist = Median(samp.r, samp.g, samp.b) - 0.25 - strokeThickness;
        strokeDist = -(abs(strokeDist) - strokeThickness);
        strokeDist = strokeDist * dot(msdfUnit, 0.5 / fwidth(input.UV));

        float opacity = clamp(sigDist + 0.5, 0.0, 1.0);
        float strokeOpacity = clamp(strokeDist + 0.5, 0.0, 1.0);
        return lerp(StrokeColor, ForegroundColor, opacity) * max(opacity, strokeOpacity);
    }
    else // small stroked text
    {
        float2 uv = input.UV * MSDFAtlasSize;
        float2 Jdx = ddx(uv);
        float2 Jdy = ddy(uv);
        float4 samp = Texture.Sample(TextureSampler, float3(input.UV, 0));

        // Same coverage source as the plain text pass: MSDF when magnified, true-SDF when minified.
        float coverage = SampleGlyphCoverage(samp, input.UV);
        float sigDist = coverage - 0.5;
        const float strokeThickness = 0.250 * 0.75;
        float strokeDist = -(abs(coverage - 0.25 - strokeThickness) - strokeThickness);

        // Same screen-pixel AA as the plain text pass: magnitude from the geometry derivatives (Jdx/Jdy) so
        // thin stems survive, scaled by 0.5/PxRange (half a screen pixel, scales with PxRange - no hardcoded
        // constant). The field derivatives give only the edge direction.
        float2 gradDir = SafeNormalize(float2(ddx(sigDist), ddy(sigDist)));
        float2 grad = float2(gradDir.x * Jdx.x + gradDir.y * Jdy.x, gradDir.x * Jdx.y + gradDir.y * Jdy.y);
        float afWidth = min(0.5 / PxRange * length(grad), 0.5);
        float opacity = smoothstep(-afWidth, afWidth, sigDist);
        float strokeOpacity = smoothstep(-afWidth, afWidth, strokeDist);

        return lerp(StrokeColor, ForegroundColor, opacity) * max(opacity, strokeOpacity);
    }
}

// ---- Instanced glyph batch: per-instance GlyphData read from a BDA STORAGE buffer by SV_InstanceID (mirrors
// RectBatchInstancedVS in BatchEffect.fx); the quad comes from SV_VertexID. Node-local glyph rects are transformed to
// world on the GPU by the instance's transform-table slot (0 = identity), so a scrolling block moves via one matrix
// write, not a per-glyph CPU re-bake, and the batch is node-aware. Reuses FontPixelShaderMsdfBatch (per-instance colour).
struct GlyphData
{
    float4 LocalRect;   // node-local x, y, w, h (world for slot-0 legacy bakes)
    float4 Source;      // atlas UV rect
    float4 Params;      // .x = transform-table slot; .y = atlas layer (PS constant-0 today); .z = depth; .w reserved
    float4 Color;       // straight RGBA, element/brush opacity folded into .w
};

[shader("vertex")]
PSInput FontBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    GlyphData* items = (GlyphData*)GlyphInstancesAddress;
    GlyphData g = items[instanceId];

    PSInput o;
    // SAME corner mapping as ExpandGlyphCorner (TextureCornerCoords[vertexId]) so the quad + UV match the direct path.
    float2 corner = TextureCornerCoords[vertexId];
    float2 localPos = g.LocalRect.xy + corner * g.LocalRect.zw;
    // Node-local -> world via the instance's transform-table matrix (slot 0 = identity for legacy world bakes).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)g.Params.x].World;
    float4 worldPos = mul(float4(localPos, g.Params.z, 1.0), nodeWorld);
    o.Position = mul(worldPos, MatrixTransform);   // MatrixTransform = the (transposed-on-upload) projection
    o.UV = g.Source.xy + corner * g.Source.zw;     // SpriteEffect == 0 for batched glyphs
    o.Color = g.Color;
    return o;
}

technique FontBatch
{
    pass Render
    {
        EffectName = "FontEffect";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShader;
    }

    pass RenderMsdf
    {
        EffectName = "FontEffectMsdf";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdf;
    }

    pass RenderMsdfBatch
    {
        EffectName = "FontEffectMsdfBatch";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdfBatch;
    }

    pass RenderMsdfBatchInstanced
    {
        EffectName = "FontEffectMsdfBatchInstanced";
        Profile = 6.6;
        VertexShader = FontBatchInstancedVS;
        PixelShader = FontPixelShaderMsdfBatch;
    }

    pass RenderMsdfOutline
    {
        EffectName = "FontEffectMsdfOutline";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdfOutline;
    }

    pass StrokedText
    {
        EffectName = "StrokedFontEffect";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = StrokedTextPS;
    }
}
