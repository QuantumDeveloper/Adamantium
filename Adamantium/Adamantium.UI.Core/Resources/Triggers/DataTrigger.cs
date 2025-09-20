using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Core.Resources.Triggers;

public class DataTrigger : TriggerBase
{
    public Binding Binding { get; set; }
    
    public object Value { get; set; }
    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        return null;
    }
}