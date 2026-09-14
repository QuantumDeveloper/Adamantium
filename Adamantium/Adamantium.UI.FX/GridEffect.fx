// THE CANVAS GRID - an unbounded plane of marks, decided per fragment from the world coordinate under it.
//
// A FOURTH effect, and for the reason the third one records: the driver's shader-object compiler has a ceiling on what
// one effect can carry, and adding shaders to BrushEffect has already killed vkCreateShadersEXT on a pass that had
// worked for months. So the grid gets its own parameter block and its own shader objects.
//
// What it replaces: the canvas used to emit one rectangle per mark. Measured at about a thousand a frame at 1:1, and
// growing as viewport area over pitch squared - three thousand at the density the step is allowed to reach, four times
// that again on a 4K viewport. Here it is ONE quad: nothing is generated for the grid, and an unbounded grid never
// exists as geometry for a moment.
//
// Two things become possible that the mark-at-a-time version could not do at all. A mark is ANALYTICALLY covered, so a
// line is exactly its width at any zoom instead of snapping between one pixel and two. And the step CROSS-FADES: the
// finer level dissolves in as it becomes readable rather than the whole grid jumping from tens to hundreds at once,
// which is what a grid in a 3D editor does and what makes it feel like a plane rather than a picture of one.

#include "Includes/CommonData.fxh"
#include "Includes/ClipMath.fxh"
#include "Includes/ShapeMath.fxh"

// One grid instance. All float4 - there is exactly ONE of these in a draw, so packing colors into bytes would buy
// nothing and cost the question of how the two of them align against the fields after.
struct CanvasGridData
{
    float4 Bounds;     // NODE-local x, y, w, h
    float4 Params;     // .x transform slot, .y marks (1 dots, 2 lines), .z opacity slot, .w mark size (logical px)
    float4 Camera;     // .xy where the world's ORIGIN sits on screen (logical px), .z screen px per world unit, .w spare
    float4 Step;       // .x the step to draw (world units, ALREADY coarsened); .y coarsening; .z 1 / the pitch a mark
                       // must keep on screen, as a RECIPROCAL - the shader must not divide; .w spare
    float4 Clip;       // .x the ancestor's rounded-clip slot, or -1
    float4 Background; // the GROUND, straight RGBA - carried here because the grid is flushed first of its clip group
    float4 GridColor;  // straight RGBA
    float4 AxisColor;  // straight RGBA; alpha 0 leaves the world's axes undrawn
};

struct GridPSInput
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;   // fragment in the ELEMENT's own logical units - the space the camera is stated in
    nointerpolation uint InstId : TEXCOORD1;
    nointerpolation float Scale : TEXCOORD2;   // device pixels per logical unit
    nointerpolation float Fade  : TEXCOORD3;
    nointerpolation float4 ClipBox   : TEXCOORD4;
    nointerpolation float4 ClipRadii : TEXCOORD5;
};

[shader("vertex")]
GridPSInput CanvasGridVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    CanvasGridData* items = (CanvasGridData*)InstancesAddress;
    CanvasGridData it = items[instanceId];

    GridPSInput o;
    float2 corner = float2(vertexId & 1u, (vertexId >> 1u) & 1u);
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)it.Params.x].World;
    float2 px = SlotPixelScale(nodeWorld);

    // No outset: the grid has no pen and must not paint one pixel outside the element - it is the ground the canvas
    // stands on, and the canvas clips to its bounds.
    float2 localPos = it.Bounds.xy + corner * it.Bounds.zw;
    float4 worldPos = mul(float4(localPos, 0.0, 1.0), nodeWorld);

    o.Position = mul(worldPos, Projection);
    // RELATIVE to the element, not to the node: the camera is stated in the canvas's own coordinates, and that is the
    // only space in which "where the origin sits" means anything.
    o.Local  = localPos - it.Bounds.xy;
    o.InstId = instanceId;
    o.Scale  = min(px.x, px.y);
    int fadeSlot = int(it.Params.z);
    o.Fade = lerp(1.0, nodes[max(fadeSlot, 0)].Params.x, step(0.0, float(fadeSlot)));
    o.ClipBox   = ClipShapeBox(it.Clip.x);
    o.ClipRadii = ClipShapeRadii(it.Clip.x);
    return o;
}

// How much of this pixel a mark of the given half-width covers, given the distance to the nearest one. Both in DEVICE
// pixels, so the ramp is one pixel wide by construction - which is what makes a line exactly its width at any zoom
// rather than snapping between one pixel and two.
float MarkCoverage(float distancePx, float halfWidthPx)
{
    return saturate(halfWidthPx + 0.5 - distancePx);
}

// Distance in DEVICE pixels from this fragment to the nearest line of the given step, per axis.
float2 DistanceToLines(float2 world, float step, float pixelsPerUnit)
{
    float2 cell = world / step;
    return abs(frac(cell + 0.5) - 0.5) * step * pixelsPerUnit;
}

// The marks: lines take the nearer of the two axes, dots want both at once - a dot is where the two crossings meet,
// which is the product and not the maximum.
float LevelCoverage(float2 world, float step, float pixelsPerUnit, float halfWidthPx, float marks)
{
    float2 d = DistanceToLines(world, step, pixelsPerUnit);
    float cx = MarkCoverage(d.x, halfWidthPx);
    float cy = MarkCoverage(d.y, halfWidthPx);

    return marks > 1.5 ? max(cx, cy) : cx * cy;
}

[shader("pixel")]
float4 CanvasGridPS(GridPSInput i) : SV_Target
{
    CanvasGridData* items = (CanvasGridData*)InstancesAddress;
    CanvasGridData it = items[i.InstId];

    float marks = it.Params.y;

    // Everything below is in DEVICE pixels: the camera is stated in logical ones, and Scale is how many device pixels a
    // logical unit is worth. Doing it once here is what keeps the grid the same weight on a high-DPI monitor.
    float pixelsPerUnit = it.Camera.z * i.Scale;
    float2 world = (i.Local - it.Camera.xy) / max(it.Camera.z, 1e-6);

    // The step arrives ALREADY COARSENED - the canvas works it out from the camera and hands it over, which it has to
    // do anyway for rulers and snapping; computing it twice is how the two come to disagree.
    float step = max(it.Step.x, 1e-6);
    float halfWidth = max(it.Params.w * i.Scale, 0.5) * 0.5;

    // TWO levels, and BOTH readable: the step, and the one a coarsening above it. Never the one BELOW - that one is
    // closer than the readable pitch by construction, so drawing it is not a fainter grid but a dense sub-pixel wash
    // under the readable one, which is what every attempt to fade it produced.
    //
    // The main level fades as its own marks approach the pitch, which is exactly when the step is about to be taken up
    // a level. At that moment the accent is already drawn at full and becomes the new main - so the handover has
    // nothing to jump over. In LOGICAL pixels, and against a reciprocal the canvas sends: a division added here is what
    // stopped the driver creating the shader at all.
    float mainPitch = step * it.Camera.z;
    float blend = saturate(mainPitch * it.Step.z - 1.0);
    float stepAccent = step * max(it.Step.y, 2.0);

    // marks < 0.5 means none at all: the ground still gets painted, because the grid IS the canvas's ground and turning
    // the marks off must not turn the canvas transparent.
    float coverage = 0.0;
    if (marks >= 0.5)
    {
        coverage = LevelCoverage(world, stepAccent, pixelsPerUnit, halfWidth, marks);
        coverage = max(coverage, LevelCoverage(world, step, pixelsPerUnit, halfWidth, marks) * blend);
    }

    float4 marksColor = it.GridColor;
    marksColor.a *= coverage;

    // The marks OVER the ground, one composite - so the element is one draw and not two.
    //
    // NOT named "color". A pixel-stage local by that name makes this compile into a shader that loses the device -
    // measured 6 starts of 6, against 0 of 6 for the same code under any other name, and 0 of 6 for a comment-only
    // change, so it is the NAME and not the recompile. HLSL semantics are matched case-insensitively and COLOR is a
    // legacy pixel-stage output semantic, so the front end evidently treats the name as one.
    float4 composited = float4(lerp(it.Background.rgb, marksColor.rgb, marksColor.a),
                           it.Background.a + marksColor.a * (1.0 - it.Background.a));

    // The world's OWN axes, over the grid: on a plane with no edges they are the only thing that says where the origin
    // is. Drawn as full lines whatever the marks are - an axis made of dots would not read as an axis.
    if (it.AxisColor.a > 0.0)
    {
        float2 axisDistance = abs(world) * pixelsPerUnit;
        float onAxis = max(MarkCoverage(axisDistance.x, halfWidth), MarkCoverage(axisDistance.y, halfWidth));
        float4 axis = it.AxisColor;
        axis.a *= onAxis;
        // OVER, not added: where an axis crosses a grid line the two must not brighten each other into a knot.
        composited = float4(lerp(composited.rgb, axis.rgb, axis.a), composited.a + axis.a * (1.0 - composited.a));
    }

    composited.a *= i.Fade * ClipCoverage(i.Position.xy, i.ClipBox, i.ClipRadii);
    if (composited.a <= 0.0) discard;

    // STRAIGHT, not premultiplied - the blend is SrcAlpha/OneMinusSrcAlpha and does the multiply itself. Every other
    // pass in the engine returns straight for the same reason.
    return float4(composited.rgb, composited.a);
}

// =====================================================================================================================
// TECHNIQUE - one pass. A grid is one thing done one way; what varies (dots or lines, how fine, what color) varies per
// instance and not per shader.
// =====================================================================================================================
technique CanvasGrid
{
    pass Marks
    {
        VertexShader = CanvasGridVS;
        PixelShader = CanvasGridPS;
    }
}
