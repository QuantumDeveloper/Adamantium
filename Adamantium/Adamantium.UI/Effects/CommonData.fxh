// SHARED by every UI effect that draws instanced, SDF-shaped geometry - BatchEffect.fx (the shapes) and BrushEffect.fx
// (the fills). ONE header, not one per effect: what lives here is the contract BETWEEN them, and the moment the same
// declaration exists in two places the two effects start to disagree about the record they are both reading.
//
// What belongs here: the per-vertex layouts, the globals BOTH effects need, and the maths every fill re-uses (the SDF
// shapes, the stroke/dash compositing, the fringe expansion). What does NOT: a global only one effect reads. That is
// not tidiness - BatchEffect's own notes record that ONE unused uint64_t declaration killed shader creation 3 runs out
// of 3 on this driver, so a parameter block carries a real budget and neither effect may spend the other's.
struct UI_VERTEX
{
    float4 position : POSITION;
    float3 normal: NORMAL;
    float2 uv0: TEXCOORD0;
    float2 uv1: TEXCOORD1;
};

struct VERTEX_OUTPUT
{
    float4 position : SV_POSITION;
    float3 normal: NORMAL;
    float2 uv0: TEXCOORD0;
    float2 uv1: TEXCOORD1;
};

// ---- Globals both effects declare ------------------------------------------------------------------------------
// ---- Globals both effects declare -----------------------------------------------------------------------------
float4x4 Projection;

// Global frame time in seconds, advanced by the render loop each present. Only the fractal's auto-morph reads it; every
// other pass ignores it. Unset (0) = no drift, so a static fractal renders fine before the loop starts feeding it.
float Time;

// Per-instance data by BUFFER DEVICE ADDRESS (BDA), not a descriptor-heap StructuredBuffer: the SV_InstanceID-indexed
// StructuredBuffer form did not bind/read on this device (the fill rasterised nothing - World came out garbage), while
// BDA is the engine's proven GPU-storage pattern (see StrokeEffect/FillFringeEffect: uint64_t address + (T*)addr).
uint64_t InstancesAddress;

float2 ViewportSize;      // render target size in DEVICE pixels - the NDC <-> pixel basis for the fringe offset

float FringePixels;       // fringe width in DEVICE pixels

// GPU-resident TRANSFORM TABLE (see Rendering/TransformTable.cs): one world matrix per MOTION NODE (a scrolled panel, an
// animating tile), fetched by the per-instance slot index. Slot 0 is ALWAYS identity, so world-baked instances (index 0)
// render unchanged - the migration path: content moves to node-LOCAL bounds + a real slot incrementally, and from then on
// moving a node costs ONE 64-byte matrix write instead of re-baking its instances. Full matrices also keep ROTATED/3D
// instances inside the batch (the old axis-aligned world bake had to reject them to per-unit draws).
uint64_t TransformsAddress;

// One entry of that table. Alpha lives HERE, beside the matrix, rather than in a second table with a second address:
// adding one more global uint64_t to this effect stopped shader creation outright (measured - a declaration alone, used
// by nothing, killed startup 3 times out of 3, while the same build without it started 3 of 3). The parameter block is
// evidently at its limit, and one slot's matrix and alpha are one node's state anyway, so they belong together.
// Params.x = alpha (1 = opaque); .y = PARENT opacity slot (-1 = none, composed on the CPU); .zw reserved. Padded to
// 16 bytes so the struct stays 16-byte aligned.
struct NodeSlot
{
    float4x4 World;
    float4   Params;
};

// ---- Bound resources and the records both effects read ----------------------------------------------------------
// ONE bound texture per segment, and it is shared by both effects, which is not obvious: the brushes sample it for an
// ImageBrush or a nine-slice, and the HALO samples it too - an arbitrary shape has no closed-form distance, so its
// glow reads a BAKED distance field through this very sampler (HaloFieldDistance). Moving it to the brushes alone left
// BatchEffect with an undefined identifier in the halo, and Slang then failed EVERY shader in the file, not just that
// one - which reads as "the header is not being included" and is not.
// t2/s2, NOT t1/s1: the glyph atlas of FontEffect.fx sits at t1 and the descriptor slots are shared across the frame -
// bound at t1 this sampler read the atlas and every nine-slice came out drawn with letters.
Texture2D SourceTexture : register(t2);
SamplerState SourceSampler : register(s2);

// One PROCEDURAL fill instance on arbitrary geometry. Shared because its FILL is a brush (BrushEffect evaluates the
// pattern from it) while its FRINGE is not: a one-pixel ring does not evaluate a pattern, it just takes the brush's low
// colour, so the ring is the same flat pass the solid fills use and lives with them in BatchEffect. Both read this
// record, so it belongs to neither.
struct PatGeomData
{
    float4x4 Local;      // element local -> SLOT space (the slot's matrix is applied on top, from the transform table)
    float4 Params;       // .y pattern type, .z cell (LOCAL units), .w transform-table slot. .x = opacity slot
    float4 LocalBounds;  // shape local bounds: minXY, sizeXY
    float4 Color1;
    float4 Color2;
    float4 Color3;
    float4 Noise;        // x octaves (sign=animate), y seed, z lacunarity, w gain (or combustible fire-palette flag)
    float4 Anim;         // .x = offset subtracted from the clock while animating, .y = the phase held while paused
};

// ---- InstancedFringe pass: the analytic-AA fringe of the SAME instances, as one instanced draw --------------------
// The ring (Rendering/FringeGeometry.cs) is scale-free - a contour point plus, on the outer edge, the two adjacent edge
// DIRECTIONS - so every instance of a mesh shares ONE ring buffer and reads its own transform/colour from the SAME
// GeometryInstance buffer the body pass used. That is what replaces the old per-element fringe draw (which cost one
// pipeline switch + one uniform matrix per element and dominated the frame). The width is applied HERE, in device
// pixels, so it stays one pixel at any zoom.
struct FringeVertex
{
    float2 Position : POSITION;
    float2 Dir0     : TEXCOORD0;   // incoming edge direction, Winding folded into its sign; zero on the contour itself
    float2 Dir1     : TEXCOORD1;   // outgoing edge direction
};

struct FringePSInput
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float  Coverage : TEXCOORD0;
};

