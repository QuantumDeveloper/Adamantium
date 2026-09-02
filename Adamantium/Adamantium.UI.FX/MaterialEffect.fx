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
                         // code - the cut, plus 4 when varnished. Spare here because the anisotropy that used to sit in
                         // it is read by nobody; the mesh carrier keeps its clip slot in the same component, which is
                         // why the mesh wood pass cannot be told either thing
};

// ---------------------------------------------------------------------------------------------------------------
// SHEEN: a lit SURFACE, and the only treatment here that looks at nothing behind it. Three decisions, taken once for
// the whole surface branch (velvet now; suede, wool and felt are this with a coarser nap):
//
//   1. THE NORMAL COMES FROM A NOISE FIELD. A flat rectangle has one normal over its whole area, so any lighting model
//      applied to it collapses into a flat fill. The noise is read as the HEIGHT of the nap and its gradient is the
//      normal - which is why this is a surface and not a gradient with a highlight painted on.
//   2. THE LIGHT IS A BRUSH PROPERTY. The view is fixed and orthographic (down -Z), so a scene light would be a
//      pretence: one direction and one elevation are the honest amount of state.
//   3. NOTHING IS CAPTURED. Velvet does not answer "what is behind me", it answers "what am I made of", so it is
//      priced like a pattern.
//
/// Value noise WITH ITS DERIVATIVE, from one evaluation. The four corner hashes that give the height also give the
/// slope, because a bilinear-smoothstep interpolant has a closed-form gradient - so a relief costs one lookup rather
/// than the three or four TAPS central differences would need.
///
/// That difference is a measurement, not a preference: the first version sampled simplex noise four times per fragment,
/// and once a second surface material shared the file vkCreateShadersEXT began failing on launch after launch - the
/// driver ceiling this file already warns about, and the reason materials are a separate effect at all.
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

// ---------------------------------------------------------------------------------------------------------------
// TWO RELIEFS, ON PURPOSE. Velvet and metal are not one material with different numbers - a pile of fibres and a ground
// steel face have nothing in common but the fact that both are lit. What they may share is the RECORD they are packed
// into; what they must not share is how they look. An earlier version gave them one field, and velvet came out looking
// like brushed metal - which is the whole objection to it.

/// VELVET's nap: an irregular field, stretched 4:1 so the fibres lie combed. Simplex, sampled around the fragment,
/// because cloth wants a field with no period in it at all - a cheaper wave sum reads as corduroy at once.
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

/// METAL's grinding: scratches, not fibres - far more stretched, far shallower, and taken from the cheap noise whose
/// derivative comes with it.
///
/// <para>DAMPED where the scratches fall below a pixel: fine grinding on a scrolling panel goes under the sampling rate
/// and boils, a shimmer far worse than the detail is worth. fwidth says how much of the field one pixel spans; past one
/// period there is nothing left to resolve and the relief is faded to flat rather than aliased.</para>
float3 MetalNormal(float2 p, float scale, float dir)
{
    float2 axis = float2(cos(dir), sin(dir));
    float2 across = float2(-axis.y, axis.x);

    // Fixed, and generous: the scratches of a ground face run one way. It is the RELIEF that is directional here - the
    // reflection lobe is not, see MetalSurface.
    const float stretch = 16.0;

    float s = max(scale, 0.5);
    float2 q = float2(dot(p, axis) / (s * stretch), dot(p, across) / s);

    float3 n = NoiseD(q);
    float2 g = axis * (n.y / (s * stretch)) + across * (n.z / s);

    // SHALLOW. A scratch tilts the surface by a few degrees; deepening it to make the scale knob legible was tried and
    // made the plate worse, not better - it stops being a ground face and starts being a landscape.
    float footprint = length(fwidth(q));
    float depth = 0.06 * saturate(1.0 / (1.0 + footprint * 3.0));

    return normalize(float3(-g * depth * s, 1.0));
}

// The light, as the whole branch states it: a direction on the surface plus an elevation, with 0 grazing and 1 straight
// on. Grazing is what lights a nap and what makes a metal's grinding visible; overhead flattens both.
float3 BranchLight(float4 light)
{
    float elev = saturate(light.z);
    float2 dir = float2(cos(light.y), sin(light.y));
    return normalize(float3(dir * (1.0 - elev), max(elev, 0.08)));
}

/// The Charlie sheen distribution glTF states as KHR_materials_sheen. What makes velvet velvet is in the exponent:
/// brightness rises towards GRAZING angles, so the rim of a fold lights up while its face stays dark - the opposite of
/// an ordinary specular lobe, and the reason a plain Blinn-Phong here looks like plastic.
float SheenD(float ndoth, float roughness)
{
    float a = max(roughness, 0.07);
    float invR = 1.0 / a;
    float sin2 = max(1.0 - ndoth * ndoth, 0.0001);
    return (2.0 + invR) * pow(sin2, invR * 0.5) / 6.2831853;
}

// Ashikhmin's visibility term - the cheap one the sheen extension names, and enough here: there is one light and no
// shadowing geometry to speak of.
float SheenV(float ndotl, float ndotv)
{
    return 1.0 / max(4.0 * (ndotl + ndotv - ndotl * ndotv), 0.0001);
}

/// The whole surface, in the shape's own device-pixel space. Shared by both carriers so a velvet rectangle and a velvet
/// path cannot drift apart in appearance - the only thing that differs between them is how coverage is found.
float4 SheenSurface(float2 p, float4 surface, float4 response, float4 light)
{
    float3 n = NapNormal(p, surface.a, light.x);

    float3 l = BranchLight(light);
    float3 v = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + v);

    float ndotl = saturate(dot(n, l));
    float ndotv = saturate(dot(n, v));
    float ndoth = saturate(dot(n, h));

    // The cloth itself: a soft wrap rather than a hard Lambert, because a nap scatters light round its own fibres and a
    // clamped cosine would leave half of it dead black.
    float wrap = saturate((dot(n, l) + 0.6) / 1.6);
    float3 body = surface.rgb * (0.25 + 0.75 * wrap);

    float3 gleam = response.rgb * (SheenD(ndoth, response.a) * SheenV(ndotl, ndotv) * ndotl);

    return float4(body + gleam, 1.0);
}

// ---------------------------------------------------------------------------------------------------------------
// METAL: the same lit surface, answering with the other half - a GGX lobe stretched along the grinding, and something
// to REFLECT. The third of the branch's shared decisions lives here: what a metal reflects is PROCEDURAL. Behind a user
// interface there is no world, so a capture would give a mirror of the window rather than of a room.

/// The studio: floor below, sky above, a bright band where they meet. Taken by a single "how far up is this ray
/// looking" number, from -1 to 1.
///
/// <para>Which number that is, is the whole difference between a plate that reads as metal and one that reads as paint.
/// The obvious answer - the REFLECTED RAY's height - is wrong here and was tried: the view is fixed and orthographic
/// down -Z and the surface is flat, so every fragment reflects almost straight up, the environment comes back the same
/// colour everywhere, and what is left is a flat fill with one needle of a highlight crawling over it. That needle was
/// also the flicker.</para>
///
/// <para>A real plate sweeps its environment ACROSS ITSELF: the far edge shows the floor, the near edge the sky, and the
/// grinding only shakes that sweep. So the ray's height is where the fragment sits on the plate, tilted by the relief -
/// which is what the caller composes.</para>
float3 StudioEnvironment(float h, float3 sky)
{
    // A room, not a two-tone card. The floor is DIM but never black - a plate in a lit room still gets light from below,
    // and a near-black floor is what once turned the grinding into hard black bars. The range between them is what
    // makes metal read as metal, though: too narrow and the plate goes back to looking like painted plastic.
    float3 ground = sky * 0.30;
    float3 horizon = sky * 1.30;   // the band where they meet is the brightest thing in the room
    float t = saturate(h * 0.5 + 0.5);
    return lerp(lerp(ground, horizon, saturate(t * 2.0)), sky, saturate(t * 2.0 - 1.0));
}

// ---- A METAL'S BRDF, written as one --------------------------------------------------------------------------
// Cook-Torrance with an ANISOTROPIC GGX, in the forms Filament states them. A metal has no diffuse lobe at all: every
// photon it returns is a specular reflection, and its colour IS its reflectance at normal incidence. So the whole
// appearance is D, G and F over one light plus what it reflects of the room - nothing else, and no tuning constants.

/// Anisotropic GGX normal distribution. Two roughnesses instead of one - along the grinding and across it - and that
/// difference is the whole of a brushed finish: the highlight is smeared into a long streak perpendicular to the
/// scratches, because the microfacets scatter widely in one direction and hardly at all in the other.
float MetalDistribution(float at, float ab, float ToH, float BoH, float NoH)
{
    float a2 = at * ab;
    float3 d = float3(ab * ToH, at * BoH, a2 * NoH);
    float d2 = max(dot(d, d), 1e-8);
    float b2 = a2 / d2;
    return a2 * b2 * b2 * (1.0 / 3.14159265);
}

/// Height-correlated Smith visibility for that distribution - the shadowing/masking term WITH the 1/(4 NoL NoV)
/// denominator already folded in, which is why the specular below multiplies rather than divides.
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

/// Bring the plate back into the range a display holds WITHOUT losing its colour. A display clips per channel, and
/// per-channel clipping is colour-blind: gold reflects nearly all of the red it is given and less than half of the blue,
/// so as it brightens the red pins at white while the blue is still climbing, and every metal converges on the same pale
/// plate - which is exactly why gold, copper and steel were telling each other apart on paper and not on screen.
/// Scaling all three channels by the SAME factor, chosen from the colour's own peak, keeps the ratio between them - and
/// that ratio is the whole of what makes gold gold. Below the knee a display is honest and nothing is touched.
float3 ToneRollOff(float3 c)
{
    const float knee = 0.75;

    float peak = max(c.r, max(c.g, c.b));
    if (peak <= knee) return c;

    float mapped = knee + (1.0 - knee) * (1.0 - exp(-(peak - knee) / (1.0 - knee)));
    return c * (mapped / peak);
}

/// <param name="bevelTilt">Which way, and how hard, the EDGE turns the surface over. Zero in the middle of the plate.
/// A milled plate has a chamfer, and that chamfer is where a metal announces itself: the face reflects one part of the
/// room, and the edge - being turned - reflects a quite different one, so a bright rim appears against the field. It is
/// the one place on a flat plate where the normal genuinely changes, which is why leaving it out left the material
/// leaning on the room sweep alone.</param>
float4 MetalSurface(float2 p, float2 halfExtent, float2 bevelTilt, float4 surface, float4 response, float4 light)
{
    float3 n = MetalNormal(p, surface.a, light.x);

    // The chamfer, folded into the same normal everything else reads - so the highlight, the room and the scratches all
    // agree about which way the surface faces there, instead of the rim being painted on afterwards.
    n = normalize(float3(n.xy + bevelTilt, n.z));

    float3 l = BranchLight(light);
    float3 v = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + v);

    // The grinding's own frame: tangent along it, bitangent across.
    float3 t = normalize(float3(cos(light.x), sin(light.x), 0.0));
    float3 b = normalize(cross(n, t));
    t = normalize(cross(b, n));

    // ONE roughness. An anisotropic lobe was here and is gone: stretching it changed the highlight's shape on paper but
    // barely anything on screen, because under a fixed orthographic view with a single light there is no sweep of angles
    // for the long axis to show itself over. A knob whose effect cannot be seen is worse than no knob, so what remains
    // directional in this material is the RELIEF, which is visible, and not the lobe, which was not.
    float perceptual = clamp(response.a, 0.045, 1.0);
    float a = perceptual * perceptual;

    // ...widened by however much the normal varies WITHIN the pixel. Not a fudge: a pixel covering a spread of normals
    // must show the average of what they reflect, and a wider lobe IS that average. Without it a narrow lobe is
    // enormously bright over a very narrow range of normals, and every scroll step makes pixels jump on and off the
    // highlight - the crawling fireflies.
    float3 dnx = ddx(n);
    float3 dny = ddy(n);
    a = saturate(a + (dot(dnx, dnx) + dot(dny, dny)) * 0.5);

    float at = max(a, 0.002);
    float ab = at;

    float NoV = abs(dot(n, v)) + 1e-5;
    float NoL = saturate(dot(n, l));
    float NoH = saturate(dot(n, h));
    float VoH = saturate(dot(v, h));

    // THE LIGHT, through the anisotropic lobe. D x V x F, with the visibility term already carrying 1/(4 NoL NoV).
    // This - not the environment - is what makes a brushed plate look brushed: the lobe is long across the scratches,
    // so one light source is smeared into a band right across them.
    float3 f0 = surface.rgb;
    float3 spec = MetalDistribution(at, ab, dot(t, h), dot(b, h), NoH)
                * MetalVisibility(at, ab, dot(t, v), dot(b, v), NoV, dot(t, l), dot(b, l), NoL)
                * MetalFresnel(f0, VoH) * NoL;

    // THE ROOM, and it is a NEAR room. A conductor has no diffuse lobe, so everything it shows away from the highlight
    // is reflected - and how that reflection varies across the plate is the whole difference between metal and paint.
    //
    // Under a fixed orthographic view a FLAT plate has one normal, so every point reflects the same DIRECTION. With the
    // environment at infinity that makes the plate one flat colour, which is exactly what it looked like. But a room is
    // not at infinity: the ray leaving each point travels a finite distance before it lands, so points at the top of
    // the plate look at the ceiling and points at the bottom at the floor. That is why a real steel panel carries a
    // soft vertical sweep of the room across itself.
    //
    // NOT curvature, and the difference matters: a curved bar changes the NORMAL, which would also bend the highlight
    // and warp the scratches. Here the normal stays flat and only the point of the room being looked at moves - which
    // is what finite distance means.
    float2 uv = p / max(halfExtent, float2(1.0, 1.0));   // -1..1 across the plate
    float3 r = reflect(-v, n);

    const float roomDistance = 1.7;   // in plate half-heights: how far the walls stand off
    float upness = clamp((uv.y + r.y * roomDistance) / (1.0 + roomDistance) * 2.0, -1.0, 1.0);

    // Prefiltered by roughness the cheap way the split-sum approximation allows: a rough surface averages the room over
    // a wide lobe, and the average of this environment is its own mid-tone. A polished plate keeps the sweep sharp; a
    // satin one washes it towards flat, which is the honest difference between the two finishes.
    float3 sharp = StudioEnvironment(upness, response.rgb);
    float3 blurred = StudioEnvironment(0.0, response.rgb);
    float3 env = lerp(sharp, blurred, saturate(perceptual * 1.6)) * MetalFresnel(f0, NoV);

    // ROLL THE HIGHLIGHT OFF instead of clipping it. A GGX peak at low roughness is worth hundreds, and a display holds
    // one - so without this the light has a knife-edge range: a few degrees of blow-out to white, and outside them no
    // highlight at all and nothing left but a flat reflection. Compressing by the highlight's own peak channel makes it
    // saturate towards the METAL'S OWN COLOUR and keeps it legible across the whole sweep of the light.
    //
    // Only the specular. The environment is already inside the display's range, and compressing it too would drag the
    // whole plate grey - which is the "dull, cannot make out the surface" end of the same complaint.
    spec = spec / (1.0 + max(spec.r, max(spec.g, spec.b)));

    return float4(ToneRollOff(env + spec), 1.0);
}

// ---- WOOD ------------------------------------------------------------------------------------------------------
// The odd one of the branch. Velvet and metal are lighting models over a plain colour; wood is a PATTERN that happens
// to be lit, and getting the pattern right matters far more here than getting the lobe right.
//
// What the pattern IS: a tree lays down one ring a year - a broad pale band while it grows fast in spring, closed by a
// narrow dense dark one in summer - and those rings are concentric cylinders about the trunk's core. A board is a
// SLICE through that stack, so its face shows where the cut plane crossed the cylinders. That is the whole reason
// timber shows arches rather than stripes, and it is why this is built from a distance to an axis rather than from a
// stripe function: stripes are what a plane parallel to the rings would give, and nobody saws boards that way.
//
// THREE noise evaluations, and that is a budget rather than a taste: this file already lost launches to
// vkCreateShadersEXT once, and the fix was to spend fewer taps (see NoiseD). One tap wanders the core, one roughens the
// rings, one draws the fibre.
float4 WoodSurface(float2 p, float2 halfExtent, float2 bevelTilt, float4 surface, float4 response, float4 light)
{
    // The figure code, unpacked: the cut, and 4 added on top when the board carries a finish.
    float varnished = step(3.5, light.w);
    float cut = light.w - varnished * 4.0;

    float s = max(surface.a, 0.5);
    float dir = light.x;

    float2 axis = float2(cos(dir), sin(dir));     // along the grain: the trunk's length
    float2 across = float2(-axis.y, axis.x);

    float u = dot(p, axis) / s;
    float v = dot(p, across) / s;

    // TWO TAPS, TAKEN ONCE, before the cut is chosen. Branching to take extra samples is how this file lost launches
    // before, so the budget stays fixed no matter which way the plank was sawn.
    float3 wander = NoiseD(float2(u * 0.11, 0.0));
    float3 rough = NoiseD(float2(u * 0.45, v * 0.45));

    // THE RING'S FOOTPRINT, MEASURED HERE - before any branch, and from the COORDINATES rather than from the distance
    // built out of them. Two things depend on that order. Screen derivatives taken inside divergent control flow are
    // undefined by the rules and, on this driver, produce a shader that will not create at all; and computing every
    // cut just to keep the flow straight makes the shader heavy enough that the driver dies creating it anyway - which
    // is exactly the pair of failures this function has already caused. Measured at the source, the branch below is
    // free to be a branch. It is an ANTI-ALIASING width, so a footprint within a factor of the true one is enough.
    float w = max(length(fwidth(float2(u, v))), 0.0015);

    // WHERE THE CORE IS, WHICH IS THE WHOLE OF THE CUT. The rings never change: they are cylinders about the trunk's
    // axis, and every one of these four figures is that same distance-to-the-axis seen from a different plane - which
    // is why they share one formula and differ only in where the axis is put.
    float r;

    if (cut < 0.5)
    {
        // PLAIN SAWN: the plane runs BESIDE the core without meeting it. A trunk leans, tapers and bends, so its axis
        // wanders relative to the face - and the arches everybody pictures as wood are the places where that wandering
        // axis comes closest to the board and the rings close into a nest. A straight axis gives dead parallel bands.
        float centre = wander.x * 7.0 - 3.5;
        float depth = 1.2 + wander.x * 3.0;
        r = length(float2(v - centre, depth)) + rough.x * 0.75;
    }
    else if (cut < 1.5)
    {
        // QUARTER SAWN: the plane passes THROUGH the core, so it meets every ring square on and they land as narrow,
        // evenly spaced lines running the length of the board. No arch is possible here - that is the point of the cut -
        // and the small wander is all that keeps them from looking ruled.
        r = v + wander.x * 0.9 + rough.x * 0.2;
    }
    else if (cut < 2.5)
    {
        // END GRAIN: the plane is ACROSS the trunk, so the cylinders show as what they are - rings about the core.
        r = length(float2(u, v)) + rough.x * 0.5;
    }
    else
    {
        // BURL: a knot of dormant buds where the grain has no direction left. Not a clean cut through an orderly log,
        // so the distance itself is dragged about before the rings are taken from it.
        r = length(float2(u, v) + float2(wander.x - 0.5, rough.x - 0.5) * 9.0) + rough.x * 2.5;
    }

    // EACH YEAR, and the sharp edge is real. The step from summer's dense wood back to the next spring's open growth is
    // abrupt in the timber, which is why a ring reads as a LINE; the soft edge is the other side of the same band.
    // Widened by the footprint above so that a plate seen small washes to the average colour instead of shimmering -
    // the same discipline as the metal lobe, and needed for the same reason.
    float ring = frac(r);
    float late = smoothstep(0.72 - w, 0.86, ring) * (1.0 - smoothstep(1.0 - w, 1.0, ring));

    // Past the point where a whole ring falls inside one pixel there is nothing left to resolve, so stop pretending and
    // settle on the proportion of late wood a ring actually has. Without this the rings alias into moire.
    late = lerp(late, 0.28, saturate(w * 2.0 - 0.35));

    // THE FIBRE: cells run ALONG the trunk, so the streaks must be long that way and fine across it. Same field as the
    // rings, read with the axes swapped in scale - which is all "grain" means here.
    float3 fibre = NoiseD(float2(u * 0.30, v * 8.0));

    float3 colour = lerp(surface.rgb, response.rgb, saturate(late));
    colour *= 0.88 + fibre.x * 0.24;

    // The relief is the FIBRE and not the rings: on a planed board the rings are colour, while the open pores of spring
    // growth are what a fingernail catches. Shallow, and shallower still where the fibre is finer than the pixel.
    float2 g = axis * (fibre.y * 0.30) + across * (fibre.z * 8.0);
    float damp = saturate(1.0 / (1.0 + length(fwidth(float2(u * 0.30, v * 8.0))) * 3.0));
    float3 n = normalize(float3(-g * 0.035 * damp, 1.0));

    // The chamfer, folded into that same normal - and on wood it does more work than on metal, because a clear coat
    // reflects almost nothing head-on and almost everything at a grazing angle. The rim is therefore where a finish
    // ANNOUNCES itself, and a plank without one is where a raw board is easiest to tell from a lacquered one.
    n = normalize(float3(n.xy + bevelTilt, n.z));

    float3 l = BranchLight(light);
    float3 vdir = float3(0.0, 0.0, 1.0);
    float3 h = normalize(l + vdir);

    float wrap = saturate((dot(n, l) + 0.35) / 1.35);
    float3 body = colour * (0.55 + 0.45 * wrap);

    // How much the normal varies within this pixel, taken HERE - outside the finish branch below, for the same reason
    // the ring's footprint is taken before the cut is chosen.
    float nVariance = length(fwidth(n));

    // RAW TIMBER STOPS HERE, and stops for real: bare wood has no film on it to reflect anything, however smoothly it
    // is planed, so the finish is a separate answer from the roughness rather than a value of it. A branch and not a
    // blend, because everything below - a full specular lobe and a room reflection - is worth skipping outright, and
    // that weight is what once made this shader too much for the driver to create.
    if (varnished < 0.5) return float4(ToneRollOff(body), 1.0);

    float NoV = abs(dot(n, vdir)) + 1e-5;
    float NoL = saturate(dot(n, l));
    float NoH = saturate(dot(n, h));
    float VoH = saturate(dot(vdir, h));

    float perceptual = clamp(response.a, 0.06, 1.0);
    float a = saturate(perceptual * perceptual + nVariance * 0.5);
    float at = max(a, 0.002);

    // Wood is a DIELECTRIC, so unlike metal it keeps BOTH lobes: the colour above is what its body scatters back, and
    // the clear coat sits on top with an F0 of 0.04 - the same for every finish, because gloss and satin differ in
    // roughness, not in reflectance.
    const float3 coatF0 = float3(0.04, 0.04, 0.04);

    float3 varnish = MetalDistribution(at, at, dot(axis, h.xy), dot(across, h.xy), NoH)
                   * MetalVisibility(at, at, dot(axis, vdir.xy), dot(across, vdir.xy), NoV,
                                     dot(axis, l.xy), dot(across, l.xy), NoL)
                   * MetalFresnel(coatF0, VoH) * NoL;

    varnish = varnish / (1.0 + max(varnish.r, max(varnish.g, varnish.b)));

    // AND THE ROOM IN IT, which is what was missing. A single light over a nearly flat board gives a highlight that is
    // the SAME everywhere - an even wash, not a highlight at all, and the reason the varnish could not be seen. What
    // makes a polished surface read as polished is that you can see the room in it, and the room's reflection VARIES
    // across the board because the room is at a finite distance. The same near-field argument metal needed.
    float2 uv = p / max(halfExtent, float2(1.0, 1.0));
    float3 rdir = reflect(-vdir, n);

    const float roomDistance = 1.7;
    float upness = clamp((uv.y + rdir.y * roomDistance) / (1.0 + roomDistance) * 2.0, -1.0, 1.0);

    float3 sharp = StudioEnvironment(upness, float3(0.86, 0.87, 0.9));
    float3 blurred = StudioEnvironment(0.0, float3(0.86, 0.87, 0.9));
    float3 room = lerp(sharp, blurred, saturate(perceptual * 1.6));

    // Schlick over the coat, and the grazing rise is the whole point: about four per cent head-on, nearly everything
    // edge-on. That is why a polished table looks like wood from above and like a mirror from across the room, and it
    // is what lights the chamfer up.
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

    // No film grain: see the metal pass - a surface has no capture whose banding would need hiding.
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

    // No film grain here. It exists to hide the banding an 8-bit CAPTURE brings, and a surface captures nothing - adding
    // it would be noise laid over a material that already has its own.
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

    // The chamfer, from the shape's own distance field - the same one the metal takes. It matters more here: a clear
    // coat reflects almost nothing head-on and almost everything at a grazing angle, so the turned rim is where a
    // finish shows itself at all.
    // The chamfer, from the shape's own distance field - the same one the metal takes. It matters more here: a clear
    // coat reflects almost nothing head-on and almost everything at a grazing angle, so the turned rim is where a
    // finish shows itself at all.
    const float bevelWidth = 9.0;   // device px of eased edge
    float2 outward = GlassSlope(d, input.Local);
    float rise = GlassCurve(d, bevelWidth);
    float4 surface = WoodSurface(input.Local, input.Half, outward * rise * 0.9,
                                 it.Surface, it.Response, it.Light);

    // No film grain - see the metal pass: a surface has no capture whose banding would need hiding.
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

    // No film grain - see the SDF pass: a surface has no capture whose banding would need hiding.
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

    // No film grain - see the SDF pass.
    return float4(saturate(surface.rgb), input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


[shader("fragment")]
float4 MaterialWoodMeshPS(MaterialMeshPSInput input) : SV_Target
{
    PatternGeomData* items = (PatternGeomData*)InstancesAddress;
    PatternGeomData it = items[input.InstId];

    float2 halfSize = max(it.LocalBounds.zw * 0.5, float2(1.0, 1.0));
    float2 centred = input.Local - (it.LocalBounds.xy + halfSize);

    // The chamfer from the bounding box, as the metal and glass mesh passes take it: an authored outline has no
    // distance field here, so the turn follows the box rather than the true edge.
    float2 fromCentre = centred / halfSize;
    float edge = saturate(max(abs(fromCentre.x), abs(fromCentre.y)));
    float rise = saturate((edge - 0.82) / 0.18);
    float len = length(fromCentre);
    float2 outward = len > 1e-5 ? fromCentre / len : float2(0.0, 0.0);

    // ONE FIGURE ONLY on this carrier, and pinned here on purpose. The figure code rides the light's fourth component,
    // and on THIS record that component is the rounded clip's slot - a number that would decode into a nonsense cut and
    // a nonsense finish. So a wooden path or star is plain sawn and varnished, whatever the brush says. It is a real
    // limitation, written up in the tech debt rather than hidden: the record has nothing spare, and giving it a field
    // of its own is what loses the device.
    float4 pinnedLight = float4(it.Anim.xyz, 4.0);   // Flat + varnished

    float4 surface = WoodSurface(centred, halfSize, outward * rise * rise * 0.9,
                                 it.Color2, it.Noise, pinnedLight);

    // No film grain - see the SDF pass.
    return float4(saturate(surface.rgb), input.Fade * it.Color3.w * ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii));
}


// =====================================================================================================================
// TECHNIQUE - one pass per TREATMENT and CARRIER. Frosted serves both Acrylic and Mica: they differ in what is CAPTURED,
// not in what is done with it. Glass bends the same capture instead of scattering it. Sheen reads no capture at all - it
// lights a surface. Sdf and Mesh are the two carriers: a shape described by a formula, and one that arrives as
// triangles.
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
