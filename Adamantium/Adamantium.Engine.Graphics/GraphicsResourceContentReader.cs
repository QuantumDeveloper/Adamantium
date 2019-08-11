using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;

namespace Adamantium.Engine.Graphics
{
    public abstract class GraphicsResourceContentReader<T> : IContentReader
    {
        public async Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters)
        {
            var service = contentManager.ServiceProvider.Get<IGraphicsDeviceService>();
            if (service == null)
                throw new InvalidOperationException("Unable to retrieve a IGraphicsDeviceService service provider");

            if (service.GraphicsDevice == null)
                throw new InvalidOperationException("GraphicsDevice is not initialized");

            return await ReadContentAsync(contentManager, service.GraphicsDevice, parameters);
        }

        protected abstract Task<T> ReadContentAsync(IContentManager contentManager, GraphicsDevice graphicsDevice, ContentReaderParameters parameters);
    }
}
