using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

// F2 binding-storm batching: a runtime source change is coalesced and applied to the target once per frame (flush),
// not synchronously. IsImmediate opts back into synchronous application.
public class BindingBatchingTests
{
    private sealed class Vm : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    private sealed class CountingConverter : IValueConverter
    {
        public int Count;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { Count++; return value; }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }

    [Test]
    public void RuntimeSourceChange_IsBatched_AppliedOnFlush()
    {
        var vm = new Vm { Name = "A" };
        var target = new TextBlock { DataContext = vm };
        target.SetBinding("Text", new Binding("Name"));

        Assert.That(target.Text, Is.EqualTo("A"), "initial connect push is synchronous");

        vm.Name = "B";
        Assert.That(target.Text, Is.EqualTo("A"), "a runtime source change must be batched, not applied synchronously");

        BindingUpdateQueue.Flush();
        Assert.That(target.Text, Is.EqualTo("B"), "the per-frame flush applies the batched update");
    }

    [Test]
    public void ManySourceChanges_CoalesceToOneApply()
    {
        var vm = new Vm { Name = "0" };
        var converter = new CountingConverter();
        var target = new TextBlock { DataContext = vm };
        target.SetBinding("Text", new Binding("Name") { Converter = converter });

        var convertsAfterConnect = converter.Count;   // initial connect already converted once

        for (var i = 1; i <= 5; i++) vm.Name = i.ToString();   // 5 rapid changes, all coalesced

        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(converter.Count - convertsAfterConnect, Is.EqualTo(1), "5 source changes should collapse to ONE apply");
            Assert.That(target.Text, Is.EqualTo("5"), "the coalesced apply uses the final source value");
        });
    }

    [Test]
    public void ImmediateBinding_AppliesSynchronously()
    {
        var vm = new Vm { Name = "A" };
        var target = new TextBlock { DataContext = vm };
        target.SetBinding("Text", new Binding("Name") { IsImmediate = true });

        vm.Name = "B";
        Assert.That(target.Text, Is.EqualTo("B"), "an IsImmediate binding applies synchronously, without a flush");
    }

    [Test]
    public void FlushBudget_CapsAppliesPerFlush_DrainsOverFlushes()
    {
        const int n = 50;
        var converter = new CountingConverter();
        var targets = new List<TextBlock>();
        var vms = new List<Vm>();
        for (var i = 0; i < n; i++)
        {
            var vm = new Vm { Name = "init" };
            var t = new TextBlock { DataContext = vm };
            t.SetBinding("Text", new Binding("Name") { Converter = converter });
            vms.Add(vm);
            targets.Add(t);
        }
        BindingUpdateQueue.Flush();   // clear any leftover from other tests; setup connects are synchronous anyway

        foreach (var vm in vms) vm.Name = "changed";   // n batched, coalesced dirty bindings

        var savedCap = BindingUpdateQueue.MaxAppliesPerFlush;
        try
        {
            BindingUpdateQueue.MaxAppliesPerFlush = 10;
            var convertsBefore = converter.Count;
            BindingUpdateQueue.Flush();

            Assert.Multiple(() =>
            {
                Assert.That(converter.Count - convertsBefore, Is.EqualTo(10), "a flush applies at most the budget");
                Assert.That(targets.Count(t => t.Text == "changed"), Is.EqualTo(10), "only the budgeted bindings updated this flush");
            });

            for (var f = 0; f < 10 && targets.Any(t => t.Text != "changed"); f++) BindingUpdateQueue.Flush();
            Assert.That(targets.All(t => t.Text == "changed"), Is.True, "the over-budget remainder drains over subsequent flushes");
        }
        finally { BindingUpdateQueue.MaxAppliesPerFlush = savedCap; }
    }

    private sealed class SpanVm : INotifyPropertyChanged
    {
        private double _max = 100;
        private double _end = 100;

        public double Max
        {
            get => _max;
            set { if (_max == value) return; _max = value; Raise(nameof(Max)); }
        }

        public double End
        {
            get => _end;
            set { if (_end == value) return; _end = value; Raise(nameof(End)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// A dependent chain has to settle INSIDE one flush, because layout runs immediately after it. Two controls share one
    /// view-model property here: the first clamps the value it is handed and writes the result back, which is a source
    /// change for the second. Applying that only on the next frame laid the second control out with its new ceiling and
    /// its old value - one frame short of the end of the rail, the next frame back on it, which is a thumb that visibly
    /// shivers at the edge for as long as the ceiling is being dragged.
    /// </summary>
    [Test]
    public void ADependentChain_SettlesWithinOneFlush()
    {
        var vm = new SpanVm();

        // The first slider follows the ceiling, clamps its end against it, and publishes the clamped end.
        var first = new RangeSlider { DataContext = vm, Minimum = 0 };
        first.SetBinding("Maximum", new Binding("Max"));
        first.SetBinding("UpperValue", new Binding("End") { Mode = BindingMode.TwoWay });

        // The second only reads that end. Its OWN ceiling is fixed, so nothing local can move its value: the new end can
        // reach it only through the view-model - which is exactly the second link of the chain.
        var second = new RangeSlider { DataContext = vm, Minimum = 0, Maximum = 100 };
        second.SetBinding("UpperValue", new Binding("End"));
        BindingUpdateQueue.Flush();
        Assert.That(second.UpperValue, Is.EqualTo(100), "both start at the view-model's end");

        // One flush, two links: the ceiling reaches the first slider, whose clamp writes the end back, which must reach
        // the second before layout runs on it.
        vm.Max = 40;
        BindingUpdateQueue.Flush();

        Assert.That(first.UpperValue, Is.EqualTo(40), "the first slider clamped its end to the new ceiling");
        Assert.That(second.UpperValue, Is.EqualTo(40),
            "and the second must not be laid out a frame behind the value that clamp published");
    }
}
