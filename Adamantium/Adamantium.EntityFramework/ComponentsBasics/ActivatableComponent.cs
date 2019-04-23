using System;
using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public abstract class ActivatableComponent: Component
   {
      private Boolean _isEnabled;

      protected ActivatableComponent()
      {
         _isEnabled = true;
      }

      public bool IsEnabled
      {
         get => _isEnabled;
          set
         {
            if (SetProperty(ref _isEnabled, value))
            {
               EnabledChanged?.Invoke(this, new StateEventArgs(value));
            }
         }
      }

      public event EventHandler<StateEventArgs> EnabledChanged;
   }
}
