namespace Adamantium.UI.Core.Resources.Triggers;

public class PropertyTrigger : TriggerBase
{
    public string Property { get; set; }

    /// <summary>Whose property to watch: a NAMED PART of the host's template, instead of the host itself (the default).
    /// The mirror of <see cref="ISetter.TargetName"/>, which says where a setter writes - together they let a trigger
    /// read one part and dress another. Without it a trigger can only ever ask about the whole control, so a group
    /// inside a template - a caption's traffic lights, a toolbar's overflow - had no way to answer for itself and the
    /// question had to be widened to the control that owns it.</summary>
    public string SourceName { get; set; }

    public Object Value { get; set; }

    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        var activator = new PropertyTriggerActivator(context,this);
        activator.Activate();
        return activator;
    }
}
