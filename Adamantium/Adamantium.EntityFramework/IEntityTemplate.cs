
using System.Threading.Tasks;

namespace Adamantium.EntityFramework.Templates
{
    public interface IEntityTemplate
    {
       Task<Entity> BuildEntity(Entity owner);
    }
}
