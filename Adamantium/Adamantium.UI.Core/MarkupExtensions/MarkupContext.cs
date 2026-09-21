namespace Adamantium.UI.Core.MarkupExtensions;

public class MarkupContext()
{
    public object TargetObject { get; set; }
    
    public string TargetObjectName { get; set; }
    
    public string TargetPropertyName { get; set; }
    
    public string RootObjectName { get; set; }
}