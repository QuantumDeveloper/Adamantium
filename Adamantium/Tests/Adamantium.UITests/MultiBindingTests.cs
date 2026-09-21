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

    // --- boolean-logic nesting: a nested MultiBinding acts as a condition that can flip the outer result -----------

    private sealed class Gate : INotifyPropertyChanged
    {
        private bool _isEnabled, _isVisible, _isAdmin, _maintenance;
        public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value, nameof(IsEnabled)); }
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value, nameof(IsVisible)); }
        public bool IsAdmin { get => _isAdmin; set => Set(ref _isAdmin, value, nameof(IsAdmin)); }
        public bool Maintenance { get => _maintenance; set => Set(ref _maintenance, value, nameof(Maintenance)); }

        private void Set(ref bool field, bool value, string name)
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    private sealed class AllTrueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.All(v => v is true);
        public object[] ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // base = AND of every value except the last; the last value is an "invert" flag (from a nested condition) that
    // flips the result 180 degrees when true.
    private sealed class AndWithOverrideConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var invert = values.Length > 0 && values[^1] is true;
            var result = values.Take(values.Length - 1).All(v => v is true);
            return invert ? !result : result;
        }
        public object[] ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    [Test]
    public void NestedMultiBinding_OverrideConditionFlipsResult()
    {
        var gate = new Gate { IsEnabled = true, IsVisible = true, IsAdmin = false, Maintenance = false };

        // Nested condition: "invert when an admin is in maintenance mode" — a check over several properties.
        var invert = new MultiBinding { Converter = new AllTrueConverter() };
        invert.Bindings.Add(new Binding("IsAdmin") { Source = gate });
        invert.Bindings.Add(new Binding("Maintenance") { Source = gate });

        var outer = new MultiBinding { Converter = new AndWithOverrideConverter() };
        outer.Bindings.Add(new Binding("IsEnabled") { Source = gate });
        outer.Bindings.Add(new Binding("IsVisible") { Source = gate });
        outer.Bindings.Add(invert);   // the nested MultiBinding is one of the outer's children

        var expression = Producer(outer, out var current);

        Assert.That(expression.ProducedValue, Is.EqualTo(true));   // base true, condition not met

        gate.IsAdmin = true;                                       // condition still incomplete (Maintenance false)
        Assert.That(current(), Is.EqualTo(true));

        gate.Maintenance = true;                                   // condition now met -> flip 180
        Assert.That(current(), Is.EqualTo(false));

        gate.Maintenance = false;                                  // flip off -> back to base
        Assert.That(current(), Is.EqualTo(true));

        gate.IsVisible = false;                                    // base now false, condition off
        Assert.That(current(), Is.EqualTo(false));
    }

    [Test]
    public void NestedMultiBinding_FlipAppliesEvenWhenBaseIsFalse()
    {
        var gate = new Gate { IsEnabled = false, IsVisible = true, IsAdmin = true, Maintenance = true };

        var invert = new MultiBinding { Converter = new AllTrueConverter() };
        invert.Bindings.Add(new Binding("IsAdmin") { Source = gate });
        invert.Bindings.Add(new Binding("Maintenance") { Source = gate });

        var outer = new MultiBinding { Converter = new AndWithOverrideConverter() };
        outer.Bindings.Add(new Binding("IsEnabled") { Source = gate });
        outer.Bindings.Add(new Binding("IsVisible") { Source = gate });
        outer.Bindings.Add(invert);

        var expression = Producer(outer, out var current);

        Assert.That(expression.ProducedValue, Is.EqualTo(true));   // base false, but condition flips it to true

        gate.IsAdmin = false;                                      // condition off -> back to base false
        Assert.That(current(), Is.EqualTo(false));
    }
}
