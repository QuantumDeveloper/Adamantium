using Adamantium.Engine.Core;
using Adamantium.EntityFramework;

namespace Adamantium.Engine
{
   public class GamePlayManager
   {
      public Entity UserControlledEntity { get; private set; }

      public Entity SelectedEntity { get; set; }

      public GamePlayManager(IServiceStorage storage)
      {
         storage.Add(this);
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
