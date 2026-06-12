using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Rendering;

public class RenderCache
{
    private readonly List<DrawCommand> _commands = new();
    private readonly List<IRenderUnit> _renderUnits = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;
    private readonly Dictionary<Guid, List<IRenderUnit>> _unitsByControl = new Dictionary<Guid, List<IRenderUnit>>();

    private readonly IRenderUnitFactory _renderUnitFactory;

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

    public void ProcessCommands(Matrix4x4F projectionMatrix)
    {
        foreach (var unit in _renderUnits)
        {
            var transform = unit.Component.WorldTransform;
            unit.Update(transform, projectionMatrix);
        }
    }
    
    public void Render()
    {
        foreach (var unit in _renderUnits)
        {
            unit.Render();
        }
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