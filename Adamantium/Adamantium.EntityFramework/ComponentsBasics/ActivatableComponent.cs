using System;
using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public abstract class ActivatableComponent: Component
   {
      private Boolean isEnabled;

      protected ActivatableComponent()
      {
         isEnabled = true;
      }

      public bool IsEnabled
      {
         get => isEnabled;
         set => SetProperty(ref isEnabled, value);
      }
   }
}
