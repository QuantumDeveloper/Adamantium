using System;
using Adamantium.Game.Core;
using Adamantium.UI;
using Adamantium.UI.Controls;

namespace Adamantium.Game;

public abstract class GameApplication : UIApplication
    {
        public Game Game { get; protected set; }
        
        public GameApplication()
        {
            DisableRendering = true;
        }

        protected virtual Game OnCreateGameInstance()
        {
            return new Game(GameMode.Slave, true);
        }

        protected override void OnStartup()
        {
            base.OnStartup();
            Game = OnCreateGameInstance();
            Game.Initialized += GameInitialized;
            Game.Run();
        }

        private void GameInitialized(object sender, EventArgs e)
        {
            OnGameInitialized();
        }

        protected virtual void OnGameInitialized()
        {
            
        }
        
    }