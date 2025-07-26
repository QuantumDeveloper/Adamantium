using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Core.Resources.Triggers;

public class DataTrigger : TriggerBase
{
    private IFundamentalUIComponent component;
    
    public Binding Binding { get; set; }
    
    public object Value { get; set; }
    public override void Apply(IFundamentalUIComponent uiComponent, ITheme theme)
    {
        Theme = theme;
        component = uiComponent;
    }

    public override void Remove(IFundamentalUIComponent uiComponent)
    {
        throw new System.NotImplementedException();
    }
}