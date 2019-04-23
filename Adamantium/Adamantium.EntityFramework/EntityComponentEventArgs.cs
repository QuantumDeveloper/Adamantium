using Adamantium.EntityFramework.ComponentsBasics;

namespace Adamantium.EntityFramework
{
    public class EntityComponentEventArgs : System.EventArgs
   {
      public Entity ComponentOwner { get; }
      public IComponent OldComponent { get; }
      public IComponent NewComponent { get; }
      public ComponentChangedAction Action { get; }

      public EntityComponentEventArgs(
         Entity componentOwner,
         IComponent oldComponent,
         IComponent newComponent,
         ComponentChangedAction action)
      {
         ComponentOwner = componentOwner;
         OldComponent = oldComponent;
         NewComponent = newComponent;
         Action = action;
      }
   }
}
