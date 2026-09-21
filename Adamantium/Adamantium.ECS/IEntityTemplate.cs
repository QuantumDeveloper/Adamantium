
using System.Threading.Tasks;

namespace Adamantium.ECS.Templates
{
    public interface IEntityTemplate
    {
       Task<Entity> BuildEntity(Entity owner);
    }
}
