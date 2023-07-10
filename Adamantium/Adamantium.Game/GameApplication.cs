using System;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Game.Core;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Serilog;

namespace Adamantium.Game;

public abstract class GameApplication : UIApplication
    {
        public IGameService GameService { get; private set; }
        
        public GameApplication()
        {
            
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            GameService = new GameService();
            EntityWorld.ServiceManager.OnDrawStarted += ServiceManagerOnOnDrawStarted;
            EntityWorld.ServiceManager.OnDrawFinished += ServiceManagerOnOnDrawFinished;
            GameService.OnGameAdded += GameServiceOnOnGameAdded;
        }

        private void GameServiceOnOnGameAdded(IGame obj)
        {
            
        }

        private void ServiceManagerOnOnDrawStarted(IRenderService service, AppTime time)
        {
            GameService.RunGames(service, time);
        }
        
        private void ServiceManagerOnOnDrawFinished(IRenderService arg1, AppTime arg2)
        {
            GameService.WaitForGames();
            GameService.CopyOutput(arg1.GraphicsDevice);
        }

        protected override void RegisterServices(IContainerRegistry containerRegistry)
        {
            base.RegisterServices(containerRegistry);
            containerRegistry.RegisterSingleton<IGameService>(GameService);
        }

        private void ServiceManagerOnDrawFinished(object sender, EventArgs e)
        {
            GameService.WaitForGames();
            if (GameService.Games.Count == 0) return;
            if (GameService.Games[0].Outputs.Count == 0) return;
            
            var deviceService = GameService.Games[0].Container.Resolve<IGraphicsDeviceService>();
            var graphicsDevice = deviceService.GraphicsDevices[1];
        }

        protected override void OnBeforeEndScene()
        {
            
        }
    }