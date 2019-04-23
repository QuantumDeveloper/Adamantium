using System;

namespace Adamantium.EntityFramework
{
   public interface IEntityOwner
   {
      Entity Owner { get; set; }

      event EventHandler<OwnerChangedEventArgs> OwnerChanged;
   }
}
