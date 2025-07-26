using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public class PropertyTrigger : TriggerBase
{
    private IFundamentalUIComponent component;
    
    public AdamantiumProperty Property { get; set; }
    
    public Object Value { get; set; }
    
    public override void Apply(IFundamentalUIComponent uiComponent, ITheme theme)
    {
        component = uiComponent;
        Theme = theme;
        Property.NotifyChanged += PropertyChanged;
    }

    public override void Remove(IFundamentalUIComponent uiComponent)
    {
        Property.NotifyChanged -= PropertyChanged;
        foreach (var setter in Setters)
        {
            RemoveSetter(setter, uiComponent);
        }
    }

    private void PropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender != component) return;

        if (e.NewValue != Value) return;
        
        foreach (var setter in Setters)
        {
            ApplySetter(setter, component, Theme);
        }
    }
}
