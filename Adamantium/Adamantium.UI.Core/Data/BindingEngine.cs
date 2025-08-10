namespace Adamantium.UI.Core.Data;

public static class BindingEngine
{
    public static BindingExpressionBase SetBinding(AdamantiumComponent target, AdamantiumProperty targetProperty,
        BindingBase bindingBase)
    {
        return BindingExpression.CreateBindingExpression(target, targetProperty, bindingBase);
    }
}