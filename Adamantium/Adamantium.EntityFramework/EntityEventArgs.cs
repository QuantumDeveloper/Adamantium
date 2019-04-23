namespace Adamantium.EntityFramework
{
   public class EntityEventArgs : System.EventArgs
   {
      public Entity Entity { get; private set; }

      public EntityEventArgs(Entity entity)
      {
         Entity = entity;
      }

   }
}
