using Adamantium.EntityFramework;

namespace Adamantium.Game;

public class UIApplicationService : EntityService
{
    public UIApplicationService(EntityWorld world) : base(world)
    {
    }

    public override bool IsUpdateService => true;
    public override bool IsRenderingService => true;
}