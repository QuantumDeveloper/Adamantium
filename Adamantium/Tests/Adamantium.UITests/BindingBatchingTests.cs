using System;
using System.ComponentModel;
using System.Globalization;
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
}
