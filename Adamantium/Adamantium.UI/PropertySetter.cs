using System;
using Adamantium.UI.Data;

namespace Adamantium.UI;

public class PropertySetter : ISetter
{
   public PropertySetter()
   {

   }

   public PropertySetter(AdamantiumProperty property, object value)
   {
      Property = property;
      Value = value;
   }

   public AdamantiumProperty Property { get; set; }
   public Object Value { get; set; }

   public void Apply(IAdamantiumComponent control)
   {
      var binding = Value as BindingBase;

      if (binding == null)
      {
         control.SetValue(Property, Value);
      }
      else
      {
         if (control is IFrameworkComponent component)
         {
            component.SetBinding(Property, binding);
         }
      }
   }
}