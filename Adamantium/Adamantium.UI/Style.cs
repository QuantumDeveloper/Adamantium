using System;

namespace Adamantium.UI
{
   public class Style
   {
      public Style()
      {
         Setters = new SetterCollection();
         Triggers = new TriggerCollection();
      }

      public Style(Type targetType, Style basedOn = null)
      {
         Setters = new SetterCollection();
         Triggers = new TriggerCollection();
         TargetType = targetType;
         BasedOn = basedOn;
      }

      public Style BasedOn { get; set; }

      public Type TargetType { get; set; }

      public SetterCollection Setters { get; }

      public TriggerCollection Triggers { get; }

      public void Attach(FrameworkElement control)
      {
         if (control != null)
         {
            foreach (var setter in Setters)
            {
               setter.Apply(control);
            }

            foreach (var trigger in Triggers)
            {
               trigger.Apply(control);
            }
         }
      }
   }
}
