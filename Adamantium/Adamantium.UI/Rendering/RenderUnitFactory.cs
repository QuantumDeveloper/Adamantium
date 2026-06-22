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
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IResourceFactory _resourceFactory;
    private UIBasicEffect _uiBasicEffect;
    private StrokeEffect _strokeEffect;

    public RenderUnitFactory(IGraphicsDevice graphicsDevice, IResourceFactory resourceFactory)
    {
        _graphicsDevice = graphicsDevice;
        _resourceFactory = resourceFactory;
        _uiBasicEffect = new UIBasicEffect(_graphicsDevice);
        _strokeEffect = new StrokeEffect(_graphicsDevice);
        _registeredFactories = new Dictionary<Type, Func<IDrawCommand, IRenderUnit>>();
        RegisterFactory<RectanglePayload>(command => new RectangleRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
        RegisterFactory<EllipsePayload>(command => new EllipseRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
        RegisterFactory<LinePayload>(command => new LineRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
        RegisterFactory<ImagePayload>(command => new ImageRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
        RegisterFactory<GeometryPayload>(command => new GeometryRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
        RegisterFactory<TextPayload>(command => new TextRenderUnit(command, _graphicsDevice, _uiBasicEffect, _strokeEffect, _resourceFactory));
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