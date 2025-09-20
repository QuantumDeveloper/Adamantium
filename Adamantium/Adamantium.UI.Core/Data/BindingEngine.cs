namespace Adamantium.UI.Core.Data;

public static class BindingEngine
{
    public static BindingExpressionBase SetBinding(IFundamentalUIComponent target, AdamantiumProperty targetProperty,
        BindingBase bindingBase)
    {
        return BindingExpression.CreateBindingExpression(target, targetProperty, bindingBase);
    }
    
    public static BindingExpressionBase SetBinding(IFundamentalUIComponent target, string targetProperty,
        BindingBase bindingBase)
    {
        return BindingExpression.CreateBindingExpression(target, targetProperty, bindingBase);
    }
}