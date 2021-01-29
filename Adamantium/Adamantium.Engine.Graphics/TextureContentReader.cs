using System;
using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;

namespace Adamantium.Engine.Graphics
{
    public class TextureContentReader : GraphicsResourceContentReader<Texture>
    {
        protected override Task<Texture> ReadContentAsync(
            IContentManager readerManager,
            GraphicsDevice graphicsDevice,
            ContentReaderParameters parameters)
        {
            try
            {
                var texture = Texture.Load(graphicsDevice, parameters.AssetPath);
                if (texture != null)
                {
                    texture.Name = parameters.AssetName;
                }
                return Task.FromResult(texture);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
    }
}