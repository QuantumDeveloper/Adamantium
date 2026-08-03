using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Coercion is a MAPPING from what was requested to what the property reads as - not a rewrite of the request. These
/// tests pin that down, because the difference is invisible until the thing the coercion depends on moves: only a kept
/// request can be mapped again and come back.
/// </summary>
[TestFixture]
public class PropertyCoercionTests
{
    private sealed class Clamped : AdamantiumComponent
    {
        public static readonly AdamantiumProperty CeilingProperty = AdamantiumProperty.Register(nameof(Ceiling),
            typeof(double), typeof(Clamped), new PropertyMetadata(100.0, OnCeilingChanged));

        public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
            typeof(double), typeof(Clamped), new PropertyMetadata(0.0, null, CoerceValue));

        public double Ceiling
        {
            get => GetValue<double>(CeilingProperty);
            set => SetValue(CeilingProperty, value);
        }

        public double Value
        {
            get => GetValue<double>(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int CoercionCount { get; private set; }

        private static object CoerceValue(AdamantiumComponent d, object baseValue)
        {
            if (d is not Clamped c || baseValue is not double value) return baseValue;
            c.CoercionCount++;
            return value > c.Ceiling ? c.Ceiling : value;
        }

        private static void OnCeilingChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        {
            (d as Clamped)?.CoerceValue(ValueProperty);
        }
    }

    [Test]
    public void TheValueReadsAsCoerced()
    {
        var c = new Clamped { Ceiling = 10 };

        c.Value = 42;

        Assert.That(c.Value, Is.EqualTo(10));
    }

    /// <summary>The heart of it: 42 was clamped to 10, and when the ceiling rises the ORIGINAL 42 comes back. Storing the
    /// clamped result instead would leave the property stuck at 10 for ever, with nothing left to say it was ever 42.</summary>
    [Test]
    public void ARequestClampedByADependencyIsRestoredWhenTheDependencyMoves()
    {
        var c = new Clamped { Ceiling = 10 };
        c.Value = 42;

        c.Ceiling = 100;

        Assert.That(c.Value, Is.EqualTo(42));
    }

    [Test]
    public void ADependencyMovingTheOtherWayClampsAgain()
    {
        var c = new Clamped { Ceiling = 100, Value = 42 };

        c.Ceiling = 5;

        Assert.That(c.Value, Is.EqualTo(5));
    }

    /// <summary>A change is announced as the value the property READS as - a callback that saw the raw request would be
    /// reacting to a value the property never had.</summary>
    [Test]
    public void TheChangeIsReportedAsTheCoercedValue()
    {
        var c = new Clamped { Ceiling = 10 };
        object reported = null;
        ValueProperty_Changed(c, v => reported = v);

        c.Value = 42;

        Assert.That(reported, Is.EqualTo(10.0));
    }

    [Test]
    public void ReCoercingToTheSameValueAnnouncesNothing()
    {
        var c = new Clamped { Ceiling = 100, Value = 42 };
        var changes = 0;
        ValueProperty_Changed(c, _ => changes++);

        c.CoerceValue(Clamped.ValueProperty);

        Assert.That(changes, Is.Zero, "nothing moved, so nothing is announced - which is what stops re-coercion looping");
    }

    [Test]
    public void CoercingAPropertyNobodySetDoesNothing()
    {
        var c = new Clamped();

        Assert.DoesNotThrow(() => c.CoerceValue(Clamped.ValueProperty));
        Assert.That(c.Value, Is.EqualTo(0.0));
    }

    /// <summary>Coercion must not touch the metadata it is handed: the default belongs to the TYPE, and one instance's
    /// state deciding it would change every other instance of that type for the life of the process.</summary>
    [Test]
    public void OneInstanceCannotChangeTheDefaultForAnother()
    {
        var first = new Clamped { Ceiling = 5 };
        first.Value = 42;

        var second = new Clamped();

        Assert.That(second.Value, Is.EqualTo(0.0), "the authored default, untouched by the first instance");
        Assert.That(second.Ceiling, Is.EqualTo(100.0));
    }

    private static void ValueProperty_Changed(Clamped component, Action<object> onChanged)
    {
        component.PropertyChanged += (_, e) =>
        {
            if (e.Property == Clamped.ValueProperty) onChanged(e.NewValue);
        };
    }
}
