using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What a property-changed callback sees when the LAST value of a property is cleared. It was once handed the raw
/// <c>UnsetValue</c> - and a callback casts what it is given, so clearing threw an InvalidCastException that took the
/// rest of the calling method with it. It no longer can: the value container keeps a seeded Default slot, so clearing
/// the local value falls back to it and the callback is told what the property now reads as. Pinned here because the
/// symptom was silent and expensive to find.
/// </summary>
[TestFixture]
public class ClearValueTests
{
    private sealed class Probe : AdamantiumComponent
    {
        public static readonly AdamantiumProperty FlagProperty = AdamantiumProperty.Register(
            nameof(Flag), typeof(bool), typeof(Probe), new PropertyMetadata(false, OnFlagChanged));

        public static object LastNewValue;
        public static string Failure;

        private static void OnFlagChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            LastNewValue = e.NewValue;
            try
            {
                var typed = (bool)e.NewValue;   // exactly what a real callback does
                Failure = null;
                _ = typed;
            }
            catch (System.Exception ex)
            {
                Failure = ex.GetType().Name;
            }
        }

        public bool Flag
        {
            get => GetValue<bool>(FlagProperty);
            set => SetValue(FlagProperty, value);
        }
    }

    [Test]
    public void ClearingTheLastValue_ReportsTheDefault_NotUnset()
    {
        var probe = new Probe { Flag = true };
        Probe.LastNewValue = null;
        Probe.Failure = null;

        probe.ClearValue(Probe.FlagProperty);

        Assert.Multiple(() =>
        {
            Assert.That(Probe.Failure, Is.Null, $"cast of e.NewValue threw; NewValue was {Probe.LastNewValue} ({Probe.LastNewValue?.GetType().Name})");
            Assert.That(Probe.LastNewValue, Is.EqualTo(false), "the callback should see the value the property now reads as");
            Assert.That(probe.Flag, Is.False, "GetValue falls back to the metadata default");
        });
    }
}
