using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// The immutable-once-published handoff from the RECORDER (device-free: walks the visual tree, calls
/// <c>component.Render</c> to produce frozen draw commands, classifies the frame) to the APPLIER (owns the GPU:
/// realizes units, bakes/upload batches, submits, presents). It is a DELTA, not the whole scene - a Clean frame
/// carries nothing (the applier replays its retained op-stream), a Partial carries only the dirty components, a Full
/// carries the whole paint-order sequence. This preserves the O(dirty) retained-cache win. The retained GPU cache
/// itself (units/batches/transform table) stays SINGLE and applier-resident - only this packet crosses the seam.
/// Phase 3 of docs/RENDER_THREAD_PLAN.md; in 3.0 the recorder and applier still run inline on one thread, with the
/// packet as the explicit seam (so 3.2 can move the applier to its own thread + double-buffer the packet).
/// </summary>
internal sealed class RenderPacket
{
    /// <summary>Clean = replay retained ops; Partial = re-render only <see cref="Draws"/>; Full = rebuild from the
    /// whole paint-order sequence in <see cref="Draws"/>.</summary>
    public RenderBuildKind Kind;

    /// <summary>Full: every visited (visible, attached) component in PAINT ORDER, each with its recorded draw commands.
    /// Partial: only the geometry-dirty components. Each entry's <see cref="ComponentDraw.Commands"/> is a COPY of the
    /// component's <c>component.Render</c> output (the shared drawing context is reused per component, so the recorder
    /// must snapshot each component's commands before rendering the next).</summary>
    public readonly List<ComponentDraw> Draws = new();

    /// <summary>Motion nodes that moved this frame (the O(1)-scroll fast path rewrites their transform-table matrices).</summary>
    public readonly List<IUIComponent> MovedNodes = new();

    /// <summary>Partial only: whether the dirty set was a MOVE (positions changed) vs a geometry-only change.</summary>
    public bool IsTransformDirty;

    /// <summary>Frame projection captured at record (from the root visual).</summary>
    public Matrix4x4F ProjectionMatrix;

    /// <summary>Reset for reuse (the packet is pooled per cache; a Clean frame produces an empty one).</summary>
    public void Reset(RenderBuildKind kind)
    {
        Kind = kind;
        Draws.Clear();
        MovedNodes.Clear();
        IsTransformDirty = false;
    }
}

/// <summary>One component's recorded contribution: its identity + the draw commands its <c>Render</c> produced this
/// frame (empty = "reuse cached units" when it was clean, or "clear stale units" when it was dirty - disambiguated by
/// <see cref="WasGeometryValid"/>, exactly as the fused walk did).</summary>
internal readonly struct ComponentDraw(IUIComponent component, IReadOnlyList<IDrawCommand> commands, bool wasGeometryValid)
{
    public IUIComponent Component { get; } = component;
    public IReadOnlyList<IDrawCommand> Commands { get; } = commands;
    public bool WasGeometryValid { get; } = wasGeometryValid;
}
