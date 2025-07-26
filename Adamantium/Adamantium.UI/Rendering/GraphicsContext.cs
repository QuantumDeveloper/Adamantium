using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Rendering;

public class GraphicsContext : IGraphicsContext
{
    private IGraphicsDeviceService _graphicsDeviceService;
    private IResourceFactory _resourceFactory;
    
    public GraphicsContext(IGraphicsDeviceService graphicsDeviceService, IResourceFactory resourceFactory)
    {
        _graphicsDeviceService = graphicsDeviceService;
        _resourceFactory = resourceFactory;
    }
    
    public IGraphicsDevice CreateGraphicsDevice()
    {
        return _graphicsDeviceService.MainGraphicsDevice.CreateRenderDevice();
    }

    public IResourceFactory GetResourceFactory()
    {
        return _resourceFactory;
    }
}