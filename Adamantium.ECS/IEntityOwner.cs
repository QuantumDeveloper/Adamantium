using System;

namespace Adamantium.ECS
{
   public interface IEntityOwner
   {
      Entity Owner { get; set; }

      event EventHandler<OwnerChangedEventArgs> OwnerChanged;
   }
}
