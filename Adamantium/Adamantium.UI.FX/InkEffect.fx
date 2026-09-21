// INK - a stroke drawn as ONE shape: one instance per stroke, and the fragment asks how far it is from the whole
// polyline. Not one capsule per segment, which is what stood here first.
//
// Why that had to change, and it is not a matter of degree. A capsule per segment is N separate draws over the same
// pixels, and each one BLENDS. Opaque ink hides that: the second capsule covers what the first put down. Translucent
// ink does not - every overlap composites twice, every joint darkens, and a stroke at half alpha reads as a chain of
// beads rather than a line. A highlighter is exactly a translucent stroke, so a highlighter was impossible.
//
// One instance per stroke fixes it at the root: the coverage of the WHOLE stroke is decided in one fragment, and the
// blend happens once. The points are already on the GPU - they were being expanded into segments - so this reads them
// where they are instead.
//
// HOW THE RECORDS ARE LAID OUT. There is one buffer, and it carries two kinds of record:
//   - a HEADER, one per stroke: the box to cover, the width, the color, and where its points start.
//   - a POINT, one per point of that stroke, written straight after its header.
// The draw issues an instance for every record, header and point alike, because the buffer is one array and the count
// is its length. A point record's vertex shader emits a quad of ZERO area, so it is thrown away before any fragment -
// which costs a vertex and nothing else. That is the price of not needing a second buffer, and it is a small one.
//
// A FIFTH effect, for the reason the third and fourth record: the driver's shader-object compiler has a ceiling on what
// one effect can carry, and adding shaders to BrushEffect has already killed vkCreateShadersEXT on a pass that worked
// for months.

#include "Includes/CommonData.fxh"
#include "Includes/ClipMath.fxh"
#include "Includes/ShapeMath.fxh"

// One record. All float4, and the same shape for both kinds - a buffer of one type is a buffer the draw can index
// without knowing which kind a slot holds.
struct InkRecord
{
    // HEADER: .xy the box's low corner, .zw its high corner - NODE-local. POINT: .xy the point, .zw unused.
    float4 Segment;
    // HEADER: .x half width (node-local), .y transform slot, .z opacity slot, .w 1 = header. POINT: .w 0.
    float4 Params;
    // HEADER: .x rounded-clip slot or -1, .y how many points, .z how far past THIS record the first of them is, .w spare.
    float4 Clip;
    float4 Color;     // straight RGBA, opacity folded into the alpha
};

struct InkPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // the fragment in NODE-local units - the space the points are stated in
    nointerpolation float2 Shape   : TEXCOORD1;   // .x half width (local), .y device pixels per local unit
    nointerpolation uint InstId    : TEXCOORD2;
    nointerpolation float Fade     : TEXCOORD3;
    nointerpolation float4 ClipBox   : TEXCOORD4;
    nointerpolation float4 ClipRadii : TEXCOORD5;
};

[shader("vertex")]
InkPSInput InkSegmentVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    InkRecord* items = (InkRecord*)InstancesAddress;
    InkRecord it = items[instanceId];

    InkPSInput o;

    // A POINT record draws nothing: every corner to the same place, so the quad has no area and no fragment is raised.
    if (it.Params.w < 0.5)
    {
        o.Position = float4(2.0, 2.0, 0.0, 1.0);
        o.Local = float2(0.0, 0.0);
        o.Shape = float2(0.0, 1.0);
        o.InstId = instanceId;
        o.Fade = 0.0;
        o.ClipBox = float4(0.0, 0.0, 0.0, 0.0);
        o.ClipRadii = float4(0.0, 0.0, 0.0, 0.0);
        return o;
    }

    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.y].World;
    float2 px = SlotPixelScale(nodeWorld);
    float iso = min(px.x, px.y);

    // The stroke's own box, grown by the radius and by one DEVICE pixel in local units so the analytic edge has
    // somewhere to fade - a quad cut to the exact radius clips the fade and leaves a hard, aliased rim.
    // THIS RECORD'S OWN SEGMENT, from its point to the next one - the box to cover, and nothing wider. Grown by the
    // radius and by one DEVICE pixel in local units so the analytic edge has somewhere to fade: a quad cut to the exact
    // radius clips the fade and leaves a hard, aliased rim.
    float grow = it.Params.x + 1.0 / max(iso, 1e-6);
    float2 low = min(it.Segment.xy, it.Segment.zw) - grow;
    float2 high = max(it.Segment.xy, it.Segment.zw) + grow;

    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    float2 localPos = lerp(low, high, corner);

    o.Position = mul(mul(float4(localPos, 0.0, 1.0), nodeWorld), Projection);

    o.Local   = localPos;
    o.Shape   = float2(it.Params.x, iso);
    o.InstId  = instanceId;

    int fadeSlot = int(it.Params.z);
    o.Fade = lerp(1.0, nodes[max(fadeSlot, 0)].Params.x, step(0.0, float(fadeSlot)));
    o.ClipBox   = ClipShapeBox(it.Clip.x);
    o.ClipRadii = ClipShapeRadii(it.Clip.x);
    return o;
}

// Distance from a point to a SEGMENT - the whole shape, in one expression. The clamp is what makes the ends round.
float DistanceToSegment(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float t = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-12));

    return length(pa - ba * t);
}

[shader("pixel")]
float4 InkSegmentPS(InkPSInput i) : SV_Target
{
    InkRecord* items = (InkRecord*)InstancesAddress;
    InkRecord it = items[i.InstId];

    // Both offsets are counted FROM THIS RECORD, because the draw bases the buffer at the run it is flushing - see the
    // note where the header is written.
    // SIGNED, because every record but the first of a stroke sits AFTER the point it counts from.
    uint count = (uint)it.Clip.y;
    uint first = (uint)((int)i.InstId + (int)it.Clip.z);

    // The NEAREST segment of the WHOLE polyline, not of the one this quad was raised for - and WHICH one it was, which
    // is the whole test.
    //
    // This quad covers one segment's box and nothing more, so what gets shaded is a chain of little boxes hugging the
    // line rather than one box the size of the stroke. Where two of those boxes lie over the same pixel - at a joint,
    // at a crossing - both fragments run, and only the one that owns the nearest segment keeps it. One owner, one
    // blend: overlapping capsules compositing twice is exactly what made a highlighter impossible, and it is why this
    // pass was one instance per stroke before.
    float nearest = 1e30;
    uint owner = 0u;

    // NEARER BY A MARGIN, not merely nearer: each segment's quad interpolates its own position for the pixel it
    // covers, and those agree only to floating-point - so two fragments comparing all-but-equal distances could name
    // different owners, and the pixel would be drawn twice or not at all. A thousandth of a device pixel is far above
    // that error and far below anything the coverage can express.
    float tie = 1e-3 / max(i.Shape.y, 1e-6);
    float2 at = i.Local;
    float2 previous = items[first].Segment.xy;

    // A stroke of one point is a dot: the loop below does not execute and the distance is to that point alone.
    if (count == 1u) nearest = length(at - previous);

    for (uint s = 1u; s < count; s++)
    {
        float2 current = items[first + s].Segment.xy;
        float away = DistanceToSegment(at, previous, current);

        // STRICTLY nearer, so a tie goes to the earlier segment. Every segment's fragment measures from the same
        // rebuilt position and walks the same points in the same order, so they all name the same winner - which is
        // what makes `exactly one of us draws` true rather than likely.
        if (away < nearest - tie)
        {
            nearest = away;
            owner = s - 1u;
        }

        previous = current;
    }

    // NOT MINE: some other segment of this stroke is nearer to this pixel, and that one is drawing it.
    if (owner != (uint)max(it.Params.w - 1.0, 0.0)) discard;

    // In DEVICE pixels, so the fade is one pixel wide however far the camera is zoomed - which keeps the edge of the
    // ink the same softness at every scale instead of blurring as it grows.
    float distance = (nearest - i.Shape.x) * i.Shape.y;
    float coverage = saturate(0.5 - distance);
    if (coverage <= 0.0) discard;

    // NOT named "color". A pixel-stage local by that name makes this compile into a shader that loses the device -
    // measured 6 starts of 6, against 0 of 6 for the same code under any other name. HLSL semantics are matched
    // case-insensitively and COLOR is a legacy pixel-stage output semantic, so the front end evidently treats the name
    // as one. Nothing else about the pass changes; only the name does.
    float4 tint = it.Color;
    tint.a *= coverage * i.Fade * ClipCoverage(i.Position.xy, i.ClipBox, i.ClipRadii);
    if (tint.a <= 0.0) discard;

    // STRAIGHT, not premultiplied: the blend here is SrcAlpha/OneMinusSrcAlpha, so it does the multiply itself.
    return float4(tint.rgb, tint.a);
}

// =====================================================================================================================
// TECHNIQUE - one pass. Ink is one thing done one way; what varies (where, how wide, what color) varies per instance.
// =====================================================================================================================
technique Ink
{
    pass Segments
    {
        VertexShader = InkSegmentVS;
        PixelShader = InkSegmentPS;
    }
}
