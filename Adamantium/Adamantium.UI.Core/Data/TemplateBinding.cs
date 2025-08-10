using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Data;

public class TemplateBinding : MarkupExtension
{
    public string Path { get; set; }

    public BindingMode Mode { get; set; } = BindingMode.OneWay;
    
    public override object ProvideObject(MarkupContext context)
    {
        return CreateBindingExpression();
    }

    protected BindingExpressionBase CreateBindingExpression()
    {
        return new TemplateBindingExpression(this);
    }
}