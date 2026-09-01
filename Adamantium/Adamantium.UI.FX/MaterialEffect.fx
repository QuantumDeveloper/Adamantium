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

#include "Includes/CommonData.fxh"
#include "Includes/ClipMath.fxh"
#include "Includes/ShapeMath.fxh"
#include "Includes/StrokeMath.fxh"
#include "Includes/NoiseMath.fxh"
#include "Includes/BrushData.fxh"

// ---- BACKDROP MATERIALS: a fill made from what is ALREADY DRAWN behind the element ---------------------------------
// The capture arrives in SourceTexture (see BackdropCapture): the region behind this element, copied with a downscaling
// blit - so it is already blurred once, for free, and a handful of taps here widen that into a proper frosting instead
// of paying for a full convolution.
//
// THE SOURCE MAPPING, as texture coordinates rather than as a rectangle: .xy scales a frame pixel into the image, .zw
// shifts it. So a fragment's place in the source is one multiply-add - the divide (and the guard against a zero-sized
// rectangle) happens once on the CPU instead of per fragment, and the blur below reuses the same scale for its taps.
//
// One per SEGMENT rather than per instance, which is why it is a parameter and not a field - a draw binds one image, so
// every instance in it maps the same way. Set at DRAW time, which is what keeps it honest across replays: for a capture
// it describes the copied region, and for mica where the desktop put the wallpaper, in this window's pixels. The window
// moving changes the second one without changing anything the frame recorded - so baking it into the instances made the
// wallpaper travel WITH the window instead of staying on the desktop.
float4 SourceUv;

struct MaterialRectData
{
    float4 Bounds;       // NODE-local x, y, w, h
    float4 Params;       // .x corner radius (negative = ellipse/polygon flag), .y transform slot, .z material, .w opacity slot
    float4 Radii;        // corner radii: TL, TR, BR, BL
    float4 Tint;         // straight RGBA laid over the capture; .a is the tint's strength
    float4 Knobs;        // .x blur (device px), .y grain, .z refraction (device px), .w Source pinned to the element
    float4 StrokeColor;  // the pen, in the slots CompositeFillStroke expects
    float4 Stroke0;      // .x width (LOGICAL units - scaled by Scale below), .y alignment
    float4 Stroke1;      // dash offset / trim / flags: this batch bakes only whole solid pens, so they stay at default
    float4 Clip;         // .x = the ROUNDED CLIP's slot, or -1; .yzw spare
};

struct MaterialPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment relative to the shape CENTRE (SDF space, device px)
    float2 Half     : TEXCOORD1;
    float4 Radii    : TEXCOORD2;
    nointerpolation uint InstId : TEXCOORD3;
    nointerpolation float Scale : TEXCOORD4;   // device pixels per logical unit, for the pen's width
    nointerpolation float Fade  : TEXCOORD5;   // the opacity slot's chain, as every other batched fill reads it
    nointerpolation float4 ClipBox   : TEXCOORD6;   // the ancestor's rounded clip, fetched in the VERTEX stage
    nointerpolation float4 ClipRadii : TEXCOORD7;
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

    // The quad has to hold the PEN as well as the fill: a stroke aligned outward leaves the bounds by half its width,
    // and a quad grown by one pixel simply cuts it off - most visibly at the corners, where the stroke stands furthest
    // from the rectangular border. Same expansion the gradient and pattern passes use.
    float widthPx = it.Stroke0.x * iso;
    float outsetPx = max(widthPx * (0.5 * (1.0 + it.Stroke0.y) + 0.5), 0.0) + 1.0;
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw + (corner * 2.0 - 1.0) * (outsetPx / px);
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);
    o.Position = mul(worldPos, Projection);
    o.Half   = it.Bounds.zw * 0.5 * px;
    o.Local  = (corner - 0.5) * it.Bounds.zw * px + (corner * 2.0 - 1.0) * outsetPx;
    o.Radii  = ScaleShapeNumbers(it.Radii, iso, step(it.Params.x, -1.5));
    o.InstId = instanceId;
    o.Scale  = iso;
    int fadeSlot = int(it.Params.w);
    o.Fade = lerp(1.0, nodes[max(fadeSlot, 0)].Params.x, step(0.0, float(fadeSlot)));
    o.ClipBox   = ClipShapeBox(it.Clip.x);
    o.ClipRadii = ClipShapeRadii(it.Clip.x);
    return o;
}

// A fragment's position in the source, 0..1. Position.xy is already the frame's device pixel, which is the space
// SourceUv was built for - so this is one multiply-add, with no matrices and no divide.
float2 CaptureUv(float2 fragment, float4 sourceUv)
{
    return fragment * sourceUv.xy + sourceUv.zw;
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
// Takes the SCALE, not the whole mapping: a picture pinned to the element has no rectangle in the frame at all, and
// only the tap spacing is wanted here.
float4 BlurCapture(float2 uv, float2 uvScale, float radiusPx)
{
    // The tap spacing is the radius in FRAME pixels put through the same scale - the mapping is already stated that way.
    float2 texel = radiusPx * uvScale;
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

    // Knobs.w pins the picture to the ELEMENT: coordinates come from the fragment's place in the SHAPE, not in the
    // frame - which is what makes such a picture travel and TURN with it.
    float pin = it.Knobs.w;
    float2 uvLocal = input.Local / max(2.0 * input.Half, float2(1.0, 1.0)) + 0.5;
    float2 uvScale = lerp(SourceUv.xy, 1.0 / max(2.0 * input.Half, float2(1.0, 1.0)), pin);
    float2 uv = saturate(lerp(CaptureUv(input.Position.xy, SourceUv), uvLocal, pin));
    float4 behind = BlurCapture(uv, uvScale, it.Knobs.x);

    // Tint over the capture, then grain. The grain is what keeps a large pane from banding - the capture came from an
    // 8-bit target and was smoothed twice, so its gradients are flatter than the eye tolerates at this size.
    float3 colour = lerp(behind.rgb, it.Tint.rgb, saturate(it.Tint.a));
    float grain = (Hash21(input.Position.xy) - 0.5) * it.Knobs.y;
    colour = saturate(colour + grain);

    // Fill and pen composited by the shared helper, exactly as the gradient and pattern passes do it - which is also
    // where the self-anti-aliased edge comes from. A pen of zero width degrades to the fill alone.
    // Params.z is the element's own alpha, Fade the slot chain above it. The pen already carries the element's alpha
    // from the bake, so only the chain is applied to the composited result.
    float4 painted = CompositeFillStroke(d, float4(colour, it.Params.z), it.StrokeColor,
                                         it.Stroke0.x * input.Scale, it.Stroke0.y, 1.0, 0.0);
    return float4(painted.rgb, painted.a * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


// ---- LIQUID GLASS: the same capture, BENT ---------------------------------------------------------------------
// Frosting scatters what is behind it; a lens BENDS it, and the bending is what makes a shape read as a solid piece of
// glass rather than as a hazy panel. Everything below follows from one observation: a thick drop of glass is flat in
// the middle and steeply curved at its rim, so light passes straight through the centre and is pushed aside near the
// edge. The signed distance already describes exactly that - it is zero at the rim and grows inward - so the surface's
// slope comes free, without a normal map or any geometry.
//
// Three things arrive together, and none of them reads as glass alone:
//   - REFRACTION: sampling is displaced along the surface's slope, hardest at the rim.
//   - DISPERSION: red and blue are displaced by slightly different amounts, so the rim carries a faint colour fringe,
//     as it does in a real lens.
//   - THE RIM ITSELF: a bright line where the curvature is steepest, which is what tells the eye the shape has depth.

// How the surface leans, at this fragment. The gradient of a signed distance IS the direction away from the nearest
// edge, so the derivatives give the slope of a lens whose shape nobody had to model.
float2 GlassSlope(float d, float2 local)
{
    float2 slope = float2(ddx(d), ddy(d));
    float len = length(slope);
    return len > 1e-5 ? slope / len : float2(0.0, 0.0);
}

// Where the curvature is: flat across the middle, rising steeply within `rim` pixels of the edge. Squared so the centre
// stays honestly flat instead of bulging slightly everywhere.
float GlassCurve(float d, float rim)
{
    float t = saturate(1.0 + d / max(rim, 1.0));   // d is negative inside; 0 at the centre, 1 at the edge
    return t * t;
}

[shader("fragment")]
float4 MaterialGlassPS(MaterialPSInput input) : SV_Target
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[input.InstId];

    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    // The lens: how far to push the sample, and in which direction.
    float strength = it.Knobs.z;
    float rim = max(strength * 2.0, 8.0);
    float2 slope = GlassSlope(d, input.Local);

    // As in the frosted pass, and the bend's scale comes from the shape too.
    float pin = it.Knobs.w;
    float2 uvScale = lerp(SourceUv.xy, 1.0 / max(2.0 * input.Half, float2(1.0, 1.0)), pin);
    float2 push = slope * (GlassCurve(d, rim) * strength) * uvScale;

    float2 uvLocal = input.Local / max(2.0 * input.Half, float2(1.0, 1.0)) + 0.5;
    float2 uv = lerp(CaptureUv(input.Position.xy, SourceUv), uvLocal, pin);

    // Dispersion: the three channels take slightly different paths, which is why the fringe appears only where the
    // bending is strong - along the rim - and not across the flat middle.
    float3 behind;
    behind.r = SourceTexture.Sample(SourceSampler, saturate(uv + push * 1.06)).r;
    behind.g = SourceTexture.Sample(SourceSampler, saturate(uv + push)).g;
    behind.b = SourceTexture.Sample(SourceSampler, saturate(uv + push * 0.94)).b;

    // A LIGHT tint only: glass takes its colour from what is behind it, and a heavy tint turns it back into a panel.
    float3 colour = lerp(behind, it.Tint.rgb, saturate(it.Tint.a) * 0.5);

    // The rim highlight, brightest where the surface turns over. Weighted towards the upper-left because that is where
    // light is assumed to come from throughout this engine's shading.
    float curve = GlassCurve(d, rim);
    float facing = saturate(dot(slope, normalize(float2(-0.7, -0.7))));
    colour += curve * curve * facing * 0.35;

    float grain = (Hash21(input.Position.xy) - 0.5) * it.Knobs.y;
    colour = saturate(colour + grain);

    // Params.z is the element's own alpha, Fade the slot chain above it. The pen already carries the element's alpha
    // from the bake, so only the chain is applied to the composited result.
    float4 painted = CompositeFillStroke(d, float4(colour, it.Params.z), it.StrokeColor,
                                         it.Stroke0.x * input.Scale, it.Stroke0.y, 1.0, 0.0);
    return float4(painted.rgb, painted.a * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


// ---- THE SAME MATERIALS ON ARBITRARY GEOMETRY -----------------------------------------------------------------
// An authored outline arrives as triangles, so these passes do LESS than the analytic ones above: no distance field, no
// radii, no edge to anti-alias - coverage IS the geometry. What remains is the material itself: read the capture, tint,
// grain.
//
// The one thing lost is the lens's SHAPE - the slope came from the distance field. It is taken from the fragment's place
// within the mesh's local bounds instead, so the bend follows the bounding box rather than the true outline.

struct MaterialMeshPSInput
{
    float4 Position : SV_Position;
    float2 Local : TEXCOORD0;                   // fragment's local mesh xy, for the lens falloff
    nointerpolation uint InstId : TEXCOORD1;
    nointerpolation float Fade : TEXCOORD2;
    nointerpolation float4 ClipBox   : TEXCOORD3;   // ...and so is the ancestor's rounded clip
    nointerpolation float4 ClipRadii : TEXCOORD4;
};

[shader("vertex")]
MaterialMeshPSInput MaterialFillVS(UI_VERTEX v, uint instanceId : SV_InstanceID)
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[instanceId];

    // local -> slot space -> world, as PatternFillVS and the other instanced fills do it.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4 world = mul(mul(float4(v.position.xyz, 1.0), it.Local), nodes[(uint)it.Params.w].World);

    MaterialMeshPSInput o;
    o.Position = mul(world, Projection);
    o.Local = v.position.xy;
    o.InstId = instanceId;
    int fadeSlot = int(it.Params.x);
    o.Fade = lerp(1.0, nodes[max(fadeSlot, 0)].Params.x, step(0.0, float(fadeSlot)));
    o.ClipBox   = ClipShapeBox(it.Anim.w);     // this carrier is PatternGeomData - the clip rides in Anim.w
    o.ClipRadii = ClipShapeRadii(it.Anim.w);
    return o;
}

[shader("fragment")]
float4 MaterialFrostedMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    // Params.y pins the picture to the ELEMENT - here, the fragment's place within the mesh's own local bounds.
    float pin = it.Params.y;
    float2 extent = max(it.LocalBounds.zw, float2(1.0, 1.0));
    float2 uvLocal = (input.Local - it.LocalBounds.xy) / extent;
    float2 uvScale = lerp(SourceUv.xy, 1.0 / extent, pin);
    float2 uv = saturate(lerp(CaptureUv(input.Position.xy, SourceUv), uvLocal, pin));
    float4 behind = BlurCapture(uv, uvScale, it.Color3.x);

    float3 colour = lerp(behind.rgb, it.Color1.rgb, saturate(it.Color1.a));
    float grain = (Hash21(input.Position.xy) - 0.5) * it.Color3.y;
    colour = saturate(colour + grain);

    return float4(colour, input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}

[shader("fragment")]
float4 MaterialGlassMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    float strength = it.Color3.z;

    // Where the lens leans and how hard - from the fragment's place in the mesh's local bounds, since there is no
    // distance field here. Flat in the middle, steep towards the outside, following the bounding box.
    float2 halfSize = max(it.LocalBounds.zw * 0.5, float2(1.0, 1.0));
    float2 outward = (input.Local - (it.LocalBounds.xy + halfSize)) / halfSize;
    float edge = saturate(max(abs(outward.x), abs(outward.y)));
    float curve = edge * edge;
    float len = length(outward);
    float2 slope = len > 1e-5 ? outward / len : float2(0.0, 0.0);

    // As in the frosted mesh pass, and the bend's scale comes from the shape too.
    float pin = it.Params.y;
    float2 extent = max(it.LocalBounds.zw, float2(1.0, 1.0));
    float2 uvScale = lerp(SourceUv.xy, 1.0 / extent, pin);
    float2 push = slope * (curve * strength) * uvScale;

    float2 uvLocal = (input.Local - it.LocalBounds.xy) / extent;
    float2 uv = lerp(CaptureUv(input.Position.xy, SourceUv), uvLocal, pin);

    float3 behind;
    behind.r = SourceTexture.Sample(SourceSampler, saturate(uv + push * 1.06)).r;
    behind.g = SourceTexture.Sample(SourceSampler, saturate(uv + push)).g;
    behind.b = SourceTexture.Sample(SourceSampler, saturate(uv + push * 0.94)).b;

    float3 colour = lerp(behind, it.Color1.rgb, saturate(it.Color1.a) * 0.5);

    float facing = saturate(dot(slope, normalize(float2(-0.7, -0.7))));
    colour += curve * curve * facing * 0.35;

    float grain = (Hash21(input.Position.xy) - 0.5) * it.Color3.y;
    colour = saturate(colour + grain);

    return float4(colour, input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


// =====================================================================================================================
// TECHNIQUE - one pass per TREATMENT and CARRIER. Frosted serves both Acrylic and Mica: they differ in what is CAPTURED,
// not in what is done with it. Glass bends the same capture instead of scattering it. Sdf and Mesh are the two carriers:
// a shape described by a formula, and one that arrives as triangles.
// =====================================================================================================================
technique Material
{
    pass FrostedSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialFrostedPS;
    }

    pass GlassSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialGlassPS;
    }

    pass FrostedMesh
    {
        Profile = 6.6;
        VertexShader = MaterialFillVS;
        PixelShader = MaterialFrostedMeshPS;
    }

    pass GlassMesh
    {
        Profile = 6.6;
        VertexShader = MaterialFillVS;
        PixelShader = MaterialGlassMeshPS;
    }

}
