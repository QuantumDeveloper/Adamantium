using System;
using Adamantium.Core;
using Adamantium.Engine.Managers;
using Adamantium.ECS;
using Serilog;

namespace Adamantium.Engine.EntityServices;

/// <summary>
/// Polls the editing tools - gizmos, picking, the grid - once per frame. Only an editor creates it; a game has no tools.
/// </summary>
public class ToolsService : EntityService
{
    private ToolsManager tools;
    private Observatory observatory;

    public ToolsService(EntityWorld world)
        : base(world)
    {
        Priority = 1;
    }

    public override bool IsUpdateService => true;
    public override bool IsRenderingService => false;
    public override EntityServiceType ServiceType => EntityServiceType.Update;

    public override void Initialize()
    {
        tools = EntityWorld.Satellites.Get<ToolsManager>();
        observatory = EntityWorld.Satellites.Get<Observatory>();
    }

    public override void Update(AppTime gameTime)
    {
        try
        {
            tools.Update(Entities, observatory);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Tools update failed");
        }
    }
}
