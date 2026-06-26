namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>One property=value test of a <see cref="MultiTrigger"/> (the WPF Condition analog). <see cref="Value"/> is
/// parsed against the host property's type at activation, exactly like a <see cref="PropertyTrigger"/>'s value.</summary>
public class Condition
{
    public string Property { get; set; }

    public object Value { get; set; }
}
