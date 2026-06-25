using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Core.Resources;

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
    
    public string TargetName { get; set; }

    public void Apply(IFundamentalUIComponent component, Style style, ITheme theme)
    {
        switch (Value)
        {
            case BindingBase binding:
                component.SetBinding(Property, (BindingBase)binding.Clone());
                break;
            case ResourceReference resourceReference:
                if (!theme.TryGetResource(resourceReference.Name, out var resource))
                    throw new ResourceNotFoundException(
                        $"Resource {resourceReference.Name} is not found for theme: {theme.Name} and control: {component.GetType().Name}");
                    
                component.SetStyleValue(Property, resource, style);
                break;
            case ThemeResource themeResource:
                themeResource.Apply(component, Property, ValuePriority.Style);
                break;
            default:
                var prop = AdamantiumPropertyMap.FindRegistered(component.GetType(), Property);
                if (prop == null)
                    return;
                
                var value = TypeCastFactory.CastFromString(Value, prop.PropertyType);
                component.SetStyleValue(prop, value, style);
                break;
        }
    }

    public void Remove(IFundamentalUIComponent component, Style style, ITheme theme)
    {
        switch (Value)
        {
            case BindingBase binding:
                component.RemoveBinding(Property);
                break;
            case ThemeResource:
                ThemeResource.Remove(component, Property, ValuePriority.Style);
                break;
            default:
                component.RemoveStyleValue(Property, style);
                break;
        }
    }

    public bool Equals(Setter other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        // TargetName is part of a setter's identity: two setters that write the SAME property/value onto DIFFERENT
        // template parts (e.g. a trigger lighting up both scrollbars' IsHitTestVisible) are distinct. Omitting it
        // collapsed them into one dictionary key in the trigger activator, so applying the second tore down the first.
        return Equals(Property, other.Property) && Equals(Value, other.Value) && Equals(TargetName, other.TargetName);
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
        return HashCode.Combine(Property, TargetName);
    }
}