using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What a changed-callback is told the property WAS. Both ends of the report are the value the property
/// READS AS - never the raw content of one priority slot.</summary>
[TestFixture]
public class PropertyChangedOldValueTests
{
    private class Probe : AdamantiumComponent
    {
        public static readonly AdamantiumProperty ShadeProperty = AdamantiumProperty.Register(nameof(Shade),
            typeof(string), typeof(Probe), new PropertyMetadata("default", OnShadeChanged));

        public readonly List<string> Seen = [];

        private static void OnShadeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
            ((Probe)a).Seen.Add($"{e.OldValue}->{e.NewValue}");

        public string Shade
        {
            get => GetValue<string>(ShadeProperty);
            set => SetValue(ShadeProperty, value);
        }
    }

    [Test]
    public void TheFirstWrite_ReportsTheDefaultItHad_NotTheUnsetSentinel()
    {
        var probe = new Probe { Shade = "red" };

        Assert.That(probe.Seen, Is.EqualTo(new[] { "default->red" }));
    }

    // The write goes to the LOCAL slot, which is empty; the value on screen came from the STYLE slot. What the property
    // was is the style's value, not the empty slot.
    [Test]
    public void AWriteOverAHigherSourcePriority_ReportsWhatWasOnScreen()
    {
        var probe = new Probe();
        probe.SetValue(Probe.ShadeProperty, "fromStyle", ValuePriority.Style);
        probe.Seen.Clear();

        probe.SetValue(Probe.ShadeProperty, "fromLocal", ValuePriority.Local);

        Assert.That(probe.Seen, Is.EqualTo(new[] { "fromStyle->fromLocal" }));
    }

    // Writing what the property already reads as is not a change, whichever slot it lands in.
    [Test]
    public void WritingTheValueItAlreadyReadsAs_IsNotAChange()
    {
        var probe = new Probe();

        probe.Shade = "default";

        Assert.That(probe.Seen, Is.Empty);
    }

    // An ATTACHED property outside this component's type chain has no seeded slot - its container is made on first
    // write. What a READER would have got there is the default, and that is what the report says.
    private static class Attacher
    {
        public static readonly AdamantiumProperty MarkProperty = AdamantiumProperty.RegisterAttached("Mark",
            typeof(string), typeof(Probe), new PropertyMetadata("unmarked", OnMarkChanged));

        public static readonly List<string> Log = [];

        private static void OnMarkChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
            Log.Add($"{e.OldValue}->{e.NewValue}");
    }

    [Test]
    public void AnAttachedPropertysFirstWrite_AlsoReportsTheDefault()
    {
        Attacher.Log.Clear();
        var probe = new Probe();

        probe.SetValue(Attacher.MarkProperty, "marked");

        Assert.Multiple(() =>
        {
            Assert.That(probe.GetValue(Attacher.MarkProperty), Is.EqualTo("marked"));
            Assert.That(Attacher.Log, Is.EqualTo(new[] { "unmarked->marked" }));
        });
    }

    // Only a transition between two CONCRETE lengths is a size change - an auto (NaN) width is not a size.
    [Test]
    public void SizeChanged_IgnoresTheAutoLength_AndFiresBetweenConcreteOnes()
    {
        var border = new Border();
        var sizes = new List<Size>();
        border.SizeChanged += (_, e) => sizes.Add(e.NewSize);

        border.Width = 100;   // auto -> explicit: no concrete "before", so nothing is reported
        Assert.That(sizes, Is.Empty);

        border.Width = 250;   // and now a real transition between two lengths
        Assert.That(sizes, Has.Count.EqualTo(1));
        Assert.That(sizes[0].Width, Is.EqualTo(250));
    }

    // A style value taken away leaves the property back on its default, and the callback must be told THAT, never the
    // Unset sentinel.
    [Test]
    public void ClearingTheOnlySource_ReportsTheFallbackItLandsOn()
    {
        var probe = new Probe();
        probe.SetValue(Probe.ShadeProperty, "fromStyle", ValuePriority.Style);
        probe.Seen.Clear();

        probe.SetValue(Probe.ShadeProperty, AdamantiumProperty.UnsetValue, ValuePriority.Style);

        Assert.That(probe.Seen, Is.EqualTo(new[] { "fromStyle->default" }),
            "what it reads as now is the default, so that is the new value");
    }
}
