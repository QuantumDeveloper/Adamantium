using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Verifies the WPF-style fallbacks of the binding engine: <see cref="BindingBase.FallbackValue"/> is used when the
/// binding cannot resolve a source/path, and <see cref="BindingBase.TargetNullValue"/> when the resolved source value
/// is null. These let a preview/runtime show a sane value when the (design-time) source is unavailable.
/// </summary>
[TestFixture]
public class BindingFallbackTests
{
    private sealed class Holder
    {
        public string Text { get; set; }   // null by default - exercises TargetNullValue
    }

    [Test]
    public void FallbackValue_AppliedToTarget_WhenSourceUnresolved()
    {
        var border = new Border();   // no DataContext -> the binding cannot resolve its source
        var binding = new Binding("Missing") { FallbackValue = 150.0 };

        BindingExpression.CreateBindingExpression(border, MeasurableUIComponent.WidthProperty, binding);

        Assert.That(border.Width, Is.EqualTo(150.0));
    }

    [Test]
    public void FallbackValue_Coerced_FromMarkupString()
    {
        // From AUML, FallbackValue arrives as a string ("150"); the target property is double -> must coerce.
        var border = new Border();
        var binding = new Binding("Missing") { FallbackValue = "150" };

        BindingExpression.CreateBindingExpression(border, MeasurableUIComponent.WidthProperty, binding);

        Assert.That(border.Width, Is.EqualTo(150.0));
    }

    [Test]
    public void NoFallback_LeavesTargetDefault()
    {
        // Without a FallbackValue an unresolved binding must NOT clobber the target - it keeps its default (NaN width).
        var border = new Border();
        var binding = new Binding("Missing");

        BindingExpression.CreateBindingExpression(border, MeasurableUIComponent.WidthProperty, binding);

        Assert.That(double.IsNaN(border.Width), Is.True);
    }

    [Test]
    public void FallbackValue_IncompatibleWithTargetType_DoesNotThrow_AndLeavesDefault()
    {
        // The user's repro: a string FallbackValue on an ICommand property. The fallback can't be coerced to ICommand,
        // so it must be ignored (target left at default) rather than pushed - which would throw and abort the load.
        var button = new Button();
        var binding = new Binding("ShowMessageCommand") { FallbackValue = "50", Mode = BindingMode.OneWay };

        Assert.DoesNotThrow(() =>
            BindingExpression.CreateBindingExpression(button, Button.CommandProperty, binding));
        Assert.That(button.Command, Is.Null);
    }

    [Test]
    public void TargetNullValue_UsedWhenSourceValueIsNull()
    {
        // Source + path resolve fine, but the value is null -> TargetNullValue (producer mode, observed via ProducedValue).
        var binding = new Binding("Text") { Source = new Holder(), TargetNullValue = "N/A" };
        var expression = new BindingExpression(null, (AdamantiumProperty)null, binding);
        expression.EstablishConnection();

        Assert.That(expression.ProducedValue, Is.EqualTo("N/A"));
    }

    [Test]
    public void TargetNullValue_FallsBackToFallbackValue_WhenNotSet()
    {
        var binding = new Binding("Text") { Source = new Holder(), FallbackValue = "fb" };
        var expression = new BindingExpression(null, (AdamantiumProperty)null, binding);
        expression.EstablishConnection();

        Assert.That(expression.ProducedValue, Is.EqualTo("fb"));
    }
}
