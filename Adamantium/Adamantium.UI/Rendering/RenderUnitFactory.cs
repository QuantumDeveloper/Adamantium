using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.UI.Rendering.RenderUnits;

namespace Adamantium.UI.Rendering;

public class RenderUnitFactory : IRenderUnitFactory
{
    private Dictionary<Type, Func<IDrawCommand, IRenderUnit>> _registeredFactories;
    private readonly RenderUnitContext _context;

    public RenderUnitFactory(IGraphicsDevice graphicsDevice, IResourceFactory resourceFactory)
    {
        // The shared services every unit needs, in one object. One buffer manager shared by every unit this factory
        // builds, so geometry buffers are reused across frames (and, later, across units) instead of being reallocated
        // on each resize/animation frame. A new shared dependency goes into RenderUnitContext, not every constructor.
        _context = new RenderUnitContext(
            graphicsDevice,
            resourceFactory,
            new UIBasicEffect(graphicsDevice),
            new StrokeEffect(graphicsDevice),
            new FillFringeEffect(graphicsDevice),
            new GpuBufferManager(graphicsDevice));
        _registeredFactories = new Dictionary<Type, Func<IDrawCommand, IRenderUnit>>();
        RegisterFactory<RectanglePayload>(command => new RectangleRenderUnit(command, _context));
        RegisterFactory<EllipsePayload>(command => new EllipseRenderUnit(command, _context));
        RegisterFactory<LinePayload>(command => new LineRenderUnit(command, _context));
        RegisterFactory<ImagePayload>(command => new ImageRenderUnit(command, _context));
        RegisterFactory<GeometryPayload>(command => new GeometryRenderUnit(command, _context));
        RegisterFactory<TextPayload>(command => new TextRenderUnit(command, _context));
    }

    public void RegisterFactory<T>(Func<IDrawCommand, IRenderUnit> factory)
    {
        _registeredFactories[typeof(T)] = factory;
    }

    public IRenderUnit CreateRenderUnitFromCommand(IDrawCommand command)
    {
        if (_registeredFactories.TryGetValue(command.Payload.GetType(), out var factory))
        {
            return factory(command);
        }

        return null;
    }
}