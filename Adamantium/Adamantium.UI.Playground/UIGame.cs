using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Graphics;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;

namespace Adamantium.UI.Playground;

public class UIGame : Game.Game
{
    public UIGame(GameMode mode, bool enableDynamicRendering, IGraphicsDeviceService service, IDependencyContainer container = null) : base(mode, enableDynamicRendering, service, container)
    {
        
    }
    
    protected override void Initialize()
    {
        base.Initialize();
        InitializeGameResources();
        EventAggregator.GetEvent<GameOutputCreatedEvent>().Subscribe(OnGameOutputCreated);
    }

    private void OnGameOutputCreated(GameOutput obj)
    {
        CreateRenderService<RenderingService>(obj);
        EntityWorld.ForceUpdate();
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        //LoadModels();
    }

    private void InitializeGameResources()
    {
        try
        {
            EntityWorld.CreateService<InputService>(EntityWorld);
            EntityWorld.CreateService<TransformService>(EntityWorld);
            EntityWorld.ForceUpdate();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}