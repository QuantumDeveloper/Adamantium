// THE BACKDROP MATERIALS - acrylic, mica, liquid glass: fills made from what is ALREADY DRAWN behind the element.
//
// A THIRD effect, and the reason is the same one that split the brushes off the shapes, only sharper. Adding these
// shaders to BrushEffect made vkCreateShadersEXT die with an access violation - and not on the new pass, on the
// GRADIENT one, which had worked for months. The driver's shader-object compiler has a ceiling on what one effect can
// carry, this file's own notes have been recording that ceiling for a while, and the brushes had reached it.
//
// So materials get their own parameter block and their own set of shader objects. It also happens to be the honest
// split: their source is not a brush's business at all. A gradient computes its colour, a texture samples an asset,
// and a material reads the FRAME - produced mid-draw, one region per segment (see BackdropCapture).

#include "Effects/CommonData.fxh"
#include "Effects/ShapeMath.fxh"
#include "Effects/StrokeMath.fxh"
#include "Effects/NoiseMath.fxh"
#include "Effects/BrushData.fxh"

// ---- BACKDROP MATERIALS: a fill made from what is ALREADY DRAWN behind the element ---------------------------------
// The capture arrives in SourceTexture (see BackdropCapture): the region behind this element, copied with a downscaling
// blit - so it is already blurred once, for free, and a handful of taps here widen that into a proper frosting instead
// of paying for a full convolution.
//
// CaptureRect maps a fragment back into that copy. It is in DEVICE pixels of the frame, not of the element, because the
// capture is grown by a margin: a blur reaches outside what it covers, and sampling right up to the element's edge
// darkens the border towards whatever the clamp returns.
struct MaterialRectData
{
    float4 Bounds;       // NODE-local x, y, w, h
    float4 Params;       // .x corner radius (negative = ellipse/polygon flag), .y transform slot, .z material, .w opacity slot
    float4 Radii;        // corner radii: TL, TR, BR, BL
    float4 Tint;         // straight RGBA laid over the capture; .a is the tint's strength
    float4 Knobs;        // .x blur (device px), .y grain, .z refraction (device px), .w reserved
    float4 CaptureRect;  // where the capture came from, in DEVICE pixels: x, y, w, h
};

struct MaterialPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the shape CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;
    float4 Radii    : TEXCOORD2;
    nointerpolation uint InstId : TEXCOORD3;
};

[shader("vertex")]
MaterialPSInput MaterialRectInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[instanceId];

    MaterialPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (1.0 / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0);
    o.Radii  = ScaleShapeNumbers(it.Radii, iso, step(it.Params.x, -1.5));
    o.InstId = instanceId;
    return o;
}

// A fragment's position in the CAPTURE, 0..1. Position.xy is already the frame's device pixel, which is exactly the
// space CaptureRect is stated in - so this is a subtraction and a divide, with no matrices involved.
float2 CaptureUv(float2 fragment, float4 captureRect)
{
    return (fragment - captureRect.xy) / max(captureRect.zw, float2(1.0, 1.0));
}

// Widening blur: a small ring of taps around the fragment. The capture is already downscaled, so each tap here reaches
// four times as far as its pixel count suggests - eight taps plus the centre buy a radius that would cost dozens at
// full resolution. Ring rather than a box: the same taps spread over a circle read smoother at equal cost.
// FIVE taps, not nine, and NOT a variable called `step`: that name belongs to a standard-library function in Slang (as
// it does in HLSL), and shadowing it inside a file whose other shaders call step() is asking the compiler to guess.
// Named texel here.
//
// Deliberately small. The capture is already downscaled fourfold, so each tap reaches four times its pixel count, and
// this driver has a documented ceiling on what one pixel shader can carry before vkCreateShadersEXT or the GPU itself
// gives out - the pattern shader hit it, and it is the reason materials are a separate effect at all. Widen only with a
// measurement in hand.
float4 BlurCapture(float2 uv, float4 captureRect, float radiusPx)
{
    float2 texel = radiusPx / max(captureRect.zw, float2(1.0, 1.0));
    float4 sum = SourceTexture.Sample(SourceSampler, uv);
    sum += SourceTexture.Sample(SourceSampler, uv + float2( texel.x,  0.0));
    sum += SourceTexture.Sample(SourceSampler, uv + float2(-texel.x,  0.0));
    sum += SourceTexture.Sample(SourceSampler, uv + float2( 0.0,  texel.y));
    sum += SourceTexture.Sample(SourceSampler, uv + float2( 0.0, -texel.y));
    return sum / 5.0;
}

[shader("fragment")]
float4 MaterialFrostedPS(MaterialPSInput input) : SV_Target
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[input.InstId];

    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    float2 uv = saturate(CaptureUv(input.Position.xy, it.CaptureRect));
    float4 behind = BlurCapture(uv, it.CaptureRect, it.Knobs.x);

    // Tint over the capture, then grain. The grain is what keeps a large pane from banding - the capture came from an
    // 8-bit target and was smoothed twice, so its gradients are flatter than the eye tolerates at this size.
    float3 colour = lerp(behind.rgb, it.Tint.rgb, saturate(it.Tint.a));
    float grain = (Hash21(input.Position.xy) - 0.5) * it.Knobs.y;
    colour = saturate(colour + grain);

    // Self-anti-aliased edge, the same one every other SDF fill here uses.
    float aa = fwidth(d) + 1e-4;
    float coverage = 1.0 - smoothstep(-aa, aa, d);
    return float4(colour, coverage);
}


// =====================================================================================================================
// TECHNIQUE - one per MATERIAL FAMILY, one pass per carrier. Frosted serves both Acrylic and Mica: they differ in what
// is CAPTURED and handed to it, not in what it does with the capture. Glass (the refracting lens) will be a second pass
// over the same record.
// =====================================================================================================================
technique Material
{
    pass FrostedSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialFrostedPS;
    }
}
