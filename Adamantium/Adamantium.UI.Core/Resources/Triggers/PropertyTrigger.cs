namespace Adamantium.UI.Core.Resources.Triggers;

public class PropertyTrigger : TriggerBase
{
    public string Property { get; set; }
    
    public Object Value { get; set; }
    
    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        var activator = new PropertyTriggerActivator(context,this);
        activator.Activate();
        return activator;
    }
}
