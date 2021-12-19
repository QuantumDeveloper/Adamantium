using System;

namespace Adamantium.UI.RoutedEvents
{
   public class AdamantiumPropertyChangedEventArgs : EventArgs
   {
      public AdamantiumPropertyChangedEventArgs(AdamantiumProperty property, object oldValue, object newValue)
      {
         Property = property;
         OldValue = oldValue;
         NewValue = newValue;
      }

      public object NewValue { get; }
      public object OldValue { get; }
      public AdamantiumProperty Property { get; }
   }
}
