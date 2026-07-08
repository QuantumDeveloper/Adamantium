using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

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
                ApplyResourceReference(component, style, theme, resourceReference);
                break;
            case ThemeResource themeResource:
                themeResource.Apply(component, Property, ValuePriority.Style);
                break;
            case ObservableResource observableResource:
                observableResource.Apply(component, Property, ValuePriority.Style);
                break;
            case Ancestor ancestor:
                ancestor.Apply(component, Property);
                break;
            case Self self:
                self.Apply(component, Property);
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

    // A {ResourceReference} resolves TREE-SCOPED from the styled component (Local dictionaries on this element or an
    // ancestor, then Theme, then Global). But a style is applied the moment it is attached - which happens WHILE the tree
    // is still being built, before the element has any parent - so a Local resource declared on an ancestor isn't
    // reachable yet and the immediate lookup misses. In that case defer: resolve again when the element attaches to the
    // (rooted) visual tree. That attach cascades through the whole subtree, so by then the full ancestor chain - including
    // the resource's owner - is present. Theme/Global references resolve immediately and never defer.
    private void ApplyResourceReference(IFundamentalUIComponent component, Style style, ITheme theme,
        ResourceReference reference)
    {
        // theme can be null here: an inline <X.Styles> is attached during construction (the moment it's added to the
        // Styles collection), before the element is themed. Fall back to the current theme; if it (or the resource) is
        // still unavailable, defer to visual-tree attach - by which point the element is themed AND rooted, so both the
        // theme and the full ancestor chain (a Local resource's owner) are present.
        var activeTheme = theme ?? UIAppContext.Current?.ThemeManager?.CurrentTheme;

        if (activeTheme != null && activeTheme.TryGetResource(component, reference.Name, out var resource))
        {
            component.SetStyleValue(Property, resource, style);
            return;
        }

        if (component is IUIComponent visual)
        {
            void OnAttached(object sender, VisualTreeAttachmentEventArgs e)
            {
                visual.AttachedToVisualTreeEvent -= OnAttached;
                var rootedTheme = UIAppContext.Current?.ThemeManager?.CurrentTheme;
                if (rootedTheme != null && rootedTheme.TryGetResource(component, reference.Name, out var deferred))
                    component.SetStyleValue(Property, deferred, style);
                // Still missing once rooted+themed = a genuinely undefined resource; leave the property unset (visibly
                // empty) rather than throw from inside the attach cascade and take down the whole subtree.
            }

            visual.AttachedToVisualTreeEvent += OnAttached;
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
            case ObservableResource:
                ObservableResource.Remove(component, Property, ValuePriority.Style);
                break;
            case Ancestor:
            case Self:
                component.RemoveBinding(Property);   // .Apply registered the expression in BindingEngine, keyed by property
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