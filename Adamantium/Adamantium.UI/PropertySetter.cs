using System;
using Adamantium.UI.Data;

namespace Adamantium.UI;

public class PropertySetter : ISetter, IEquatable<PropertySetter>
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
         if (control is IUIComponent component)
         {
            component.SetBinding(Property, binding);
         }
      }
   }

   public bool Equals(PropertySetter other)
   {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Equals(Property, other.Property) && Equals(Value, other.Value);
   }

   public override bool Equals(object obj)
   {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((PropertySetter)obj);
   }

   public override int GetHashCode()
   {
      return HashCode.Combine(Property);
   }
  
}