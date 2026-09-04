namespace Adamantium.UI.Core;

public enum ValuePriority : int
{
    Animation = 0,
    Local = 1,
    Binding = 2,
    Trigger = 3,
    Template = 4,
    Style = 5,
    Inherited = 6,
    TypeDefault = 7,
    Default,
    Effective
}