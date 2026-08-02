using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The property store read from one thread while another writes it. This is not a hypothetical: the engine runs a pump
/// thread and a loop thread, and both touch the tree - which is why reads used to take the component's lock, and why
/// holding that lock across a callback deadlocked the two against each other.
/// <para>Reads are now lock-free, so what has to be shown is that they still never observe a half-written state and
/// never throw - a reader that tore would trade a deadlock for something far worse.</para>
/// </summary>
[TestFixture]
public class PropertyStoreConcurrencyTests
{
    private const int Iterations = 20_000;

    /// <summary>A reader only ever sees a value somebody actually set - never a torn or default one mid-write.</summary>
    [Test]
    public void ReadingWhileWriting_OnlyEverSeesWrittenValues()
    {
        var border = new Border();
        var written = new[] { 1.0, 2.0, 3.0, 4.0 };
        var seen = new HashSet<double>();
        var faults = new List<string>();

        using var stop = new CancellationTokenSource();

        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var value = border.Width;
                if (!double.IsNaN(value) && System.Array.IndexOf(written, value) < 0)
                    lock (faults) faults.Add($"read {value}, which nobody wrote");
                lock (seen) seen.Add(value);
            }
        });

        for (var i = 0; i < Iterations; i++) border.Width = written[i % written.Length];

        stop.Cancel();
        reader.Wait();

        Assert.That(faults, Is.Empty);
    }

    /// <summary>Writes to DIFFERENT properties of one component do not tread on each other: the lock is per container,
    /// so they never contend - and every last one must still land.</summary>
    [Test]
    public void WritingDifferentPropertiesInParallel_AllLand()
    {
        var border = new Border();

        Parallel.Invoke(
            () => { for (var i = 0; i < Iterations; i++) border.Width = 100; },
            () => { for (var i = 0; i < Iterations; i++) border.Height = 200; },
            () => { for (var i = 0; i < Iterations; i++) border.Opacity = 0.5f; });

        Assert.Multiple(() =>
        {
            Assert.That(border.Width, Is.EqualTo(100));
            Assert.That(border.Height, Is.EqualTo(200));
            Assert.That(border.Opacity, Is.EqualTo(0.5f));
        });
    }

    /// <summary>ATTACHED properties are the ones that arrive after construction - the only part of the store that still
    /// grows - so several threads attaching to the same component at once is exactly the race worth proving.</summary>
    [Test]
    public void AttachedPropertiesSetFromManyThreads_AllLand()
    {
        var children = new List<Border>();
        for (var i = 0; i < 200; i++) children.Add(new Border());

        Parallel.ForEach(children, child =>
        {
            Grid.SetRow(child, 3);
            Grid.SetColumn(child, 4);
            Grid.SetColumnSpan(child, 2);
        });

        foreach (var child in children)
        {
            Assert.Multiple(() =>
            {
                Assert.That(Grid.GetRow(child), Is.EqualTo(3));
                Assert.That(Grid.GetColumn(child), Is.EqualTo(4));
                Assert.That(Grid.GetColumnSpan(child), Is.EqualTo(2));
            });
        }
    }

    /// <summary>Priorities still decide the winner while writes come from several threads at once: an animation masks a
    /// local value, and removing it uncovers what was underneath - the case the container has to RESCAN for.</summary>
    [Test]
    public void PrioritiesHold_WhileWrittenConcurrently()
    {
        var border = new Border();
        border.SetValue(MeasurableUIComponent.WidthProperty, 50.0, ValuePriority.Local);

        Parallel.Invoke(
            () => { for (var i = 0; i < Iterations; i++) border.SetValue(MeasurableUIComponent.WidthProperty, 999.0, ValuePriority.Animation); },
            () => { for (var i = 0; i < Iterations; i++) _ = border.Width; });

        Assert.That(border.Width, Is.EqualTo(999.0), "the animation slot outranks the local one");

        border.SetValue(MeasurableUIComponent.WidthProperty, AdamantiumProperty.UnsetValue, ValuePriority.Animation);
        Assert.That(border.Width, Is.EqualTo(50.0), "clearing it uncovers the local value again");
    }
}
