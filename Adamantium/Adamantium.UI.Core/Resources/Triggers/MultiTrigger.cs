using System.Collections.Generic;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// Applies its setters / Enter-Exit actions only while ALL of its <see cref="Conditions"/> hold at once - the WPF
/// MultiTrigger analog. Lets a theme express combined visual states a single <see cref="PropertyTrigger"/> can't (a
/// check box's "checked AND hovered" accent shade, "focused AND pressed", ...). Setters are the trigger's content, as
/// with PropertyTrigger; the conditions go in <c>&lt;MultiTrigger.Conditions&gt;</c>.
/// </summary>
public class MultiTrigger : TriggerBase
{
    /// <summary>The property=value tests that must ALL be true for the trigger to be active.</summary>
    public List<Condition> Conditions { get; } = [];

    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        var activator = new MultiTriggerActivator(context, this);
        activator.Activate();
        return activator;
    }
}
