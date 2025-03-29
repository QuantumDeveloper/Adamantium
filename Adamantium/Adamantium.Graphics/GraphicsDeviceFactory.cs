using Adamantium.Graphics.Core;

namespace Adamantium.Graphics;

public class GraphicsDeviceFactory : IGraphicsDeviceFactory
{
    public IGraphicsDevice Create(MainGraphicsDevice mainDevice, GraphicsDeviceType deviceType)
    {
        return GraphicsDevice.Create(mainDevice, deviceType);
    }
}