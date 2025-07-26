using Adamantium.Graphics.Core;

namespace Adamantium.UI.Core.Graphics;

public interface IGraphicsContext
{
    public IGraphicsDevice CreateGraphicsDevice();
    
    public IResourceFactory GetResourceFactory();
}