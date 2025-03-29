using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core;

public interface IGraphicsDeviceFactory
{
    IGraphicsDevice Create(MainGraphicsDevice mainDevice, GraphicsDeviceType deviceType);
}