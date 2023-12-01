namespace Adamantium.UI.Controls;

public enum ValuePriority : int
{
    Local = 0,
    Animation = 1,
    Binding = 2,
    Trigger = 3,
    Style = 4,
    Effective,
    Default,
}