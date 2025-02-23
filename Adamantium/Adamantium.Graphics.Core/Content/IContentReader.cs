using System.Threading.Tasks;

namespace Adamantium.Graphics.Core.Content
{
   public interface IContentReader
   {
      Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters);
   }
}
