using System;
using Adamantium.Core;
using Adamantium.Engine.Managers;
using Adamantium.ECS;
using Adamantium.Game.Core;
using Serilog;

namespace Adamantium.Engine.EntityServices;

/// <summary>
/// Polls the editing tools - gizmos, picking, the grid - once per frame. Only an editor creates it; a game has no tools.
/// </summary>
public class ToolsService : EntityService
{
    private ToolsManager tools;
    private LightManager lightManager;
    private CameraManager cameraManager;
    private IGame game;

    public ToolsService(EntityWorld world)
        : base(world)
    {
        // After TransformService: the tools pick against this frame's transforms, not the last one's.
        Priority = 1;
    }

    public override bool IsUpdateService => true;
    public override bool IsRenderingService => false;
    public override EntityServiceType ServiceType => EntityServiceType.Update;

    public override void Initialize()
    {
        tools = EntityWorld.DependencyResolver.Resolve<ToolsManager>();
        lightManager = EntityWorld.DependencyResolver.Resolve<LightManager>();
        cameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
        game = EntityWorld.DependencyResolver.Resolve<IGame>();
    }

    public override void Update(AppTime gameTime)
    {
        try
        {
            tools.Update(Entities, cameraManager, lightManager, game.ActiveOutput?.Input);
            lightManager.Update();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Tools update failed");
        }
    }
}
