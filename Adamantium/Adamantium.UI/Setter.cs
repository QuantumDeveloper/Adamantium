using System;
using Adamantium.UI.Data;
using Adamantium.UI.Resources;

namespace Adamantium.UI;

public class Setter : ISetter, IEquatable<Setter>
{
    public Setter()
    {
    }

    public Setter(string property, object value)
    {
        Property = property;
        Value = value;
    }

    public string Property { get; set; }
    public Object Value { get; set; }

    public void Apply(IFundamentalUIComponent component, ITheme theme)
    {
        switch (Value)
        {
            case BindingBase binding:
            {
                component.SetBinding(Property, binding);
                break;
            }
            case ResourceReference resourceReference:
            {
                var resource = theme.Resources[resourceReference.Name];
                component.SetValue(Property, resource);
                break;
            }
            default:
                component.SetValue(Property, Value);
                break;
        }
    }

    public void UnApply(IFundamentalUIComponent component, ITheme theme)
    {
        switch (Value)
        {
            case BindingBase binding:
            {
                component.RemoveBinding(Property);

                break;
            }
            case ResourceReference resourceReference:
            {
                var resource = theme.Resources[resourceReference.Name];
                component.SetValue(Property, resource);
                break;
            }
            default:
                component.SetValue(Property, Value);
                break;
        }
    }

    public bool Equals(Setter other)
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
        return Equals((Setter)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Property);
    }
}