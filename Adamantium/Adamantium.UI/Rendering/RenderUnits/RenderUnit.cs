using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.UI.Rendering.Retained;

namespace Adamantium.UI.Rendering.RenderUnits;

public abstract class RenderUnit<TPayload> : DeferredDisposableObject, IRenderUnit, IInstanceableFill where TPayload : class
{
    protected RenderUnit(IDrawCommand command, RenderUnitContext context) : base(context.GraphicsDevice)
    {
        Context = context;
        DrawCommand = command;
        Payload = DrawCommand.Payload as TPayload;
    }

    // All the shared services (device, effects, resource factory, buffer manager) live here, so adding a new one is a
    // one-line change to RenderUnitContext instead of a new parameter on every unit constructor and the factory.
    protected RenderUnitContext Context { get; }

    public TPayload Payload { get; protected set; }

    public UIBasicEffect UIBasicEffect => Context.UIBasicEffect;

    // Global toggle: lets the stroke fall back to the CPU path on the fly if anything is wrong with the GPU path.
    public static bool UseGpuStroke { get; set; } = true;

    protected StrokeEffect StrokeEffect => Context.StrokeEffect;

    // Analytic-AA fill fringe (GPU coverage edge). On like UseGpuStroke; toggle off to A/B against MSAA.
    public static bool UseGpuFill { get; set; } = true;

    protected FillFringeEffect FillFringeEffect => Context.FillFringeEffect;

    protected GpuBufferManager BufferManager => Context.BufferManager;

    public UIRenderComponent StrokeRenderer { get; set; }

    // The analytic-AA coverage fringe around a solid fill's contour (drawn on top of the body). Null = no fill AA.
    public UIRenderComponent FillFringeRenderer { get; set; }

    public UIRenderComponent GeometryRenderer { get; set; }

    // --- retained geometry-instancing (IInstanceableFill) -------------------------------------------------------------
    public bool FillInstanced { get; set; }

    // Not instanceable by default (a unit draws its fill body per-unit). GeometryRenderUnit overrides this for solid
    // arbitrary geometry so N identical shapes collapse to one instanced draw.
    public virtual bool TryGetInstancedFill(out GeometryKey key, out object mesh, out Vector4F color)
    {
        key = default;
        mesh = null;
        color = default;
        return false;
    }

    protected void ProcessStrokeData(Pen pen, Geometry geometry)
    {
        if (pen == null) return;

        StrokeRenderer?.DeferDispose();

        // The GPU path handles any solid-colour stroke: one OR many contours (combined/group geometry, shapes with
        // holes), open or closed, with dashes/trim and every cap/join - all as real geometry. Only a non-solid brush
        // (e.g. a gradient stroke) still falls back to the CPU StrokeGeometry path.
        if (UseGpuStroke && StrokeEffect != null && TryGetSolidContours(pen, geometry, out var contours))
        {
            StrokeRenderer = new GpuStrokeRenderComponent(GraphicsDevice, UIBasicEffect, StrokeEffect, contours, pen, BufferManager);
        }
        else
        {
            var strokeGeometry = new StrokeGeometry(pen, geometry);
            StrokeRenderer = new StrokeRenderComponent(GraphicsDevice, UIBasicEffect, strokeGeometry.Mesh, pen, BufferManager);
        }
        StrokeRenderer.RenderData = DrawCommand.RenderData;
    }

    // Every outline contour of a solid-colour stroke (combined/group geometry yields several), each as its raw points
    // plus whether it is a closed loop. False (CPU fallback) only for a non-solid brush or a geometry with no contour.
    private static bool TryGetSolidContours(Pen pen, Geometry geometry, out List<(Adamantium.Mathematics.Vector2[] Points, bool IsClosed)> contours)
    {
        contours = null;
        if (pen.Brush is not SolidColorBrush) return false;

        var mesh = geometry?.Mesh;
        if (mesh == null || mesh.Contours.Count == 0) return false;

        List<(Adamantium.Mathematics.Vector2[], bool)> list = [];
        foreach (var contour in mesh.Contours)
            if (contour.Points is { Length: >= 2 })
                list.Add((contour.Points, contour.IsGeometryClosed));

        if (list.Count == 0) return false;
        contours = list;
        return true;
    }

    // On a geometry change, try to rewrite the existing GPU stroke's points in place (same topology + size-compatible
    // pen) instead of rebuilding it - the resize fast path. False (caller rebuilds via ProcessStrokeData) for the CPU
    // stroke, the dash/cut path, or a topology/pen-size change.
    protected bool TryUpdateStroke(Pen pen, Geometry geometry)
    {
        if (pen == null || StrokeRenderer is not GpuStrokeRenderComponent gpuStroke) return false;
        if (!TryGetSolidContours(pen, geometry, out var contours)) return false;
        if (!gpuStroke.TryUpdateGeometry(contours, pen)) return false;

        gpuStroke.RenderData = DrawCommand.RenderData;
        return true;
    }

    // Analytic AA: build a GPU coverage fringe around a SOLID fill's CLOSED contours, drawn over the body for a soft
    // ~1px edge. Only a solid-colour fill with a real closed contour gets it; otherwise no fringe (no fill AA).
    protected void ProcessFillFringe(Geometry geometry, Brush brush)
    {
        FillFringeRenderer?.DeferDispose();
        FillFringeRenderer = null;
        // A solid OR a gradient fill gets an analytic-AA fringe (the fringe shader now colours the ring by the gradient
        // too - so a tessellated gradient shape no longer has aliased edges). Image/null fills still don't.
        if (!UseGpuFill || FillFringeEffect == null || brush is not (SolidColorBrush or GradientBrush)) return;

        // Feather the RESOLVED fill boundary (outer outline + holes, extracted from the fill mesh) so self-intersecting /
        // holed shapes get their inner edges AA'd too; fall back to the raw path contours if extraction finds nothing.
        var contours = FillBoundary.ExtractLoops(geometry?.Mesh) ?? BuildFillContours(geometry);
        if (contours == null) return;

        FillFringeRenderer = new GpuFillRenderComponent(GraphicsDevice, UIBasicEffect, FillFringeEffect, contours, brush, BufferManager);
        FillFringeRenderer.RenderData = DrawCommand.RenderData;
    }

    // The closed contours (>= 3 points) of a solid fill that get an analytic-AA fringe. Null when there are none.
    private static List<(Adamantium.Mathematics.Vector2[] Points, bool IsClosed)> BuildFillContours(Geometry geometry)
    {
        var mesh = geometry?.Mesh;
        if (mesh == null || mesh.Contours.Count == 0) return null;

        List<(Adamantium.Mathematics.Vector2[] Points, bool IsClosed)> contours = [];
        foreach (var contour in mesh.Contours)
            if (contour.IsGeometryClosed && contour.Points is { Length: >= 3 })
                contours.Add((contour.Points, true));
        return contours.Count == 0 ? null : contours;
    }

    // Keep the analytic-AA fringe in sync on a hot update. A geometry change (rebuild) already re-tessellated `geometry`,
    // so rebuild the fringe from it. A brush/colour change is a CHEAP update (RequiresBufferRebuild excludes the brush):
    // the existing fringe just repoints its brush (read live at Render - no contour re-upload, and a solid<->non-solid
    // flip simply stops/starts it drawing); only a fill that never had a fringe yet became solid needs a build (it
    // tessellates on demand). No-op when nothing relevant changed.
    protected void UpdateFillFringe(Geometry geometry, Brush brush, bool rebuild, bool brushChanged)
    {
        if (rebuild)
        {
            // Same-topology resize -> rewrite the fringe contours in place (no new component / allocation); otherwise
            // (contour count or a contour's point count changed) rebuild it.
            if (brush is SolidColorBrush solid && FillFringeRenderer is GpuFillRenderComponent existingFringe)
            {
                var contours = FillBoundary.ExtractLoops(geometry?.Mesh) ?? BuildFillContours(geometry);
                if (contours != null && existingFringe.TryUpdateContours(contours, solid))
                {
                    FillFringeRenderer.RenderData = DrawCommand.RenderData;
                    return;
                }
            }
            ProcessFillFringe(geometry, brush);
            return;
        }
        if (!brushChanged) return;
        if (FillFringeRenderer is GpuFillRenderComponent fringe)
        {
            fringe.Brush = brush;
        }
        else if (UseGpuFill && FillFringeEffect != null && brush is SolidColorBrush)
        {
            geometry.ProcessGeometry(GeometryType.Both);
            ProcessFillFringe(geometry, brush);
        }
    }

    protected IResourceFactory ResourceFactory => Context.ResourceFactory;
    protected IDrawCommand DrawCommand { get; set; }
    protected IGraphicsDevice GraphicsDevice => Context.GraphicsDevice;

    public IUIComponent Component => DrawCommand.Component;

    // Overwrites the record-time opacity with the effective alpha composed from the frozen snapshot (RenderCache), so a
    // paint-only opacity change re-bakes the same commands with a new alpha - every consumer (batch FillOpacity, the
    // per-unit fill/stroke/fringe) reads RenderData.Opacity, so one write covers them all.
    public void SetEffectiveOpacity(float opacity)
    {
        if (DrawCommand?.RenderData is { } rd) rd.Opacity = opacity;
    }

    public void Update(Matrix4x4F transform, Matrix4x4F projection, double renderScale)
    {
        // Keep ALL of the unit's renderers pointed at the CURRENT RenderData. UpdateWithDrawCommand repoints the body
        // unconditionally but the fringe/stroke only on a geometry/brush/pen change - so an OPACITY-only change (a
        // fading container) left the analytic-AA fringe + stroke on their old RenderData, i.e. their old opacity: a
        // bright ~1px rim lingered around a thumb whose body had already faded out. Re-share the RenderData every frame
        // (it also carries the viewport zoom the fringe reads in PreRender for its ~1 device-px width).
        var renderData = DrawCommand?.RenderData;
        if (renderData != null)
        {
            renderData.RenderScale = renderScale;
            if (GeometryRenderer != null) GeometryRenderer.RenderData = renderData;
            if (FillFringeRenderer != null) FillFringeRenderer.RenderData = renderData;
            if (StrokeRenderer != null) StrokeRenderer.RenderData = renderData;
        }
        GeometryRenderer?.Update(transform, projection);
        FillFringeRenderer?.Update(transform, projection);
        StrokeRenderer?.Update(transform, projection);
    }

    public virtual void PreRender()
    {
        GeometryRenderer?.PreRender();
        FillFringeRenderer?.PreRender();
        StrokeRenderer?.PreRender();
    }

    public virtual void Render()
    {
        // Body first, then its analytic-AA fringe on top of the edge, then the stroke over the fill. The body is SKIPPED
        // when it went to the retained instanced renderer (FillInstanced) - its fringe/stroke still draw over the instance.
        if (!FillInstanced) GeometryRenderer?.Render();
        FillFringeRenderer?.Render();
        StrokeRenderer?.Render();
    }

    public abstract void UpdateWithDrawCommand(IDrawCommand drawCommand);
    public virtual bool Match(IDrawCommand drawCommand)
    {
        return DrawCommand.Payload.GetType() == drawCommand.Payload.GetType();
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        // The unit owns its renderers (their vertex/index buffers and text render targets), so dispose them too.
        // The unit itself is disposed via the deferred queue (after the frame fence), so disposing them now is safe.
        GeometryRenderer?.Dispose();
        FillFringeRenderer?.Dispose();
        StrokeRenderer?.Dispose();
        base.Dispose(disposeManagedResources);
    }
}

public class GeometryRenderUnit : RenderUnit<GeometryPayload>
{
    public GeometryRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        Payload.Geometry.ProcessGeometry(GeometryType.Both);
        _frozenMesh = FrozenMesh.From(Payload.Geometry.Mesh, Payload.Geometry.Bounds);   // freeze the tessellated mesh for the instanced path
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, Payload.Geometry.Mesh, Payload.Brush, BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessFillFringe(Payload.Geometry, Payload.Brush);
        ProcessStrokeData(Payload.Pen, Payload.Geometry);
    }

    // Solid arbitrary geometry is instanceable: N identical Paths share one mesh + one instanced draw. A gradient/image
    // fill (or a mesh with no vertices) falls back to the per-unit body. The instanced applier reads the FROZEN mesh
    // snapshot (captured at record after ProcessGeometry), never the live Geometry.Mesh - so it is render-thread safe.
    private FrozenMesh _frozenMesh;

    public override bool TryGetInstancedFill(out GeometryKey key, out object mesh, out Vector4F color)
    {
        key = default; mesh = null; color = default;
        if (Payload.Brush is not SolidColorBrush solid) return false;
        // Instanceable if the frozen mesh has drawable vertices - an indexed OR a non-indexed triangle list. UI shape
        // fills tessellate to a NON-indexed list; the retained renderer picks DrawIndexed vs Draw off the topology.
        if (_frozenMesh is not { HasPoints: true } fm) return false;

        key = fm.Key;
        mesh = fm;
        var c = solid.Color.ToVector4();
        // same bake as the per-unit fill: colour x brush opacity x element opacity
        c.W *= (float)(solid.Opacity * (DrawCommand?.RenderData?.Opacity ?? 1.0f));
        color = c;
        return true;
    }

    // Gradient sibling of TryGetInstancedFill: a GRADIENT fill on arbitrary geometry batches through the gradient
    // instanced-fill path. Returns the shared-mesh key (same fingerprint, so identical shapes still merge), the frozen
    // mesh, the (frozen) gradient brush, and the shape's LOCAL bounds (the shader maps a fragment's local pos to a uv).
    public bool TryGetInstancedGradientFill(out GeometryKey key, out object mesh, out GradientBrush brush,
        out Rect localBounds, out double opacity)
    {
        key = default; mesh = null; brush = null; localBounds = default; opacity = 1.0;
        if (Payload.Brush is not GradientBrush g) return false;
        if (_frozenMesh is not { HasPoints: true } fm) return false;

        key = fm.Key;
        mesh = fm;
        brush = g;
        localBounds = fm.Bounds;
        opacity = DrawCommand?.RenderData?.Opacity ?? 1.0;
        return true;
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not GeometryPayload inputPayload) return;

        // Capture the old pen/brush BEFORE reassigning Payload, otherwise the comparisons below are always equal.
        var oldPen = Payload.Pen;
        var oldBrush = Payload.Brush;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        // Geometry change: rewrite the body's buffers in place (no new component / no Vulkan allocation - the resize/
        // animation fast path). The brush is a cheap repoint, applied either way.
        if (rebuild)
        {
            inputPayload.Geometry.ProcessGeometry(GeometryType.Both);
            _frozenMesh = FrozenMesh.From(inputPayload.Geometry.Mesh, inputPayload.Geometry.Bounds);   // re-freeze the re-tessellated mesh
            ((GeometryRenderComponent)GeometryRenderer).UpdateGeometry(inputPayload.Geometry.Mesh);
        }
        ((GeometryRenderComponent)GeometryRenderer).Background = inputPayload.Brush;

        DrawCommand = drawCommand;
        Payload = inputPayload;
        if (GeometryRenderer != null)
        {
            GeometryRenderer.RenderData = DrawCommand.RenderData;
        }

        // Analytic-AA fringe follows the fill: cheap brush/colour change repoints it, a geometry change rebuilds it.
        UpdateFillFringe(inputPayload.Geometry, inputPayload.Brush, rebuild, !Equals(oldBrush, inputPayload.Brush));

        // Stroke: geometry change -> full rebuild; pen-only change -> try a cheap uniform repoint (dash offset /
        // thickness / colour / trim animation) on the existing GPU stroke, rebuilding only if the buffer sizes change.
        if (rebuild)
        {
            if (!TryUpdateStroke(inputPayload.Pen, inputPayload.Geometry))
                ProcessStrokeData(inputPayload.Pen, inputPayload.Geometry);
        }
        else if (!Equals(oldPen, inputPayload.Pen) &&
                 !(StrokeRenderer is GpuStrokeRenderComponent gs && gs.TryRepoint(inputPayload.Pen)))
        {
            ProcessStrokeData(inputPayload.Pen, inputPayload.Geometry);
        }
    }
}

public class LineRenderUnit : RenderUnit<LinePayload>
{
    public LineRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        var geometry = new LineGeometry(Payload.LineStart, Payload.LineEnd);
        geometry.ProcessGeometry(GeometryType.Both);
        ProcessStrokeData(Payload.Pen, geometry);
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not LinePayload inputPayload) return;

        var oldPayload = Payload;
        DrawCommand = drawCommand;
        Payload = inputPayload;

        // A line is pure stroke. RequiresBufferRebuild bundles endpoints AND pen, which would rebuild the buffers every
        // frame of a dash-offset animation. Split them: only an ENDPOINT move changes the ribbon geometry (rebuild);
        // a pen-only change (offset/thickness/colour/trim) tries a cheap uniform repoint, rebuilding only on a size change.
        var geometryChanged = !oldPayload.LineStart.Equals(inputPayload.LineStart)
                              || !oldPayload.LineEnd.Equals(inputPayload.LineEnd);

        if (geometryChanged)
        {
            var geometry = new LineGeometry(inputPayload.LineStart, inputPayload.LineEnd);
            geometry.ProcessGeometry(GeometryType.Both);
            if (!TryUpdateStroke(inputPayload.Pen, geometry))
                ProcessStrokeData(inputPayload.Pen, geometry);
        }
        else if (!Equals(oldPayload.Pen, inputPayload.Pen) &&
                 !(StrokeRenderer is GpuStrokeRenderComponent gs && gs.TryRepoint(inputPayload.Pen)))
        {
            var geometry = new LineGeometry(inputPayload.LineStart, inputPayload.LineEnd);
            geometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, geometry);
        }
    }
}

public class RectangleRenderUnit : RenderUnit<RectanglePayload>
{
    // The item-background batch (RectBatchCollector) reads the rect's brush/rect/corners/pen through this to bake it
    // into the instanced draw. See RectBatchEffect.fx.
    public RectanglePayload RectPayload => Payload;

    // A rect that the item-background SDF batch will draw (solid fill, no pen, uniform corners): the batch shader
    // reconstructs the rounded rect from a signed-distance field and self-anti-aliases, so this unit needs NO analytic-AA
    // FRINGE. Building one per rect is pure waste - and its per-tile GPU buffer, rebuilt on every resize, is exactly what
    // exhausted device memory (vkAllocateMemory=ErrorOutOfDeviceMemory) while dragging the size slider. Mirrors
    // RectBatchCollector.CanBatch so "no fringe built" == "the batch will draw it". Geometry-identity instancing phase 4.
    private static bool IsSdfBatchable(RectanglePayload p)
    {
        if (!RectBatchCollector.Enabled) return false;
        if (p.Brush is not (null or SolidColorBrush)) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        var hasFill = p.Brush is SolidColorBrush { Color.A: > 0 };
        var hasStroke = p.Pen is { Brush: SolidColorBrush { Color.A: > 0 } };
        if (!hasFill && !hasStroke) return false;
        var c = p.CornerRadius;
        return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft;
    }

    // A rect the GRADIENT SDF batch (GradientRectCollector) will draw: a linear/radial gradient fill, a batchable pen,
    // uniform corners. Like IsSdfBatchable it means "build ZERO per-unit machinery" - the batch's pixel shader draws the
    // gradient + self-AAs. Mirrors GradientRectCollector.CanBatch.
    private static bool IsGradientBatchable(RectanglePayload p)
    {
        if (!GradientRectCollector.Enabled) return false;
        if (p.Brush is not GradientBrush g || g.GradientStops.Count == 0) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        var c = p.CornerRadius;
        return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft;
    }

    public RectangleRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        // A batchable rect (solid, no pen, uniform corners) is drawn ENTIRELY by the item-background SDF batch, which
        // self-AAs - so build ZERO per-unit machinery: no tessellation, no geometry/fringe/stroke, no GPU buffers. This
        // is what makes a big virtualized tile grid cheap: a slider shrink that realizes hundreds of tiles no longer
        // tessellates + allocates per tile (that was the 1-fps freeze and the resize OOM). The rare rejected case
        // (rotated/sheared world, or per-frame overflow) builds its body lazily in Render via EnsureMachinery. A gradient
        // fill routes to the gradient SDF batch, also machinery-free.
        if (IsSdfBatchable(Payload) || IsGradientBatchable(Payload)) return;
        BuildMachinery(Payload);
    }

    // The per-unit fill/fringe/stroke renderers for a NON-batched rect. Tessellates once here; the buffers themselves are
    // still allocated lazily on first draw (see UIRenderComponent.SetMesh).
    private void BuildMachinery(RectanglePayload payload)
    {
        var g = new RectangleGeometry(payload.DestinationRect, payload.CornerRadius);
        g.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, g.Mesh, payload.Brush, BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessFillFringe(g, payload.Brush);
        ProcessStrokeData(payload.Pen, g);
    }

    // The item-background batch reads the fill opacity here: a batchable rect has no GeometryRenderer to carry RenderData.
    public double FillOpacity => DrawCommand?.RenderData?.Opacity ?? 1.0;

    // A batchable rect the batch REJECTED this frame (rotated/sheared world, or the instance buffer overflowed) must draw
    // itself, so build its BODY on demand. No fringe: it would need a PreRender we've already passed this frame; the body
    // self-fills its buffer in Render. Idempotent (a persistently-rejected rect keeps the body across frames).
    public void EnsureMachinery()
    {
        if (GeometryRenderer != null) return;
        var g = new RectangleGeometry(Payload.DestinationRect, Payload.CornerRadius);
        g.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, g.Mesh, Payload.Brush, BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not RectanglePayload inputPayload) return;

        // Fast path: no machinery (a batchable tile, the common case). Just repoint payload/command - NO tessellation, NO
        // buffers - so resizing a virtualized grid stays cheap. Build machinery only if the rect is now non-batchable
        // (e.g. it gained a pen or a gradient fill).
        if (GeometryRenderer == null)
        {
            DrawCommand = drawCommand;
            Payload = inputPayload;
            if (!IsSdfBatchable(inputPayload) && !IsGradientBatchable(inputPayload)) BuildMachinery(inputPayload);
            return;
        }

        // Machinery exists (a non-batchable rect, or a kept fallback body): update it incrementally.
        var oldPen = Payload.Pen;
        var oldBrush = Payload.Brush;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (rebuild)
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            ((GeometryRenderComponent)GeometryRenderer).UpdateGeometry(rectangleGeometry.Mesh);
        }
        ((GeometryRenderComponent)GeometryRenderer).Background = inputPayload.Brush;
        DrawCommand = drawCommand;
        Payload = inputPayload;
        GeometryRenderer.RenderData = DrawCommand.RenderData;

        // Fringe: skip for a batchable rect (the batch self-AAs, and a per-tile fringe rebuilt on resize is the OOM);
        // drop a stale one. Otherwise follow the fill.
        if (IsSdfBatchable(inputPayload) || IsGradientBatchable(inputPayload))
        {
            FillFringeRenderer?.DeferDispose();
            FillFringeRenderer = null;
        }
        else
        {
            UpdateFillFringe(rectangleGeometry, inputPayload.Brush, rebuild, !Equals(oldBrush, inputPayload.Brush));
        }

        // Stroke: geometry change -> full rebuild; pen-only change -> try a cheap uniform repoint (animation), else
        // rebuild (process the geometry first, since it wasn't processed in the non-rebuild branch).
        if (rebuild)
        {
            if (!TryUpdateStroke(inputPayload.Pen, rectangleGeometry))
                ProcessStrokeData(inputPayload.Pen, rectangleGeometry);
        }
        else if (!Equals(oldPen, inputPayload.Pen) &&
                 !(StrokeRenderer is GpuStrokeRenderComponent gs && gs.TryRepoint(inputPayload.Pen)))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, rectangleGeometry);
        }
    }
}

public class EllipseRenderUnit : RenderUnit<EllipsePayload>
{
    // The ellipse SDF batch (EllipseBatchCollector) reads the payload through this to bake it into the instanced draw.
    public EllipsePayload EllipsePayload => Payload;

    // The batch reads the fill opacity here: a batchable ellipse has no GeometryRenderer to carry RenderData.
    public double FillOpacity => DrawCommand?.RenderData?.Opacity ?? 1.0;

    // An ellipse the SDF batch will draw (solid fill, no pen, FULL ellipse): the batch shader reconstructs it from an
    // implicit field and self-anti-aliases, so this unit needs NO tessellated body and NO AA fringe. Building them per
    // ellipse is pure waste (and their per-unit GPU buffers are exactly what strained device memory). Mirrors
    // EllipseBatchCollector.CanBatch so "no machinery built" == "the batch will draw it".
    private static bool IsSdfBatchable(EllipsePayload p)
    {
        if (!EllipseBatchCollector.Enabled) return false;
        if (p.Brush is not SolidColorBrush s || s.Color.A == 0) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    // A full ellipse the GRADIENT ellipse SDF batch will draw (linear/radial gradient fill). Like IsSdfBatchable it means
    // "build ZERO per-unit machinery". Mirrors GradientEllipseCollector.CanBatch.
    private static bool IsGradientBatchable(EllipsePayload p)
    {
        if (!GradientEllipseCollector.Enabled) return false;
        if (p.Brush is not GradientBrush g || g.GradientStops.Count == 0) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public EllipseRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        // A batchable ellipse is drawn ENTIRELY by the SDF batch (resolution-independent, self-AA) - build ZERO per-unit
        // machinery: no tessellation, no geometry/fringe/stroke, no GPU buffers. The rare rejected case (rotated/sheared
        // world, or per-frame overflow) builds its body lazily in Render via EnsureMachinery. A gradient fill routes to
        // the gradient ellipse batch, also machinery-free.
        if (IsSdfBatchable(Payload) || IsGradientBatchable(Payload)) return;
        BuildMachinery(Payload);
    }

    // The per-unit tessellated fill/fringe/stroke for a NON-batched ellipse (a sector/arc, a stroked or gradient ellipse).
    private void BuildMachinery(EllipsePayload payload)
    {
        var g = new EllipseGeometry(payload.DestinationRect, payload.StartAngle, payload.SweepAngle, payload.EllipseType);
        g.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, g.Mesh, payload.Brush, BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessFillFringe(g, payload.Brush);
        ProcessStrokeData(payload.Pen, g);
    }

    // A batchable ellipse the batch REJECTED this frame (rotated/sheared world, or overflow) must draw itself, so build
    // its BODY on demand. No fringe (its PreRender is already past this frame); the body self-fills its buffer in Render.
    public void EnsureMachinery()
    {
        if (GeometryRenderer != null) return;
        var g = new EllipseGeometry(Payload.DestinationRect, Payload.StartAngle, Payload.SweepAngle, Payload.EllipseType);
        g.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, g.Mesh, Payload.Brush, BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not EllipsePayload inputPayload) return;

        // Fast path: no machinery (a batchable ellipse). Just repoint payload/command - NO tessellation, NO buffers.
        // Build machinery only if it is now non-batchable (gained a pen, a gradient fill, or became a sector).
        if (GeometryRenderer == null)
        {
            DrawCommand = drawCommand;
            Payload = inputPayload;
            if (!IsSdfBatchable(inputPayload) && !IsGradientBatchable(inputPayload)) BuildMachinery(inputPayload);
            return;
        }

        // Machinery exists (a non-batchable ellipse, or a kept fallback body): update it incrementally.
        var oldPen = Payload.Pen;
        var oldBrush = Payload.Brush;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        var ellipseGeometry = new EllipseGeometry(inputPayload.DestinationRect, inputPayload.StartAngle, inputPayload.SweepAngle,
            inputPayload.EllipseType);
        if (rebuild)
        {
            ellipseGeometry.ProcessGeometry(GeometryType.Both);
            ((GeometryRenderComponent)GeometryRenderer).UpdateGeometry(ellipseGeometry.Mesh);
        }
        ((GeometryRenderComponent)GeometryRenderer).Background = inputPayload.Brush;
        DrawCommand = drawCommand;
        Payload = inputPayload;
        GeometryRenderer.RenderData = drawCommand.RenderData;

        // Fringe: skip for a batchable ellipse (the batch self-AAs); drop a stale one. Otherwise follow the fill.
        if (IsSdfBatchable(inputPayload) || IsGradientBatchable(inputPayload))
        {
            FillFringeRenderer?.DeferDispose();
            FillFringeRenderer = null;
        }
        else
        {
            UpdateFillFringe(ellipseGeometry, inputPayload.Brush, rebuild, !Equals(oldBrush, inputPayload.Brush));
        }

        // Stroke: geometry change -> full rebuild; pen-only change -> try a cheap uniform repoint (animation), else
        // rebuild (process the geometry first, since it wasn't processed in the non-rebuild branch).
        if (rebuild)
        {
            if (!TryUpdateStroke(inputPayload.Pen, ellipseGeometry))
                ProcessStrokeData(inputPayload.Pen, ellipseGeometry);
        }
        else if (!Equals(oldPen, inputPayload.Pen) &&
                 !(StrokeRenderer is GpuStrokeRenderComponent gs && gs.TryRepoint(inputPayload.Pen)))
        {
            ellipseGeometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, ellipseGeometry);
        }
    }
}

public class ImageRenderUnit : RenderUnit<ImagePayload>
{
    public ImageRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        var rectangleGeometry = new RectangleGeometry(Payload.DestinationRect);
        // Generate the quad's vertices (every other render unit does this in its ctor too). Without it the mesh is
        // empty -> the component's VertexBuffer is null -> UIRenderComponent.Render early-returns and the image is
        // never drawn (this is why images never appeared while text/solid shapes did).
        rectangleGeometry.ProcessGeometry(GeometryType.Solid);
        RemapSourceUv(rectangleGeometry.Mesh, Payload.SourceUv);
        if (Payload.Image is BitmapSource bitmapImage)
        {
            // Null when the image's async decode hasn't produced pixels yet - the unit draws nothing this frame and
            // UpdateWithDrawCommand retries on the next re-render (Render() itself is null-tolerant).
            GeometryRenderer = CreateImageRenderer(rectangleGeometry.Mesh, bitmapImage);
            if (GeometryRenderer != null) GeometryRenderer.RenderData = DrawCommand.RenderData;
        }
    }

    // A mosaic tile samples just its normalised sub-rect of the shared photo: squeeze the quad's 0..1 UVs into it.
    private static void RemapSourceUv(Adamantium.Graphics.Core.Models.Mesh mesh, Rect? sourceUv)
    {
        if (sourceUv is not { } src || mesh?.UV0 == null || mesh.UV0.Length == 0) return;
        var uvs = new Vector2F[mesh.UV0.Length];
        for (var i = 0; i < uvs.Length; i++)
        {
            var uv = mesh.UV0[i];
            uvs[i] = new Vector2F((float)(src.X + uv.X * src.Width), (float)(src.Y + uv.Y * src.Height));
        }
        mesh.SetUVs(0, uvs);
    }

    // BitmapSource (not just BitmapImage) so SharedSurfaceImage / RenderTargetImage also render. A live shared
    // surface (game→panel) is sampled directly and synchronised via its Produce/Consume timeline (see
    // ImageRenderComponent.SharedSource/PreRender); a regular bitmap is sampled directly.
    private ImageRenderComponent CreateImageRenderer(Adamantium.Graphics.Core.Models.Mesh mesh, BitmapSource image)
    {
        var texture = image.GetOrCreateTexture(ResourceFactory);
        if (texture == null) return null;   // decode still pending - no texture yet, draw nothing until a re-render
        var component = new ImageRenderComponent(GraphicsDevice, UIBasicEffect, mesh, texture, BufferManager)
        {
            Sampler = GraphicsDevice.SamplerStates.LinearClampToEdge
        };
        // A live shared surface (game→panel): sample it directly, and drive the producer/consumer timeline so the
        // sample never races the producer's write (see ImageRenderComponent.PreRender). Composited with the default
        // AlphaBlend so the panel's Opacity controls translucency; the producer now publishes an opaque frame
        // (alpha=1, see RenderingService.BeginDraw) so at Opacity=1 it is fully solid.
        if (texture is Adamantium.Graphics.SharedSurface shared)
        {
            component.SharedSource = shared;
        }
        return component;
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not ImagePayload inputPayload) return;

        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            RemapSourceUv(rectangleGeometry.Mesh, inputPayload.SourceUv);
            GeometryRenderer?.DeferDispose();
            if (inputPayload.Image is BitmapSource bitmapImage)
            {
                GeometryRenderer = CreateImageRenderer(rectangleGeometry.Mesh, bitmapImage);
            }
        }
        else if (GeometryRenderer == null && inputPayload.Image is BitmapSource pendingImage)
        {
            // The unit was built while the image's async decode was still running (no texture -> no renderer).
            // The pixels are usually in by the time the tile re-renders - build the renderer now.
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            RemapSourceUv(rectangleGeometry.Mesh, inputPayload.SourceUv);
            GeometryRenderer = CreateImageRenderer(rectangleGeometry.Mesh, pendingImage);
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;

        GeometryRenderer?.RenderData = drawCommand.RenderData;
    }
}

public class TextRenderUnit : RenderUnit<TextPayload>
{
    // The text batch aggregator (RenderCache) reaches the block's TextLayout / params / foreground / FontRenderer
    // through this. Null only transiently before the component is built. See docs/TEXT_GLYPH_BATCH_PLAN.md §9.
    public TextRenderComponent TextComponent => GeometryRenderer as TextRenderComponent;

    public TextRenderUnit(IDrawCommand command, RenderUnitContext context) : base(command, context)
    {
        Payload.TextLayout.Update(GraphicsDevice);
        // Pad the text quad/RT so glyph effects (outline/glow) that reach beyond the body aren't clipped at
        // the block edges. The body stays put: the rect is grown symmetrically (origin shifted by -pad), so
        // its centre - and thus mesh-local (0,0) where the text is anchored - is unchanged.
        var pad = Payload.TextLayout.EffectPadding;
        var ds = Payload.DesiredSize;
        var rectangleGeometry = new RectangleGeometry(new Rect(-pad, -pad, ds.Width + 2 * pad, ds.Height + 2 * pad));
        rectangleGeometry.ProcessGeometry(GeometryType.Solid);
        GeometryRenderer = new TextRenderComponent(GraphicsDevice,
            UIBasicEffect,
            rectangleGeometry.Mesh,
            ResourceFactory.GetFontRenderer(GraphicsDevice), 
            Payload.TextLayout,
            Payload.TextRenderingParameters, 
            Payload.Background,
            Payload.Foreground,
            Payload.Stroke,
            BufferManager);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        TextComponent.GlyphRun = Payload.TextLayout.SnapshotGlyphs();   // freeze the shaped glyphs for the render-thread batch bake
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not TextPayload inputPayload) return;
        
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            inputPayload.TextLayout.Update(GraphicsDevice);   // re-upload the (re-shaped) glyph buffer either way

            // Same size, same params, same (re-shaped-in-place) layout instance: only the text content changed, so the
            // render target is the same size - reuse it and just re-rasterize, instead of disposing the component and
            // allocating a fresh render target. The live-text (counters/clocks) + recycled-list-row fast path; the RT
            // allocation it skips is the dominant per-change cost. Mirrors the colour-only UpdateColors path below.
            if (GeometryRenderer is TextRenderComponent reuse
                && Payload.DesiredSize == inputPayload.DesiredSize
                && Payload.TextLayout == inputPayload.TextLayout
                && Equals(Payload.TextRenderingParameters, inputPayload.TextRenderingParameters))
            {
                reuse.UpdateText(inputPayload.Background, inputPayload.Foreground, inputPayload.Stroke);
            }
            else
            {
                var pad = inputPayload.TextLayout.EffectPadding;
                var ds = inputPayload.DesiredSize;
                var rectangleGeometry = new RectangleGeometry(new Rect(-pad, -pad, ds.Width + 2 * pad, ds.Height + 2 * pad));
                rectangleGeometry.ProcessGeometry(GeometryType.Both);
                GeometryRenderer?.DeferDispose();
                GeometryRenderer = new TextRenderComponent(GraphicsDevice,
                    UIBasicEffect,
                    rectangleGeometry.Mesh,
                    ResourceFactory.GetFontRenderer(GraphicsDevice),
                    inputPayload.TextLayout,
                    inputPayload.TextRenderingParameters,
                    inputPayload.Background,
                    inputPayload.Foreground,
                    inputPayload.Stroke,
                    BufferManager);
            }
            // Glyphs were re-shaped in place (reuse) or a new component was built - refresh the frozen snapshot either way.
            TextComponent.GlyphRun = inputPayload.TextLayout.SnapshotGlyphs();
        }
        else if (!Equals(Payload.Background, inputPayload.Background) ||
                 !Equals(Payload.Foreground, inputPayload.Foreground) ||
                 !Equals(Payload.Stroke, inputPayload.Stroke))
        {
            // Geometry/layout unchanged - only the colours differ. Swap brushes and force a re-raster,
            // reusing the existing render target (RequiresBufferRebuild ignores colours, so without this
            // a colour-only change would never repaint). Compared against the old Payload, before reassign.
            ((TextRenderComponent)GeometryRenderer).UpdateColors(
                inputPayload.Background, inputPayload.Foreground, inputPayload.Stroke);
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;
        GeometryRenderer.RenderData = drawCommand.RenderData;
    }
}