using Adamantium.Graphics.Core;
using Adamantium.Imaging;

namespace Adamantium.Game.Core;

/// <summary>
/// Makes outputs over the surfaces of whoever hosts the universe - a window, a panel. The host registers it.
/// </summary>
public interface IOutputFactory
{
    /// <summary>
    /// Makes an output drawing into <paramref name="context"/>; throws when the host has none for that kind of surface.
    /// </summary>
    UniverseOutput Create(OutputContext context);

    /// <summary>
    /// Makes an output drawing into <paramref name="context"/> with the given formats.
    /// </summary>
    UniverseOutput Create(OutputContext context, SurfaceFormat pixelFormat, DepthFormat depthFormat, MSAALevel msaaLevel);
}
