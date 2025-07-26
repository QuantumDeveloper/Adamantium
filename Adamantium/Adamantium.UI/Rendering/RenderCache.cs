using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Rendering;

public class RenderCache
{
    private readonly Dictionary<Guid, List<DrawCommand>> _drawCommands = new Dictionary<Guid, List<DrawCommand>>();
    private readonly List<DrawCommand> _commands = new();
    private readonly List<IRenderUnit> _renderUnits = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;
    private readonly Dictionary<Guid, List<IRenderUnit>> _unitsByControl = new Dictionary<Guid, List<IRenderUnit>>();
    
    private readonly IRenderUnitFactory _renderUnitFactory;

    public RenderCache(IDrawingContext context, IRenderUnitFactory renderUnitFactory)
    {
        _drawingContext = context;
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
            unit.Update(projectionMatrix);
            unit.Render();
        }
    }
    
    private void BuildRenderCommands(IRootVisualComponent visualRoot)
    {
        _renderUnits.Clear();
        var projectionMatrix = visualRoot.GetProjectionMatrix();
        var queue = new Queue<IUIComponent>();
        queue.Enqueue(visualRoot);
        while (queue.Count > 0)
        {
            var component = queue.Dequeue();
                
            if (component.Visibility != Visibility.Visible) return;

            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            var drawCommands = _drawingContextInternal.GetDrawCommands();
            if (drawCommands.Any())
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
                    if (i > units.Count || isNewControl)
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
                            unit.Dispose();
                            unit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                            units[i] = unit;
                        }
                    }
                }
                
                // Remove extra units
                if (units.Count > drawCommands.Count)
                {
                    for (int i = drawCommands.Count; i < units.Count; i++)
                        units[i].Dispose();

                    units.RemoveRange(drawCommands.Count, units.Count - drawCommands.Count);
                }
                
                _unitsByControl[component.RenderId] = units;
                _renderUnits.AddRange(units);
            }
            else
            {
                if (_unitsByControl.TryGetValue(component.RenderId, out var units))
                {
                    _renderUnits.AddRange(units);
                }
            }

            foreach (var visual in component.VisualChildren)
            {
                queue.Enqueue(visual);
            }
        }
    }

    private void OnComponentDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        e.Component.DetachedFromVisualTreeEvent -= OnComponentDetachedFromVisualTree;
        var units = _unitsByControl[e.Component.RenderId];
        DisposeRenderUnits(units);

        _drawCommands.Remove(e.Component.RenderId);
    }

    private void DisposeRenderUnits(List<IRenderUnit> units)
    {
        foreach (var unit in units)
        {
            unit?.Dispose();
        }
    }
}