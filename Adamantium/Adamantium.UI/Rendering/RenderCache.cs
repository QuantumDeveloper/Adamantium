using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public class RenderCache
{
    private readonly List<DrawCommand> _commands = new();
    private readonly List<IRenderUnit> _renderUnits = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;
    private readonly Dictionary<Guid, List<IRenderUnit>> _unitsByControl = new Dictionary<Guid, List<IRenderUnit>>();

    private readonly IRenderUnitFactory _renderUnitFactory;

    // Last render scale seen in ProcessCommands; maps a unit's window-logical clip rect to framebuffer-pixel scissor.
    private double _renderScale = 1.0;

    public RenderCache(IDrawingContext context, IRenderUnitFactory renderUnitFactory)
    {
        _drawingContext = context;
        _drawingContextInternal = (IDrawingContextInternal)context;
        _renderUnitFactory = renderUnitFactory;
    }
    
    public void BuildFromVisualTree(IRootVisualComponent visualRoot)
    {
        _commands.Clear();
        BuildRenderCommands(visualRoot);
    }

    /// <summary>
    /// Builds units from a FLAT list of components (the adorner stage), instead of walking a visual tree. Each
    /// component renders and is cached by RenderId exactly as in the tree build; units of components no longer in the
    /// list (e.g. a deselected adorner) are disposed. Used for overlays that aren't part of the content tree.
    /// </summary>
    public void BuildFromComponents(IReadOnlyList<IUIComponent> components, Matrix4x4F projectionMatrix)
    {
        _commands.Clear();
        _renderUnits.Clear();

        var present = new HashSet<Guid>();
        if (components != null)
        {
            foreach (var component in components)
            {
                if (component.Visibility != Visibility.Visible) continue;
                present.Add(component.RenderId);

                var wasGeometryValid = component.IsGeometryValid;
                _drawingContextInternal.Clear();
                component.Render(_drawingContext);
                ProcessRenderCommands(component, projectionMatrix, wasGeometryValid);
            }
        }

        // Free the units of any component dropped from the list since the last build.
        List<Guid> stale = null;
        foreach (var id in _unitsByControl.Keys)
            if (!present.Contains(id)) (stale ??= new List<Guid>()).Add(id);
        if (stale != null)
            foreach (var id in stale) RemoveAndDeferDispose(id);
    }

    /// <summary>
    /// Immediately disposes every cached render unit and empties the cache. The caller must ensure the GPU is
    /// idle first (e.g. after a DeviceWaitIdle). Used by the off-screen designer, which builds a brand-new tree
    /// each render: those controls never detach (each owns its own root window), so the attachment-based
    /// reconciliation can't reclaim them - the designer resets the cache between renders instead.
    /// </summary>
    public void DisposeUnits()
    {
        foreach (var units in _unitsByControl.Values)
        {
            foreach (var unit in units)
                unit?.Dispose();
        }

        _unitsByControl.Clear();
        _renderUnits.Clear();
    }

    public void ProcessCommands(Matrix4x4F projectionMatrix, double renderScale)
    {
        _renderScale = renderScale;
        foreach (var unit in _renderUnits)
        {
            var transform = unit.Component.WorldTransform;
            unit.Update(transform, projectionMatrix, renderScale);
        }
    }
    
    /// <summary>Out-of-render-pass pass: recorded before BeginRendering (shared-surface latch copies).</summary>
    public void PreRender()
    {
        foreach (var unit in _renderUnits)
        {
            unit.PreRender();
        }
    }

    /// <summary>Renders every cached unit with no scissor management. Used by GPU-free tests (no device).</summary>
    public void Render() => Render(null, default);

    /// <summary>
    /// Renders every cached unit, narrowing the Vulkan scissor per unit so a unit whose owner sits inside one or more
    /// <see cref="IUIComponent.ClipToBounds"/> ancestors (a scroll viewport, a clipped panel, a content transition) is
    /// clipped to the intersection of those ancestors' bounds. <paramref name="fullScissor"/> is the window-wide
    /// scissor restored for unclipped units.
    /// </summary>
    public void Render(IGraphicsDevice device, Rect2D fullScissor)
    {
        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor
        foreach (var unit in _renderUnits)
        {
            if (device != null)
            {
                var scissor = ResolveScissor(unit.Component, fullScissor, out var clipped);
                if (clipped)
                {
                    device.SetScissors(scissor);
                    scissorNarrowed = true;
                }
                else if (scissorNarrowed)
                {
                    // First unclipped unit after a clipped one: restore the full window scissor.
                    device.SetScissors(fullScissor);
                    scissorNarrowed = false;
                }
            }
            unit.Render();
        }

        // Leave the device on the full scissor for whatever renders next (e.g. the adorner pass).
        if (scissorNarrowed) device.SetScissors(fullScissor);
    }

    // The scissor for a unit: the intersection of every ancestor viewport that ClipToBounds (in framebuffer pixels),
    // or fullScissor if none clip. `clipped` is false in the latter case so the caller keeps the window scissor.
    private Rect2D ResolveScissor(IUIComponent component, Rect2D fullScissor, out bool clipped)
    {
        Rect? clip = null;
        for (var c = component; c != null; c = c.VisualParent)
        {
            if (!c.ClipToBounds) continue;
            // The element's own viewport (local 0,0..RenderSize) mapped into window-logical space by its WorldTransform.
            var rect = new Rect(0, 0, c.RenderSize.Width, c.RenderSize.Height).TransformToAABB(c.WorldTransform);
            clip = clip is { } existing ? existing.Intersect(rect) : rect;
        }

        if (clip is not { } logical)
        {
            clipped = false;
            return fullScissor;
        }

        clipped = true;
        return ToFramebufferScissor(logical, fullScissor);
    }

    // Window-logical rect -> Vulkan scissor in framebuffer pixels (logical x RenderScale), clamped to the window
    // scissor so it never exceeds the attachment and collapses to empty (nothing drawn) when fully scrolled out.
    private Rect2D ToFramebufferScissor(Rect logical, Rect2D fullScissor)
    {
        var fbLeft = fullScissor.Offset.X;
        var fbTop = fullScissor.Offset.Y;
        var fbRight = fbLeft + (int)fullScissor.Extent.Width;
        var fbBottom = fbTop + (int)fullScissor.Extent.Height;

        var left = Math.Clamp((int)Math.Floor(logical.X * _renderScale), fbLeft, fbRight);
        var top = Math.Clamp((int)Math.Floor(logical.Y * _renderScale), fbTop, fbBottom);
        var right = Math.Clamp((int)Math.Ceiling(logical.Right * _renderScale), fbLeft, fbRight);
        var bottom = Math.Clamp((int)Math.Ceiling(logical.Bottom * _renderScale), fbTop, fbBottom);

        return new Rect2D
        {
            Offset = new Offset2D { X = left, Y = top },
            Extent = new Extent2D { Width = (uint)Math.Max(0, right - left), Height = (uint)Math.Max(0, bottom - top) }
        };
    }
    
    private void BuildRenderCommands(IRootVisualComponent visualRoot)
    {
        _renderUnits.Clear();
        var projectionMatrix = visualRoot.GetProjectionMatrix();
        var stack = new Stack<IUIComponent>();
        stack.Push(visualRoot);
        while (stack.Count > 0)
        {
            var component = stack.Pop();

            if (component.Visibility != Visibility.Visible) continue;

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op (records nothing),
            // so an empty command list means "reuse the cached units". A dirty control re-records, so an
            // empty list then means "this control now draws nothing" and its stale units must be cleared.
            var wasGeometryValid = component.IsGeometryValid;

            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            ProcessRenderCommands(component, projectionMatrix, wasGeometryValid);

            foreach (var uiComponent in component.VisualChildren.Reverse())
            {
                stack.Push(uiComponent);
            }
        }

        ReconcileDetachedControls();
    }

    private void ProcessRenderCommands(IUIComponent component, Matrix4x4F projectionMatrix, bool wasGeometryValid)
    {
        var drawCommands = _drawingContextInternal.GetDrawCommands();
        if (drawCommands.Count > 0)
        {
            bool isNewControl = false;
            if (!_unitsByControl.TryGetValue(component.RenderId, out var units))
            {
                units = new List<IRenderUnit>();
                _unitsByControl[component.RenderId] = units;
                isNewControl = true;
            }

            for (int i = 0; i < drawCommands.Count; i++)
            {
                var command = drawCommands[i];
                command.RenderData.ProjectionMatrix = projectionMatrix;
                if (i >= units.Count || isNewControl)
                {
                    var unit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                    units.Add(unit);
                }
                else
                {
                    var unit = units[i];
                    if (unit.Match(command))
                    {
                        unit.UpdateWithDrawCommand(command);
                    }
                    else
                    {
                        unit.DeferDispose();
                        unit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                        units[i] = unit;
                    }
                }
            }

            // Remove extra units
            if (units.Count > drawCommands.Count)
            {
                for (int i = drawCommands.Count; i < units.Count; i++)
                    units[i].DeferDispose();

                units.RemoveRange(drawCommands.Count, units.Count - drawCommands.Count);
            }

            _unitsByControl[component.RenderId] = units;
            _renderUnits.AddRange(units);
        }
        else
        {
            // No commands this frame. Distinguish the two cases (see wasGeometryValid in BuildRenderCommands):
            //  - was clean: Render() didn't re-record -> reuse the cached units.
            //  - was dirty: the control re-rendered to nothing -> clear its stale units so they stop drawing.
            if (_unitsByControl.TryGetValue(component.RenderId, out var units))
            {
                if (wasGeometryValid)
                {
                    _renderUnits.AddRange(units);
                }
                else
                {
                    RemoveAndDeferDispose(component.RenderId);
                }
            }
        }
    }

    /// <summary>
    /// Frees the cached units of any control no longer attached to the visual tree. Must run during the build
    /// (EndDraw): disposal is deferred on the current frame slot and drained M frames later, so calling it earlier
    /// (e.g. from the detach event during Update) would dispose a unit still in flight. The root visual is kept
    /// even though it is always rendered - it reports attached via its own <see cref="IUIComponent.RootVisual"/>.
    /// Attachment, not visibility, is the keep signal, so hidden-but-attached controls retain their resources.
    /// </summary>
    private void ReconcileDetachedControls()
    {
        List<Guid> detached = null;
        foreach (var pair in _unitsByControl)
        {
            var units = pair.Value;
            if (units.Count == 0
                || units[0].Component.IsAttachedToVisualTree) continue;
            (detached ??= new List<Guid>()).Add(pair.Key);
        }

        if (detached == null) return;
        foreach (var id in detached)
            RemoveAndDeferDispose(id);
    }

    /// <summary>
    /// Drops the cache entry and defer-disposes its units (deferred until the frame fence signals, as the GPU may
    /// still be using them). Build-phase only (EndDraw). Idempotent.
    /// </summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_unitsByControl.Remove(renderId, out var units)) return;

        foreach (var unit in units)
            unit?.DeferDispose();
    }
}