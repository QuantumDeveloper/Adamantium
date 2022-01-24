using System;

namespace Adamantium.UI;

public interface ISetter
{
   AdamantiumProperty Property { get; set; }
   
   Object Value { get; set; }
   
   void Apply(IAdamantiumComponent control);
}