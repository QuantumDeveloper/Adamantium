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
    // The atlas array slice this glyph was packed into - read by the pixel stage, see the note below.
    float Layer : PSIZE2;
};

// ============================================================================================================
// THE ATLAS LAYER IS DYNAMIC AGAIN. It was pinned to a compile-time 0 for a long time: this driver's shader-object
// compiler (NVIDIA Quadro RTX 4000, VK_EXT_shader_object) AVd in vkCreateShadersEXT on ANY non-trivial
// Texture2DArray use here - a runtime layer index, an extra VS->PS varying, even a switch over constant indices -
// and bisection at the time left a single sample at a constant layer as the only form that compiled. Text was
// therefore limited to one 1024x1024 layer; glyphs the packer spilled to layers 1+ silently sampled layer 0.
//
// What changed is not the driver. This effect used to carry two passes nothing could reach (a gradient-derivative
// AA variant and an outline verification pass) and it was sitting at the limit where that compiler gives up - the
// same limit that made a second read of the transform table fatal. With those gone there is room, and the layer
// index compiles and runs. Measured: a cold compile still flakes (the AV is a floating one and hits any changed
// shader), but once through it starts 6 of 6 and the glyphs are the same as before.
//
// So if this ever AVs again, the question to ask is what the EFFECT has grown, not what the sampler is doing.
// ============================================================================================================

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
    // The rounded ancestor clip's SHAPE, fetched from the transform table in the VERTEX stage like every other family
    // (see ClipMath.fxh). That fetch is a SECOND read of the table from this shader, which used to kill the shader
    // compiler outright until the effect lost its two dead passes.
    nointerpolation float4 ClipBox : TEXCOORD1;
    nointerpolation float4 ClipRadii : TEXCOORD2;
    // The atlas array slice, as a runtime value - see the note above for why it was a compile-time 0 for so long.
    nointerpolation float Layer : TEXCOORD3;
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
// True-SDF blend band, in atlas texels per screen pixel. Below SdfBlendLo the glyph is magnified -> use the
// MSDF median (keeps sharp corners). Above SdfBlendHi it is minified -> use the single-channel true SDF
// (alpha), which stays crisp where median(bilinear) softens (its only cost, rounded corners, is sub-pixel
// at that size). Smoothly blended in between so there is no pop across the size threshold.
float SdfBlendLo;
float SdfBlendHi;

// The rounded ancestor clip for the PER-BLOCK (direct) draw, as plain uniforms: xy = the clip rect's origin in device
// pixels, zw = its size (zw = 0 means no clip), and the four corner radii. The batched pass fetches the same two
// values from the transform table by slot, which this pass cannot do - it never binds the table, and a first read
// from it would be a new compile shape on the effect that is tightest on this driver. One draw = one block = one
// clip, so a uniform says it exactly.
float4 DirectClipBox;
float4 DirectClipRadii;

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

// The rounded clip, shared with the UI's effects (one physical file, linked in - see Adamantium.FX.csproj). It reads
// NodeSlot and TransformsAddress, both declared just above, so it has to come AFTER them. NOTHING after the path.
#include "Includes/ClipMath.fxh"

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

    // The clip comes in as a uniform here (see DirectClipBox) instead of from the table by slot, and it has to be
    // WRITTEN either way: a varying this vertex shader does not set reaches the pixel shader as whatever was in the
    // register, and the batch shares this PSInput with it. A zero box is "no clip".
    vertex.Layer = item.Layer;
    vertex.ClipBox = DirectClipBox;
    vertex.ClipRadii = DirectClipRadii;

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

[shader("vertex")]
PSInput FontVertexShader(FontItem item, uint vertexId : SV_VertexID)
{
    return ExpandGlyphCorner(item, (int)vertexId);   // vertexId 0..3 = strip corner
}

// Canonical MSDF reconstruction (Chlumsky). ScreenPxRange() gives the field slope in screen pixels.
// FontWeight shifts the 0.5 contour INSIDE the ScreenPxRange term (a true distance bias, not an opacity
// add), so it makes stems thinner/thicker without hazing the background. Selected via the RenderMsdf pass,
// toggled from FontRenderer.UseCanonicalMsdf.
[shader("fragment")]
float4 FontPixelShaderMsdf(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, input.Layer));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight) + 0.5, 0.0, 1.0);
    // Gamma-boost the coverage (same as the gradient pass): raises partial opacities so thin stems keep
    // their colour instead of washing out toward the background. The engine blends in sRGB, so this also
    // compensates the perceptual lightening of un-gamma-corrected coverage AA.
    //
    // The boost is taken on the PRODUCT, colour alpha included, and that is NOT an oversight to "fix" by reordering:
    // splitting them (pow(coverage) * alpha) was tried twice and both times made text look washed out. The reason is
    // that UI text is rarely fully opaque - secondary text in the theme is not - so the boost has been carrying that
    // alpha too, and taking it out thins every half-covered stem.
    // The ELEMENT's fade is a different number and does not belong under the boost either. The CPU folds it into
    // ForegroundColor.a for this pass, so it arrives ALREADY raised to 2.2 and the boost hands it back linear - the
    // same trick the batch shader plays in its vertex stage (FontRenderer.DrawLayoutDirect).
    float alpha = pow(ForegroundColor.a * opacity, 1.0 / 2.2);
    // The rounded ancestor clip, as coverage, exactly as the batch pass applies it. Both the premultiplied colour and
    // the alpha are cut: this pass outputs rgb*alpha, so cutting one without the other leaves colour where the glyph
    // was cut away. A zero-size box gives 1 and costs nothing.
    alpha *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(ForegroundColor.rgb * alpha, alpha);
}

// Batch variant of FontPixelShaderMsdf: the foreground comes from the per-instance vertex colour (input.Color,
// baked per glyph on the CPU) instead of the ForegroundColor uniform, so ONE instanced draw can render glyphs
// of many text blocks - each its own colour - from the shared atlas. Identical MSDF reconstruction otherwise.
// Used by the CPU pre-transform text batch (docs/TEXT_GLYPH_BATCH_PLAN.md sec. 9 Stage 2). input.Color is a plain
// interpolated attribute (no matrix), so it is driver-safe on this Turing.
[shader("fragment")]
float4 FontPixelShaderMsdfBatch(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, input.Layer));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight) + 0.5, 0.0, 1.0);
    // Unchanged on purpose - the element's fade is pre-compensated in the vertex stage so that this very boost hands
    // it back linear. See the FADE line in FontBatchInstancedVS.
    float alpha = pow(input.Color.a * opacity, 1.0 / 2.2);
    // The rounded ancestor clip, as coverage. Applied to the PREMULTIPLIED colour as well as the alpha - this pass
    // outputs rgb*alpha, so cutting only the alpha would leave the colour standing where the glyph was cut away.
    alpha *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(input.Color.rgb * alpha, alpha);
}

// ---- Instanced glyph batch: per-instance GlyphData read from a BDA STORAGE buffer by SV_InstanceID (mirrors
// RectBatchInstancedVS in BatchEffect.fx); the quad comes from SV_VertexID. Node-local glyph rects are transformed to
// world on the GPU by the instance's transform-table slot (0 = identity), so a scrolling block moves via one matrix
// write, not a per-glyph CPU re-bake, and the batch is node-aware. Reuses FontPixelShaderMsdfBatch (per-instance colour).
struct GlyphData
{
    float4 LocalRect;   // node-local x, y, w, h (world for slot-0 legacy bakes)
    float4 Source;      // atlas UV rect
    float4 Params;      // .x = transform-table slot; .y = atlas layer (read by the PS); .z = depth; .w reserved
    float4 Clip;        // .x = the ROUNDED CLIP's slot, or -1; .yzw spare
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
    // The element's alpha from the OPACITY SLOT, exactly as every other family reads it: a fading ancestor then moves
    // one number in the table instead of re-baking every glyph under it. Params.w is -1 when nothing above fades.
    float fadeSlot = g.Params.w;
    float fade = nodes[(uint)max(fadeSlot, 0.0)].Params.x;
    fade = lerp(1.0, fade, step(0.0, fadeSlot));
    // FADE, pre-compensated for the pixel shader's gamma boost. That boost is taken on the whole product
    // (pow(Color.a * coverage, 1/2.2)) and must stay that way - moving the colour's alpha out of it washes text out,
    // tried twice. But the ELEMENT's fade is not coverage, and inside the boost it came out too strong: a block at
    // Opacity 0.5 kept 0.755 of its ink while every shape beside it was at 0.501. Raising it to 2.2 here makes the
    // boost hand back exactly `fade`, and at fade = 1 the expression is the old one unchanged.
    // Carrying it as a fifth varying instead works too - measured, the effect compiles with one - but it spends an
    // interpolant on the effect that is tightest on this driver, and makes the pixel shader carry the knowledge.
    o.Color = float4(g.Color.rgb, g.Color.a * pow(fade, 2.2));
    // The clip's shape, from the table by the slot the record carries - one fetch per instance, as everywhere else.
    // Together with the fade this shader now reads that table THREE times; it used to AV the compiler on the second,
    // and what made the difference is the two dead passes this effect no longer carries.
    o.Layer = g.Params.y;   // the atlas layer this glyph was packed into
    o.ClipBox = ClipShapeBox(g.Clip.x);
    o.ClipRadii = ClipShapeRadii(g.Clip.x);
    return o;
}

technique FontBatch
{
    pass RenderMsdf
    {
        EffectName = "FontEffectMsdf";
        Profile = 5.1;
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdf;
    }

    pass RenderMsdfBatchInstanced
    {
        EffectName = "FontEffectMsdfBatchInstanced";
        Profile = 6.6;
        VertexShader = FontBatchInstancedVS;
        PixelShader = FontPixelShaderMsdfBatch;
    }
}
