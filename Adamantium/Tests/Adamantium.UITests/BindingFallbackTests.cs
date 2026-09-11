using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
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

    private sealed class Painter : System.ComponentModel.INotifyPropertyChanged
    {
        private Brush _brush = Brushes.Red;

        public Brush Brush
        {
            get => _brush;
            set { _brush = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Brush))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    // "The path did not resolve" and "the source HOLDS null" are two different answers, and only the first one has
    // nothing to say. They used to arrive as the same null and both were dropped - so a source could never hand a
    // property its default back, and every "null means let the theme decide" property (a grid's rules, its search
    // washes) was one-way in practice: a page could take the theme's colour over but never give it back.
    [Test]
    public void ASourceThatHoldsNull_HandsTheTargetItsDefaultBack()
    {
        var painter = new Painter();
        var border = new Border { DataContext = painter };
        border.SetBinding(nameof(Border.Background), new Binding(nameof(Painter.Brush)));
        Assert.That(border.Background, Is.EqualTo(Brushes.Red), "the binding carried the brush over");

        painter.Brush = null;
        BindingUpdateQueue.Flush();

        Assert.That(border.Background, Is.Null, "...and carried the source's null back");
    }

    // The other half of the same rule: a property with no null to go back to must not be handed one. A double slot
    // holding null is read as (double)null.
    [Test]
    public void APropertyThatCannotHoldNothing_IsNotHandedIt()
    {
        var border = new Border { Width = 42, DataContext = new Holder() };
        border.SetBinding(nameof(border.Width), new Binding(nameof(Holder.Text)));

        Assert.Multiple(() =>
        {
            Assert.That(border.Width, Is.EqualTo(42));
            Assert.DoesNotThrow(() => _ = border.Width);
        });
    }
}
