using Adamantium.Core.Events;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.Game.Core;

/// <summary>
/// Outputs over the UI's surfaces: a window, or a <see cref="RenderTargetPanel"/> inside one.
/// </summary>
public class UIOutputFactory : IOutputFactory
{
    private readonly IEventAggregator eventAggregator;

    public UIOutputFactory(IEventAggregator eventAggregator)
    {
        this.eventAggregator = eventAggregator;
    }

    public UniverseOutput Create(OutputContext context)
    {
        if (context.Context is IWindow)
        {
            return new WindowUniverseOutput(eventAggregator, context);
        }

        if (context.Context is RenderTargetPanel)
        {
            return new RenderTargetUniverseOutput(eventAggregator, context);
        }

        throw new NotSupportedException($"context of type {context.Context?.GetType()} is not supported");
    }

    public UniverseOutput Create(OutputContext context, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel)
    {
        if (context.Context is RenderTargetPanel)
        {
            return new RenderTargetUniverseOutput(eventAggregator, context, pixelFormat, depthFormat, msaaLevel);
        }

        throw new NotSupportedException($"context of type {context.Context?.GetType()} is not supported");
    }
}
