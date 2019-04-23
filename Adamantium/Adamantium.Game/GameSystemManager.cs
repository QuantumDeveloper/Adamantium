using Adamantium.Engine.Core;

namespace Adamantium.Engine
{
   public class GameSystemManager:SystemManager
   {
      protected GameBase Game;

      public GameSystemManager(GameBase game):base (game)
      {
         Game = game;
      }
   }
}
