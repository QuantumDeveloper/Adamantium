using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Terse self binding: <c>{Self Background}</c> binds a target property to ANOTHER property on the SAME element (the
/// WPF <c>{Binding RelativeSource={RelativeSource Self}}</c>). The single positional arg is the source property path.
/// </summary>
public class Self : MarkupExtension
{
    [DefaultProperty]
    public string Path { get; set; }

    public BindingMode Mode { get; set; } = BindingMode.OneWay;

    public IValueConverter Converter { get; set; }

    public object ConverterParameter { get; set; }

    public object FallbackValue { get; set; }

    public object TargetNullValue { get; set; }

    public SelfBindingExpression Apply(IFundamentalUIComponent target, string propertyName,
        ValuePriority priority = ValuePriority.Binding)
    {
        var expression = new SelfBindingExpression(target, target.GetProperty(propertyName), this) { Priority = priority };
        BindingEngine.Register(expression);
        return expression;
    }

    public override object ProvideObject(MarkupContext context)
    {
        if (context?.TargetObject is IFundamentalUIComponent target && !string.IsNullOrEmpty(context.TargetPropertyName))
            Apply(target, context.TargetPropertyName);
        return this;
    }
}
