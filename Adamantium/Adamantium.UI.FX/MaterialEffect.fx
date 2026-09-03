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
    float4 Surface;      // SURFACES: .rgb what it is made of (cloth colour / metal F0), .a grain scale in device px
    float4 Response;     // SURFACES: .rgb what it answers light with (sheen colour / environment), .a roughness
    float4 Light;        // SURFACES: .x grain direction (rad), .y light angle (rad), .z elevation, .w the WOOD's figure
                         // code - cut, plus 4 when varnished. Spare here (the anisotropy that sat in it is read by
                         // nobody); on the MESH carrier the same component is the clip slot, hence the pinned figure
};

// ---------------------------------------------------------------------------------------------------------------
// THE SURFACES. Three rules for the whole branch: the normal comes from a NOISE FIELD (a flat rectangle has one normal,
// so any lighting model over it collapses into a flat fill), the light is a BRUSH property (the view is fixed and
// orthographic, so a scene light would be a pretence), and nothing is CAPTURED - these say what a thing is made of,
// not what shows through it.
//
/// Value noise with its DERIVATIVE from one evaluation: the four corner hashes that give the height also give the
/// slope. TAPS ARE A BUDGET, not a preference - the first version sampled simplex four times per fragment and
/// vkCreateShadersEXT began failing launch after launch.
///
/// Returns (height, d/dx, d/dy).
float3 NoiseD(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;

    float a = Hash21(i);
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f);
    float2 du = 6.0 * f * (1.0 - f);

    float k1 = b - a;
    float k2 = c - a;
    float k3 = a - b - c + d;

    return float3(a + k1 * u.x + k2 * u.y + k3 * u.x * u.y,
                  du.x * (k1 + k3 * u.y),
                  du.y * (k2 + k3 * u.x));
}

// TWO RELIEFS, ON PURPOSE. Velvet and metal may share the RECORD; they must not share how they look. One field for both
// was tried and velvet came out looking like brushed metal.

/// VELVET's nap: an irregular field stretched 4:1 so the fibres lie combed. Simplex, because cloth wants a field with
/// no period in it at all - a cheaper wave sum reads as corduroy at once.
float NapHeight(float2 p, float scale, float dir)
{
    float2 axis = float2(cos(dir), sin(dir));
    float2 along = float2(dot(p, axis), dot(p, float2(-axis.y, axis.x)) * 4.0);
    return SimplexNoise(along / max(scale, 0.5));
}

float3 NapNormal(float2 p, float scale, float dir)
{
    float e = max(scale * 0.35, 0.5);
    float dx = NapHeight(p + float2(e, 0), scale, dir) - NapHeight(p - float2(e, 0), scale, dir);
    float dy = NapHeight(p + float2(0, e), scale, dir) - NapHeight(p - float2(0, e), scale, dir);

    return normalize(float3(-dx * 2.2, -dy * 2.2, 1.0));
}

/// METAL's grinding: scratches, not fibres - far more stretched and far shallower. DAMPED where they fall below a
/// pixel, or fine grinding on a scrolling panel boils.
float3 MetalNormal(float2 p, float scale, float dir)
{
    float2 axis = float2(cos(dir), sin(dir));
    float2 across = float2(-axis.y, axis.x);

    // The RELIEF is what is directional in this material; the reflection lobe is not - see MetalSurface.
    const float stretch = 16.0;

    float s = max(scale, 0.5);
    float2 q = float2(dot(p, axis) / (s * stretch), dot(p, across) / s);

    float3 n = NoiseD(q);
    float2 g = axis * (n.y / (s * stretch)) + across * (n.z / s);

    // SHALLOW: a scratch tilts the surface by a few degrees. Deepening it was tried and turns the plate into landscape.
    float footprint = length(fwidth(q));
    float depth = 0.06 * saturate(1.0 / (1.0 + footprint * 3.0));

    return normalize(float3(-g * depth * s, 1.0));
}

/// The branch's light: an azimuth plus an elevation. Grazing lights a nap and shows a metal's grinding; overhead
/// flattens both.
///
/// <para>NEITHER END MAY BE A POLE. Straight overhead a light has no direction along the surface, so the azimuth knob
/// would silently stop mattering; exactly grazing lights nothing. The range stops short of both, and elevation is an
/// ANGLE - mixed linearly it crowds the useful part into the middle of the knob.</para>
float3 BranchLight(float4 light)
{
    float theta = lerp(0.08, 1.35, saturate(light.z));   // ~5 to ~77 degrees
    float2 dir = float2(cos(light.y), sin(light.y));
    return float3(dir * cos(theta), sin(theta));
}

/// The Charlie sheen distribution (KHR_materials_sheen). What makes velvet velvet is in the exponent: brightness rises
/// towards GRAZING angles, so a fold's rim lights up while its face stays dark - the opposite of an ordinary lobe.
float SheenD(float ndoth, float roughness)
{
    float a = max(roughness, 0.07);
    float invR = 1.0 / a;
    float sin2 = max(1.0 - ndoth * ndoth, 0.0001);
    return (2.0 + invR) * pow(sin2, invR * 0.5) / 6.2831853;
}

// Ashikhmin's visibility - the cheap one the extension names, and enough for one light and no shadowing geometry.
float SheenV(float ndotl, float ndotv)
{
    return 1.0 / max(4.0 * (ndotl + ndotv - ndotl * ndotv), 0.0001);
}

/// The whole surface, in the shape's own device-pixel space. Shared by both carriers so a velvet rectangle and a velvet
/// path cannot drift apart.
float4 SheenSurface(float2 p, float4 surface, float4 response, float4 light)
{
    float3 n = NapNormal(p, surface.a, light.x);

    float3 l = BranchLight(light);
    float3 v = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + v);

    float ndotl = saturate(dot(n, l));
    float ndotv = saturate(dot(n, v));
    float ndoth = saturate(dot(n, h));

    // A soft wrap rather than a hard Lambert: a nap scatters light round its own fibres.
    float wrap = saturate((dot(n, l) + 0.6) / 1.6);
    float3 body = surface.rgb * (0.25 + 0.75 * wrap);

    float3 gleam = response.rgb * (SheenD(ndoth, response.a) * SheenV(ndotl, ndotv) * ndotl);

    return float4(body + gleam, 1.0);
}

// METAL: the same lit surface with the other half of the answer - a GGX lobe and something to REFLECT. What it reflects
// is PROCEDURAL: behind a user interface there is no world, so a capture gives a mirror of the window, not of a room.

/// The studio: floor below, sky above, a bright band where they meet, taken by one "how far up is this ray looking"
/// number from -1 to 1.
///
/// <para>WHICH number is the whole difference between metal and paint. The reflected ray's own height is wrong here and
/// was tried: under a fixed orthographic view a flat plate reflects the same direction everywhere, so the environment
/// comes back one flat colour. A real plate sweeps the room ACROSS itself, so the caller composes the height from where
/// the fragment sits, and the relief only shakes it.</para>
float3 StudioEnvironment(float h, float3 sky)
{
    // A room, not a two-tone card. The floor is dim but never black - a near-black floor turned the grinding into hard
    // black bars - and too narrow a range puts the plate back to painted plastic.
    float3 ground = sky * 0.30;
    float3 horizon = sky * 1.30;
    float t = saturate(h * 0.5 + 0.5);
    return lerp(lerp(ground, horizon, saturate(t * 2.0)), sky, saturate(t * 2.0 - 1.0));
}

// ---- A METAL'S BRDF ------------------------------------------------------------------------------------------
// Cook-Torrance with anisotropic GGX, in Filament's forms. A metal has NO diffuse lobe: everything it returns is a
// specular reflection and its colour IS its reflectance at normal incidence. D, G and F over one light plus the room,
// with no tuning constants.

/// Anisotropic GGX distribution: two roughnesses, along the grinding and across it.
float MetalDistribution(float at, float ab, float ToH, float BoH, float NoH)
{
    float a2 = at * ab;
    float3 d = float3(ab * ToH, at * BoH, a2 * NoH);
    float d2 = max(dot(d, d), 1e-8);
    float b2 = a2 / d2;
    return a2 * b2 * b2 * (1.0 / 3.14159265);
}

/// Height-correlated Smith visibility, with the 1/(4 NoL NoV) denominator already folded in - which is why the
/// specular below multiplies rather than divides.
float MetalVisibility(float at, float ab, float ToV, float BoV, float NoV, float ToL, float BoL, float NoL)
{
    float lambdaV = NoL * length(float3(at * ToV, ab * BoV, NoV));
    float lambdaL = NoV * length(float3(at * ToL, ab * BoL, NoL));
    return 0.5 / max(lambdaV + lambdaL, 1e-5);
}

/// Schlick's Fresnel over a metal's F0 - for a conductor that is its colour, which is why gold reflects gold.
float3 MetalFresnel(float3 f0, float u)
{
    float f = pow(1.0 - u, 5.0);
    return f0 + (1.0 - f0) * f;
}

/// Back into the display's range WITHOUT losing the colour. Per-channel clipping is colour-blind: gold reflects nearly
/// all of its red and under half its blue, so as it brightens the red pins at white while the blue still climbs, and
/// every metal converges on the same pale plate. Scaling all three by the SAME factor keeps the ratio, which is the
/// whole of what makes gold gold. Below the knee nothing is touched.
float3 ToneRollOff(float3 c)
{
    const float knee = 0.75;

    float peak = max(c.r, max(c.g, c.b));
    if (peak <= knee) return c;

    float mapped = knee + (1.0 - knee) * (1.0 - exp(-(peak - knee) / (1.0 - knee)));
    return c * (mapped / peak);
}

/// <param name="bevelTilt">How hard the EDGE turns the surface over; zero in the middle. It is the one place on a flat
/// plate where the normal genuinely changes, so the face and the rim reflect different parts of the room and a bright
/// edge appears - which is where a metal announces itself.</param>
float4 MetalSurface(float2 p, float2 halfExtent, float2 bevelTilt, float4 surface, float4 response, float4 light)
{
    float3 n = MetalNormal(p, surface.a, light.x);

    // Folded into the same normal everything else reads, so the highlight, the room and the scratches agree about the
    // rim instead of it being painted on afterwards.
    n = normalize(float3(n.xy + bevelTilt, n.z));

    float3 l = BranchLight(light);
    float3 v = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + v);

    // The grinding's own frame: tangent along it, bitangent across.
    float3 t = normalize(float3(cos(light.x), sin(light.x), 0.0));
    float3 b = normalize(cross(n, t));
    t = normalize(cross(b, n));

    // ONE roughness. An anisotropic lobe was here and is gone: under a fixed orthographic view with a single light the
    // long axis has no sweep of angles to show itself over, so it changed the highlight on paper and not on screen.
    // What stays directional here is the RELIEF, which is visible.
    float perceptual = clamp(response.a, 0.045, 1.0);
    float a = perceptual * perceptual;

    // ...widened by the normal's variance WITHIN the pixel: a pixel covering a spread of normals must show the average
    // of what they reflect, and a wider lobe IS that average. Without it the highlight crawls in fireflies.
    float3 dnx = ddx(n);
    float3 dny = ddy(n);
    a = saturate(a + (dot(dnx, dnx) + dot(dny, dny)) * 0.5);

    float at = max(a, 0.002);
    float ab = at;

    float NoV = abs(dot(n, v)) + 1e-5;
    float NoL = saturate(dot(n, l));
    float NoH = saturate(dot(n, h));
    float VoH = saturate(dot(v, h));

    // THE LIGHT: D x V x F, the visibility term already carrying 1/(4 NoL NoV).
    float3 f0 = surface.rgb;
    float3 spec = MetalDistribution(at, ab, dot(t, h), dot(b, h), NoH)
                * MetalVisibility(at, ab, dot(t, v), dot(b, v), NoV, dot(t, l), dot(b, l), NoL)
                * MetalFresnel(f0, VoH) * NoL;

    // THE ROOM, and it is a NEAR one. A flat plate under a fixed orthographic view reflects the same DIRECTION
    // everywhere, so an environment at infinity gives one flat colour. A room is not at infinity: each ray travels a
    // finite distance before it lands, so the top of the plate looks at the ceiling and the bottom at the floor. That
    // sweep is the difference between metal and paint.
    //
    // NOT curvature: a curved bar would change the NORMAL and bend the highlight and the scratches with it. Here the
    // normal stays flat and only the point of the room being looked at moves.
    float2 uv = p / max(halfExtent, float2(1.0, 1.0));   // -1..1 across the plate
    float3 r = reflect(-v, n);

    const float roomDistance = 1.7;   // in plate half-heights: how far the walls stand off
    float upness = clamp((uv.y + r.y * roomDistance) / (1.0 + roomDistance) * 2.0, -1.0, 1.0);

    // Prefiltered by roughness the cheap split-sum way: a rough surface averages the room over a wide lobe, and this
    // environment's average is its own mid-tone. Polished keeps the sweep sharp, satin washes it flat.
    float3 sharp = StudioEnvironment(upness, response.rgb);
    float3 blurred = StudioEnvironment(0.0, response.rgb);
    float3 env = lerp(sharp, blurred, saturate(perceptual * 1.6)) * MetalFresnel(f0, NoV);

    // Roll the highlight off rather than clip it: a GGX peak at low roughness is worth hundreds and a display holds one,
    // so without this the light has a knife-edge range - blown white for a few degrees, no highlight outside them.
    // Only the SPECULAR: the environment is already in range and compressing it drags the whole plate grey.
    spec = spec / (1.0 + max(spec.r, max(spec.g, spec.b)));

    return float4(ToneRollOff(env + spec), 1.0);
}

// ---- WOOD ------------------------------------------------------------------------------------------------------
// The odd one of the branch. Velvet and metal are lighting models over a plain colour; wood is a PATTERN that happens
// to be lit, and getting the pattern right matters far more here than getting the lobe right.
//
// A tree lays down one ring a year - broad pale spring growth closed by a narrow dark summer band - and those rings are
// concentric cylinders about the trunk. A board is a SLICE through that stack, so its face shows where the cut plane
// crossed them. That is why timber shows arches and not stripes, and why this is built from a distance to an AXIS: a
// stripe function is what a plane parallel to the rings would give, and nobody saws boards that way.
//
// FOUR taps, and that is a budget: this file has lost launches to vkCreateShadersEXT over tap count before (see NoiseD).
/// <param name="bevel">The chamfer as TWO things. <c>.xy</c> is the tilt, constant across the facet because a planed
/// facet is flat. <c>.z</c> is how far ACROSS it the fragment lies, 0 at the break and 1 at the outer edge - the figure
/// needs it, because cutting removes material progressively. Displace by the tilt alone and the band shifts rigidly:
/// the rings JUMP at the break and then run parallel to the face's.</param>
float4 WoodSurface(float2 p, float2 halfExtent, float3 bevel, float4 surface, float4 response, float4 light)
{
    float2 bevelTilt = bevel.xy;

    // The figure code, unpacked: the cut, and 4 added on top when the board carries a finish.
    float varnished = step(3.5, light.w);
    float cut = light.w - varnished * 4.0;

    float s = max(surface.a, 0.5);
    float dir = light.x;

    float2 axis = float2(cos(dir), sin(dir));     // along the grain: the trunk's length
    float2 across = float2(-axis.y, axis.x);

    // THE COAT, taken FIRST: a finish is a film ON TOP of the wood, so everything it does happens before you see grain.
    //
    // ORANGE PEEL. A clear coat levels under its own weight but never completely, and that residual swell is why a
    // varnished board shows a scatter of GLINTS that swim as the light turns rather than one flat sheet - each swell
    // catches the source at its own angle. A perfectly flat coat reflects the same everywhere and reads as no coat.
    // Its amount follows ROUGHNESS, that way round: a mirror lacquer has been cut back and buffed, a hand-rubbed satin
    // one has not. FINE and SHALLOW - coarse and deep it stops being a highlight and becomes pale blotches.
    float peel = varnished * (0.25 + 0.75 * saturate(response.a));
    float3 swell = NoiseD(p * 0.045);
    float2 coatSlope = swell.yz * 0.045 * peel * 9.0;

    // AND REFRACTION: the coat is a slab of glass with the wood at its bottom, so the figure is seen DISPLACED where
    // the film is sloped. Small - a film a fraction of a millimetre thick, not a lens.
    //
    // The chamfer displaces it too, for another reason: cutting removes material, so the facet shows wood from steadily
    // deeper. GROWING FROM ZERO at the break, squared so it starts flat there, which keeps the rings CONTINUOUS across
    // the line while bending their course. A constant displacement shifts the band rigidly and the rings jump.
    float2 seen = p - coatSlope * 5.5 + bevelTilt * (bevel.z * bevel.z * 34.0);

    float u = dot(seen, axis) / s;
    float v = dot(seen, across) / s;

    // Both taps taken BEFORE the cut is chosen, so the budget is the same whichever way the plank was sawn.
    float3 wander = NoiseD(float2(u * 0.11, 0.0));
    float3 rough = NoiseD(float2(u * 0.45, v * 0.45));

    // THE RING'S FOOTPRINT, measured here: before any branch, and from the COORDINATES rather than from the distance
    // built out of them. Both halves of that matter. Screen derivatives taken inside divergent control flow are
    // undefined, and on this driver produce a shader that will not create at all; computing every cut instead, to keep
    // the flow straight, makes it heavy enough that the driver dies creating it anyway. This function has caused both.
    // It is an ANTI-ALIASING width, so a footprint within a factor of the true one is enough.
    float w = max(length(fwidth(float2(u, v))), 0.0015);

    // WHERE THE CORE IS, WHICH IS THE WHOLE OF THE CUT: all four figures are the same distance-to-the-axis seen from a
    // different plane, so they share one formula and differ only in where the axis is put.
    float r;

    if (cut < 0.5)
    {
        // PLAIN SAWN: the plane runs BESIDE the core. A trunk leans and bends, so its axis wanders relative to the
        // face, and the arches are where that wandering axis comes closest and the rings close into a nest. A straight
        // axis gives dead parallel bands.
        float centre = wander.x * 7.0 - 3.5;
        float depth = 1.2 + wander.x * 3.0;
        r = length(float2(v - centre, depth)) + rough.x * 0.75;
    }
    else if (cut < 1.5)
    {
        // QUARTER SAWN: the plane passes THROUGH the core and meets every ring square on, so they land as narrow evenly
        // spaced lines along the board. No arch is possible, which is the point of the cut.
        r = v + wander.x * 0.9 + rough.x * 0.2;
    }
    else if (cut < 2.5)
    {
        // END GRAIN: the plane is ACROSS the trunk, so the cylinders show as what they are - rings about the core.
        r = length(float2(u, v)) + rough.x * 0.5;
    }
    else
    {
        // BURL: a knot of dormant buds with no grain direction left, so the distance is dragged about before the rings
        // are taken from it.
        r = length(float2(u, v) + float2(wander.x - 0.5, rough.x - 0.5) * 9.0) + rough.x * 2.5;
    }

    // EACH YEAR, and the sharp edge is real: the step from summer's dense wood back to spring growth is abrupt in the
    // timber, which is why a ring reads as a LINE. Widened by the footprint so a plate seen small washes to the average
    // instead of shimmering.
    float ring = frac(r);
    float late = smoothstep(0.72 - w, 0.86, ring) * (1.0 - smoothstep(1.0 - w, 1.0, ring));

    // Past a whole ring per pixel there is nothing left to resolve: settle on the proportion of late wood a ring has,
    // or the rings alias into moire.
    late = lerp(late, 0.28, saturate(w * 2.0 - 0.35));

    // THE FIBRE: cells run ALONG the trunk, so the streaks are long that way and fine across it - the same field with
    // the axes swapped in scale, which is all "grain" means here.
    float3 fibre = NoiseD(float2(u * 0.30, v * 8.0));

    float3 colour = lerp(surface.rgb, response.rgb, saturate(late));
    colour *= 0.88 + fibre.x * 0.24;

    // The relief is the FIBRE, not the rings: on a planed board the rings are colour, while the open pores of spring
    // growth are what a fingernail catches. Shallower still where the fibre is finer than the pixel.
    float2 g = axis * (fibre.y * 0.30) + across * (fibre.z * 8.0);
    float damp = saturate(1.0 / (1.0 + length(fwidth(float2(u * 0.30, v * 8.0))) * 3.0));
    float3 n = normalize(float3(-g * 0.035 * damp, 1.0));

    n = normalize(float3(n.xy + bevelTilt, n.z));

    // TWO SURFACES, not to be confused. The wood's relief is what the light SINKS INTO and shades the body; the coat is
    // a separate film over it, and that is what REFLECTS - its swell plus the chamfer, which the film follows over.
    float3 coatN = normalize(float3(-coatSlope + bevelTilt, 1.0));

    float3 l = BranchLight(light);
    float3 vdir = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + vdir);

    float wrap = saturate((dot(n, l) + 0.35) / 1.35);
    float3 body = colour * (0.55 + 0.45 * wrap);

    // Outside the finish branch below, for the same reason the ring's footprint is taken before the cut is chosen.
    float nVariance = length(fwidth(coatN));

    // RAW TIMBER STOPS HERE: bare wood has no film to reflect anything however smoothly it is planed, so the finish is
    // a separate answer from the roughness rather than a value of it. A branch, because the lobe and the room below are
    // worth skipping outright.
    if (varnished < 0.5) return float4(ToneRollOff(body), 1.0);

    // The coat's own frame, ORTHONORMAL, which the lobe requires: the GGX denominator is built on t, b and n being a
    // basis. Given flat 2D projections instead it does not evaluate to a dimmer highlight but to nearly none at all.
    float3 t = normalize(float3(cos(dir), sin(dir), 0.0));
    float3 bt = normalize(cross(coatN, t));
    t = normalize(cross(bt, coatN));

    float NoV = abs(dot(coatN, vdir)) + 1e-5;
    float NoL = saturate(dot(coatN, l));
    float NoH = saturate(dot(coatN, h));
    float VoH = saturate(dot(vdir, h));

    float perceptual = clamp(response.a, 0.06, 1.0);
    float a = saturate(perceptual * perceptual + nVariance * 0.5);
    float at = max(a, 0.002);

    // A DIELECTRIC keeps BOTH lobes, unlike metal: the colour above is what the body scatters back, and the coat sits
    // on top at F0 0.04 - the same for every finish, because gloss and satin differ in roughness, not reflectance.
    const float3 coatF0 = float3(0.04, 0.04, 0.04);

    float3 varnish = MetalDistribution(at, at, dot(t, h), dot(bt, h), NoH)
                   * MetalVisibility(at, at, dot(t, vdir), dot(bt, vdir), NoV, dot(t, l), dot(bt, l), NoL)
                   * MetalFresnel(coatF0, VoH) * NoL;

    varnish = varnish / (1.0 + max(varnish.r, max(varnish.g, varnish.b)));

    // AND THE ROOM IN IT. One light over a nearly flat board gives the SAME highlight everywhere - an even wash, not a
    // highlight, which is why the varnish could not be seen. What reads as polished is seeing the room, and its
    // reflection varies across the board because the room is at a finite distance: the near-field argument metal needed.
    float2 uv = p / max(halfExtent, float2(1.0, 1.0));
    float3 rdir = reflect(-vdir, coatN);

    const float roomDistance = 1.7;
    float upness = clamp((uv.y + rdir.y * roomDistance) / (1.0 + roomDistance) * 2.0, -1.0, 1.0);

    float3 sharp = StudioEnvironment(upness, float3(0.86, 0.87, 0.9));
    float3 blurred = StudioEnvironment(0.0, float3(0.86, 0.87, 0.9));
    float3 room = lerp(sharp, blurred, saturate(perceptual * 1.6));

    // Schlick over the coat: four per cent head-on, nearly everything edge-on. That is why a polished table looks like
    // wood from above and like a mirror from across the room, and it is what lights the chamfer up.
    float3 fres = MetalFresnel(coatF0, NoV);

    return float4(ToneRollOff(lerp(body, room, fres) + varnish), 1.0);
}

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


[shader("fragment")]
float4 MaterialSheenPS(MaterialPSInput input) : SV_Target
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[input.InstId];

    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    // The grain is read in the SHAPE's own space, not the frame's: cloth belongs to the thing it covers, so a velvet
    // pane scrolled across the window keeps its weave instead of swimming through a fixed field.
    float4 surface = SheenSurface(input.Local, it.Surface, it.Response, it.Light);

    // No film grain - see MaterialMetalPS.
    float4 painted = CompositeFillStroke(d, float4(saturate(surface.rgb), it.Params.z), it.StrokeColor,
                                         it.Stroke0.x * input.Scale, it.Stroke0.y, 1.0, 0.0);
    return float4(painted.rgb, painted.a * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


[shader("fragment")]
float4 MaterialMetalPS(MaterialPSInput input) : SV_Target
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[input.InstId];

    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    // THE CHAMFER, from the shape's own distance field: which way is out, and how far into the bevel this fragment is.
    // Squared, so the face stays flat and the turn happens in the last few pixels rather than as a dome.
    const float bevelWidth = 9.0;   // device px of milled edge
    float2 outward = GlassSlope(d, input.Local);
    float rise = GlassCurve(d, bevelWidth);
    float4 surface = MetalSurface(input.Local, input.Half, outward * rise * 0.9, it.Surface, it.Response, it.Light);

    // No film grain: it hides the banding an 8-bit CAPTURE brings, and a surface captures nothing.
    float4 painted = CompositeFillStroke(d, float4(saturate(surface.rgb), it.Params.z), it.StrokeColor,
                                         it.Stroke0.x * input.Scale, it.Stroke0.y, 1.0, 0.0);
    return float4(painted.rgb, painted.a * input.Fade * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


[shader("fragment")]
float4 MaterialWoodPS(MaterialPSInput input) : SV_Target
{
    MaterialRectData* items = (MaterialRectData*)InstancesAddress;
    MaterialRectData it = items[input.InstId];

    float isPolygon = step(it.Params.x, -1.5);
    float isEllipse = step(it.Params.x, -0.0001) * (1.0 - isPolygon);
    float lim = min(input.Half.x, input.Half.y);
    float4 r4 = lerp(min(input.Radii, float4(lim, lim, lim, lim)), input.Radii, isPolygon);
    float d = BrushShapeDistance(input.Local, input.Half, r4, 2, isEllipse + isPolygon * 2.0);

    // A CHAMFER, not the rounded easing the metal takes. Metal is milled and its edge broken over; a wooden edge is
    // CUT, one pass of a plane, leaving a flat facet with a crisp line where it meets the face - and that LINE is what
    // reads as volume. A gradient smeared over the last few pixels has none, which is why the edge looked like nothing.
    // So the tilt is constant across the facet, with the break softened over a pixel and a half: enough not to
    // stair-step, not enough to lose the line.
    const float bevelWidth = 13.0;   // device px of facet
    float2 outward = GlassSlope(d, input.Local);
    float facet = smoothstep(-bevelWidth, -bevelWidth + 1.5, d);
    float across = saturate((d + bevelWidth) / bevelWidth);   // 0 at the break, 1 at the outer edge
    float4 surface = WoodSurface(input.Local, input.Half, float3(outward * facet, across * facet),
                                 it.Surface, it.Response, it.Light);

    // No film grain - see MaterialMetalPS.
    float4 painted = CompositeFillStroke(d, float4(saturate(surface.rgb), it.Params.z), it.StrokeColor,
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


[shader("fragment")]
float4 MaterialSheenMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    // The same surface the SDF pass draws, read in the mesh's own local space so a velvet path and a velvet rectangle
    // wear the same cloth. Color2 / Noise / Anim.xyz carry the nap here - this carrier shares its record with the
    // pattern fill, and those are the fields a material leaves untouched.
    float2 halfSize = max(it.LocalBounds.zw * 0.5, float2(1.0, 1.0));
    float2 centred = input.Local - (it.LocalBounds.xy + halfSize);
    float4 surface = SheenSurface(centred, it.Color2, it.Noise, it.Anim);

    // No film grain - see MaterialMetalPS.
    return float4(saturate(surface.rgb), input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}

[shader("fragment")]
float4 MaterialMetalMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    float2 halfSize = max(it.LocalBounds.zw * 0.5, float2(1.0, 1.0));
    float2 centred = input.Local - (it.LocalBounds.xy + halfSize);

    // The chamfer again, but from the bounding box: an authored outline has no distance field here, so the turn follows
    // the box rather than the true edge - the same approximation the glass mesh pass already makes.
    float2 fromCentre = centred / halfSize;
    float edge = saturate(max(abs(fromCentre.x), abs(fromCentre.y)));
    float rise = saturate((edge - 0.82) / 0.18);
    float len = length(fromCentre);
    float2 outward = len > 1e-5 ? fromCentre / len : float2(0.0, 0.0);
    float4 surface = MetalSurface(centred, halfSize, outward * rise * rise * 0.9, it.Color2, it.Noise, it.Anim);

    // No film grain - see MaterialMetalPS.
    return float4(saturate(surface.rgb), input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


[shader("fragment")]
float4 MaterialWoodMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    float2 halfSize = max(it.LocalBounds.zw * 0.5, float2(1.0, 1.0));
    float2 centred = input.Local - (it.LocalBounds.xy + halfSize);

    // The chamfer from the bounding box, as the sibling mesh passes take it: no distance field here, so the facet
    // follows the box rather than the true edge.
    float2 fromCentre = centred / halfSize;
    float edge = saturate(max(abs(fromCentre.x), abs(fromCentre.y)));
    float facet = smoothstep(0.90, 0.93, edge);
    float across = saturate((edge - 0.90) / 0.10);
    float len = length(fromCentre);
    float2 outward = len > 1e-5 ? fromCentre / len : float2(0.0, 0.0);

    // ONE FIGURE ONLY on this carrier, pinned on purpose: the figure code rides the light's fourth component, and on
    // THIS record that component is the clip slot - a number that would decode into a nonsense cut and finish. So a
    // wooden path or star is plain sawn and varnished whatever the brush says. A real limitation, in the tech debt: the
    // record has nothing spare, and giving it a field of its own is what loses the device.
    float4 pinnedLight = float4(it.Anim.xyz, 4.0);   // Flat + varnished

    float4 surface = WoodSurface(centred, halfSize, float3(outward * facet, across * facet),
                                 it.Color2, it.Noise, pinnedLight);

    // No film grain - see MaterialMetalPS.
    return float4(saturate(surface.rgb), input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


// =====================================================================================================================
// TECHNIQUE - one pass per TREATMENT and CARRIER. Frosted serves Acrylic and Mica, which differ in what is CAPTURED and
// not in what is done with it; Glass bends the same capture; the surfaces read no capture at all. Sdf and Mesh are the
// carriers: a shape described by a formula, and one that arrives as triangles.
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

    pass SheenSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialSheenPS;
    }

    pass SheenMesh
    {
        Profile = 6.6;
        VertexShader = MaterialFillVS;
        PixelShader = MaterialSheenMeshPS;
    }

    pass MetalSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialMetalPS;
    }

    pass MetalMesh
    {
        Profile = 6.6;
        VertexShader = MaterialFillVS;
        PixelShader = MaterialMetalMeshPS;
    }

    pass WoodSdf
    {
        Profile = 6.6;
        VertexShader = MaterialRectInstancedVS;
        PixelShader = MaterialWoodPS;
    }

    pass WoodMesh
    {
        Profile = 6.6;
        VertexShader = MaterialFillVS;
        PixelShader = MaterialWoodMeshPS;
    }

}
