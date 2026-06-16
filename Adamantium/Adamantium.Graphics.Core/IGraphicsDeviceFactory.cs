using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core;

public interface IGraphicsDeviceFactory
{
    IGraphicsDevice Create(MainGraphicsDevice mainDevice, GraphicsDeviceType deviceType);

    // Bridges Graphics.Core -> Graphics: lets MainGraphicsDevice (Core) own the heap without referencing the impl.
    IDescriptorHeapManager CreateDescriptorHeapManager(IGraphicsDevice device);
}