using Adamantium.Engine.Core;

namespace Adamantium.Game
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
