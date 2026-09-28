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

// The atlas layer is a runtime index, so glyphs the packer spills to layers 1+ are sampled from their own layer.

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
    // The rounded ancestor clip's SHAPE, fetched from the transform table in the VERTEX stage like every other family
    // (see ClipFromSlot). That fetch is a SECOND read of the table from this shader, which used to kill the shader
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

// Rounded ancestor clip for the per-block draw: xy origin and zw size in device pixels (zw = 0: no clip), plus corner
// radii. One draw is one block with one clip; the batched pass reads the same values from the transform table.
float4 DirectClipBox;
float4 DirectClipRadii;

// Instanced glyph batch: per-instance GlyphData, and the transform table indexed by slot (0 = identity), so node-local
// glyphs move with one matrix write instead of a CPU re-bake.
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

// ---- Text clip: a rounded rectangle in device pixels, box.xy = origin, box.zw = size (0 = no clip), radii = TL, TR,
// BR, BL. The text's own code, so the engine does not depend on the UI's shader headers.

float TextClipDistance(float2 p, float2 halfSize, float4 radii)
{
    // SDF y is down, so a negative p.y is the top half.
    float r = p.x < 0.0 ? (p.y < 0.0 ? radii.x : radii.w)
                        : (p.y < 0.0 ? radii.y : radii.z);
    float2 q = abs(p) - halfSize + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, float2(0.0, 0.0))) - r;
}

float ClipCoverage(float2 fragment, float4 box, float4 radii)
{
    if (box.z <= 0.0) return 1.0;
    float2 halfSize = max(box.zw * 0.5, float2(1.0, 1.0));
    float2 local = fragment - (box.xy + halfSize);
    float lim = min(halfSize.x, halfSize.y);
    float d = TextClipDistance(local, halfSize, min(radii, float4(lim, lim, lim, lim)));
    float aa = fwidth(d) + 1e-4;
    return 1.0 - smoothstep(-aa, aa, d);
}

// The batch reads its clip from the transform table: row 0 of the slot's matrix is the box, row 1 the radii, and
// Params.x marks the slot as carrying one. A slot below 0 means no clip.
void ClipFromSlot(float slotIndex, out float4 box, out float4 radii)
{
    box = float4(0.0, 0.0, 0.0, 0.0);
    radii = float4(0.0, 0.0, 0.0, 0.0);
    if (slotIndex < 0.0) return;
    NodeSlot clip = ((NodeSlot*)TransformsAddress)[(uint)slotIndex];
    if (clip.Params.x < 0.5) return;
    box = clip.World[0];
    radii = clip.World[1];
}

// Per-glyph quad expansion, now in the VERTEX stage (corner from SV_VertexID), so the geometry shader is gone:
// plain instanced rendering (4-vertex triangle strip x N glyphs), portable to Metal/MoltenVK.
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
    // Gamma-boost coverage times the color's alpha so thin stems keep their color; splitting the two washed text out.
    // The element's fade arrives pre-raised to 2.2, so the boost hands it back linear.
    float alpha = pow(ForegroundColor.a * opacity, 1.0 / 2.2);
    // The rounded ancestor clip, as coverage, exactly as the batch pass applies it. Both the premultiplied colour and
    // the alpha are cut: this pass outputs rgb*alpha, so cutting one without the other leaves colour where the glyph
    // was cut away. A zero-size box gives 1 and costs nothing.
    alpha *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(ForegroundColor.rgb * alpha, alpha);
}

// Batch variant of FontPixelShaderMsdf: the color comes per instance (input.Color), so one instanced draw renders glyphs
// of many text blocks, each in its own color.
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
    // The fade is raised to 2.2 so the pixel shader's gamma boost hands back exactly `fade`, matching the shapes beside it.
    o.Color = float4(g.Color.rgb, g.Color.a * pow(fade, 2.2));
    // The clip's shape, from the table by the slot the record carries - one fetch per instance, as everywhere else.
    o.Layer = g.Params.y;   // the atlas layer this glyph was packed into
    ClipFromSlot(g.Clip.x, o.ClipBox, o.ClipRadii);
    return o;
}

technique FontBatch
{
    pass RenderMsdf
    {
        EffectName = "FontEffectMsdf";
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdf;
    }

    pass RenderMsdfBatchInstanced
    {
        EffectName = "FontEffectMsdfBatchInstanced";
        VertexShader = FontBatchInstancedVS;
        PixelShader = FontPixelShaderMsdfBatch;
    }
}
