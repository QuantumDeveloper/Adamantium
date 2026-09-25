using Adamantium.Graphics.Core;
using Adamantium.Imaging;

namespace Adamantium.Game.Core;

/// <summary>
/// Makes outputs over the surfaces of whoever hosts the universe - a window, a panel. The host registers it.
/// </summary>
public interface IOutputFactory
{
    /// <summary>
    /// Makes an output drawing into <paramref name="context"/> that reports through <paramref name="events"/>, the bus of
    /// the universe it belongs to; throws when the host has none for that kind of surface.
    /// </summary>
    UniverseOutput Create(OutputContext context, IUniverseEventAggregator events);

    /// <summary>
    /// Makes an output drawing into <paramref name="context"/> with the given formats.
    /// </summary>
    UniverseOutput Create(OutputContext context, IUniverseEventAggregator events, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel);
}
