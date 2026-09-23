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

    // LOOPED, and [loop] is load-bearing: twelve sample instructions in one fragment shader take this driver's
    // compiler down (measured - eleven compile, twelve die), and unrolled this is that version.
    const float2 offsets[13] = {
        float2(-1.0,  1.0), float2( 1.0,  1.0), float2(-1.0, -1.0), float2( 1.0, -1.0),
        float2(-0.5,  0.5), float2( 0.5,  0.5), float2(-0.5, -0.5), float2( 0.5, -0.5),
        float2(-1.0,  0.0), float2( 1.0,  0.0), float2( 0.0, -1.0), float2( 0.0,  1.0),
        float2( 0.0,  0.0)
    };
    // The inner quad carries half the result, the corners and the axes a quarter each.
    const float weights[13] = {
        0.03125, 0.03125, 0.03125, 0.03125,
        0.125,   0.125,   0.125,   0.125,
        0.0625,  0.0625,  0.0625,  0.0625,
        0.125
    };

    float4 sum = 0;
    [loop]
    for (int t = 0; t < 13; ++t)
    {
        sum += SourceTexture.SampleLevel(SourceSampler, uv + offsets[t] * texel, level) * weights[t];
    }
    return sum;
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
