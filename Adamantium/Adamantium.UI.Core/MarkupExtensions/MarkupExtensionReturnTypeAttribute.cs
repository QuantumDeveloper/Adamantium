namespace Adamantium.UI.Core.MarkupExtensions;

[AttributeUsage(AttributeTargets.Class)]
public class MarkupExtensionReturnTypeAttribute : Attribute
{
    public MarkupExtensionReturnTypeAttribute(Type returnType)
    {
        ReturnType = returnType;
    }
    
    public Type ReturnType { get; }
}