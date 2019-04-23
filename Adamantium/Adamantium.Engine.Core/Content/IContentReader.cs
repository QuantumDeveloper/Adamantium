using System.Threading.Tasks;

namespace Adamantium.Engine.Core.Content
{
   public interface IContentReader
   {
      Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters);
   }
}
