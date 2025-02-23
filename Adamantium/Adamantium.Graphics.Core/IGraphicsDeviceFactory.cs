using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core;

public interface IGraphicsDeviceFactory
{
    IGraphicsDevice Create(MainGraphicsDevice mainDevice);

    IGraphicsDevice Create(MainGraphicsDevice mainDevice, PresentationParameters parameters);
}