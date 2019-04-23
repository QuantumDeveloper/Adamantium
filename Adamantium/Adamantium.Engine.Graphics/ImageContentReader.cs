using System.Threading.Tasks;
using Adamantium.Engine.Core.Content;

namespace Adamantium.Engine.Graphics
{
   public class ImageContentReader:IContentReader
   {
      public Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters)
      {
         var image = Image.Load(parameters.AssetPath);
         if (image != null)
         {
            image.Name = parameters.AssetName;
         }
         return Task.FromResult((object)image);
      }
   }
}
