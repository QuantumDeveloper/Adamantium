using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics;

public class GraphicsDeviceFactory : IGraphicsDeviceFactory
{
    public IGraphicsDevice Create(MainGraphicsDevice mainGraphicsDevice)
    {
        return GraphicsDevice.Create(mainGraphicsDevice);
    }

    public IGraphicsDevice Create(MainGraphicsDevice mainDevice, PresentationParameters parameters)
    {
        return GraphicsDevice.Create(mainDevice, parameters);
    }
}