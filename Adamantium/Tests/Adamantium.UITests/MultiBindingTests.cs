using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Exercises the multi-binding core in producer mode (no UI target): child bindings use an explicit Source, so the
/// expression's combined value is observed through ProducedValue/ValueChanged. Covers combining several sources,
/// live updates via INotifyPropertyChanged, and multi-binding nested inside multi-binding.
/// </summary>
[TestFixture]
public class MultiBindingTests
{
    private sealed class Num : INotifyPropertyChanged
    {
        private int _value;
        public int Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    private sealed class SumConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Where(v => v != null).Sum(System.Convert.ToInt32);
        public object[] ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private static MultiBindingExpression Producer(MultiBinding mb, out Func<object> current)
    {
        var expression = new MultiBindingExpression(null, null, mb);
        object last = null;
        expression.ValueChanged += e => last = e.ProducedValue;
        expression.EstablishConnection();
        current = () => last;
        return expression;
    }

    [Test]
    public void Combines_MultipleSources_AndUpdatesLive()
    {
        var a = new Num { Value = 1 };
        var b = new Num { Value = 2 };
        var mb = new MultiBinding { Converter = new SumConverter() };
        mb.Bindings.Add(new Binding("Value") { Source = a });
        mb.Bindings.Add(new Binding("Value") { Source = b });

        var expression = Producer(mb, out var current);

        Assert.That(expression.ProducedValue, Is.EqualTo(3));

        a.Value = 10;
        Assert.That(current(), Is.EqualTo(12));   // 10 + 2, pushed via INotifyPropertyChanged
    }

    [Test]
    public void NestedMultiBinding_CombinesAndBubbles()
    {
        var a = new Num { Value = 1 };
        var b = new Num { Value = 2 };

        var inner = new MultiBinding { Converter = new SumConverter() };
        inner.Bindings.Add(new Binding("Value") { Source = a });
        inner.Bindings.Add(new Binding("Value") { Source = b });

        var outer = new MultiBinding { Converter = new SumConverter() };
        outer.Bindings.Add(new Binding("Value") { Source = a });
        outer.Bindings.Add(inner);   // multi-binding inside multi-binding

        var expression = Producer(outer, out var current);

        Assert.That(expression.ProducedValue, Is.EqualTo(4));   // a + (a + b) = 1 + 3

        a.Value = 10;
        Assert.That(current(), Is.EqualTo(22));                 // 10 + (10 + 2)
    }

    [Test]
    public void StringFormat_UsedWhenNoConverter()
    {
        var a = new Num { Value = 3 };
        var b = new Num { Value = 7 };
        var mb = new MultiBinding { StringFormat = "{0}+{1}" };
        mb.Bindings.Add(new Binding("Value") { Source = a });
        mb.Bindings.Add(new Binding("Value") { Source = b });

        var expression = Producer(mb, out _);

        Assert.That(expression.ProducedValue, Is.EqualTo("3+7"));
    }
}
