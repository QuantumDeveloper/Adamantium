using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Core.Effects;

namespace Adamantium.Engine.Graphics
{
   public class EffectContentReader:GraphicsResourceContentReader<Effect>
   {
      protected override Task<Effect> ReadContentAsync(IContentManager readerManager, GraphicsDevice graphicsDevice,
         ContentReaderParameters parameters)
      {
         Effect effect = null;
         if (parameters.AssetPath.EndsWith(EffectData.CompiledExtension))
         {
            effect = Effect.Load(parameters.AssetPath, graphicsDevice);
         }
         return Task.FromResult(effect);
      }
   }
}
