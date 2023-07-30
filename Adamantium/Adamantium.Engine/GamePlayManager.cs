using Adamantium.Core.DependencyInjection;
using Adamantium.EntityFramework;

namespace Adamantium.Engine
{
   public class GamePlayManager
   {
      public Entity UserControlledEntity { get; private set; }

      public Entity SelectedEntity { get; set; }

      public GamePlayManager(IContainerRegistry container)
      {
         container.RegisterInstance<GamePlayManager>(this);
      }

      public void SetUserControlled(Entity entity)
      {
         UserControlledEntity = entity;
      }

      public void SetSelected(Entity entity)
      {
         SelectedEntity = entity;
      }
   }
}
