using System;
using Adamantium.Multiverse;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Universes;

/// <summary>
/// Outputs over the UI's surfaces: a window, or a <see cref="RenderTargetPanel"/> inside one.
/// </summary>
public class UIOutputFactory : IOutputFactory
{
    public UniverseOutput Create(OutputContext context, IUniverseEventAggregator events)
    {
        if (context.Context is IWindow)
        {
            return new WindowUniverseOutput(events, context);
        }

        if (context.Context is RenderTargetPanel)
        {
            return new RenderTargetUniverseOutput(events, context);
        }

        throw new NotSupportedException($"context of type {context.Context?.GetType()} is not supported");
    }

    public UniverseOutput Create(OutputContext context, IUniverseEventAggregator events, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel)
    {
        if (context.Context is RenderTargetPanel)
        {
            return new RenderTargetUniverseOutput(events, context, pixelFormat, depthFormat, msaaLevel);
        }

        throw new NotSupportedException($"context of type {context.Context?.GetType()} is not supported");
    }
}
