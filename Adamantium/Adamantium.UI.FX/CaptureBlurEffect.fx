// THE BACKDROP'S BLUR PYRAMID - one level filled from the one above it.
//
// The levels used to be filled by a halving BLIT, which filters LINEAR: the average of the 2x2 under each destination
// texel. That is a box, and a box does not remove what is above Nyquist before it decimates - so a regular pattern
// behind a pane did not blur, it folded into moire, worse at every level. A checkerboard made it unmissable.
//
// A wide blur is never one wide kernel. It is a pyramid, and what matters is the filter used going DOWN: suppress the
// high frequencies first, decimate second. Thirteen taps in the pattern from Jimenez's SIGGRAPH course (four corner
// quads plus a centre quad, each tap bilinear and therefore two texels wide) do that for the price of thirteen
// fetches per destination texel - and a destination texel is a quarter of the level above it, so the whole pyramid
// costs less than a third of one full-resolution pass.

#include "Includes/CommonData.fxh"

// The level being READ, and the size of the level being written. The source level is explicit because the pyramid is
// walked one step at a time and each step reads exactly one level - never whatever the hardware would pick.
float4 BlurStep;   // .x source level, .yz destination size in texels, .w spare

struct BlurVSOutput
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

// ONE TRIANGLE, not two. A quad is two triangles with a seam down the diagonal, and the rasteriser shades in 2x2
// quads - so along that seam the edge quads are shaded twice. An oversized triangle covers the target with no seam,
// no index buffer and no vertex buffer at all: the three corners come from the vertex id.
[shader("vertex")]
BlurVSOutput CaptureBlurVS(uint id : SV_VertexID)
{
    BlurVSOutput o;
    // NO Y FLIP. Vulkan's clip space already points Y DOWN, which is the direction the copy's rows run in, so negating
    // it here mirrored every level - and since each level is drawn from the one above, the pyramid came out flipped,
    // upright, flipped, and the backdrop jumped to its mirror image and back as the blur widened.
    o.Uv = float2((id << 1) & 2, id & 2);
    o.Position = float4(o.Uv * 2.0 - 1.0, 0.0, 1.0);
    return o;
}

[shader("fragment")]
float4 CaptureBlurPS(BlurVSOutput input) : SV_Target
{
    float level = BlurStep.x;
    float2 texel = 1.0 / max(BlurStep.yz, float2(1.0, 1.0));   // one texel of the DESTINATION = two of the source
    float2 uv = input.Uv;

    // The four corner quads. Each sits a whole destination texel out on a diagonal, so its bilinear fetch straddles
    // four source texels - together they cover the 4x4 neighbourhood that stops the fold.
    float4 a = SourceTexture.SampleLevel(SourceSampler, uv + float2(-texel.x,  texel.y), level);
    float4 b = SourceTexture.SampleLevel(SourceSampler, uv + float2( texel.x,  texel.y), level);
    float4 c = SourceTexture.SampleLevel(SourceSampler, uv + float2(-texel.x, -texel.y), level);
    float4 d = SourceTexture.SampleLevel(SourceSampler, uv + float2( texel.x, -texel.y), level);

    // The centre quad, at half a texel - the part that keeps the result from being four separate averages.
    float2 h = texel * 0.5;
    float4 e = SourceTexture.SampleLevel(SourceSampler, uv + float2(-h.x,  h.y), level);
    float4 f = SourceTexture.SampleLevel(SourceSampler, uv + float2( h.x,  h.y), level);
    float4 g = SourceTexture.SampleLevel(SourceSampler, uv + float2(-h.x, -h.y), level);
    float4 i = SourceTexture.SampleLevel(SourceSampler, uv + float2( h.x, -h.y), level);

    // The axis taps, which is what makes the shape round rather than a cross of four blobs.
    float4 j = SourceTexture.SampleLevel(SourceSampler, uv + float2(-texel.x, 0.0), level);
    float4 k = SourceTexture.SampleLevel(SourceSampler, uv + float2( texel.x, 0.0), level);
    float4 l = SourceTexture.SampleLevel(SourceSampler, uv + float2(0.0, -texel.y), level);
    float4 m = SourceTexture.SampleLevel(SourceSampler, uv + float2(0.0,  texel.y), level);

    float4 centre = SourceTexture.SampleLevel(SourceSampler, uv, level);

    // The weights of the pattern: the inner quad carries half the result, the corners and the axes a quarter each.
    return (e + f + g + i) * 0.125
         + (a + b + c + d) * 0.03125
         + (j + k + l + m) * 0.0625
         + centre * 0.125;
}

technique CaptureBlur
{
    pass Down
    {
        EffectName = "CaptureBlurEffect";
        VertexShader = CaptureBlurVS;
        PixelShader = CaptureBlurPS;
    }
}
