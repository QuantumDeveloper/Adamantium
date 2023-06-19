using System;
using Adamantium.Core.DependencyInjection;
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
            //DisableRendering = true;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            GameService = new GameService();
        }

        protected override void RegisterServices(IContainerRegistry containerRegistry)
        {
            base.RegisterServices(containerRegistry);
            containerRegistry.RegisterSingleton<IGameService>(GameService);
        }
    }