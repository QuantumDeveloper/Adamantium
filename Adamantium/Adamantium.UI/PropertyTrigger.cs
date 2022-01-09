using System;

namespace Adamantium.UI;

public class PropertyTrigger:ITrigger
{
   public SetterCollection Setters { get; set; }
   public void Apply(FrameworkComponent control)
   {
      throw new NotImplementedException();
   }
}