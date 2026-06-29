using System.ComponentModel;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Value inheritance down the logical tree (DataContext and any other Inherits property). The mechanism existed but
/// was disconnected (InheritanceParent was never wired); these lock in that a parent's DataContext reaches its
/// descendants — including the payoff: a child's {Binding} resolving against the inherited DataContext.
/// </summary>
[TestFixture]
public class InheritedPropertyTests
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

    [Test]
    public void DataContext_InheritsToChild()
    {
        var vm = new object();
        var parent = new TextBlock();
        var child = new TextBlock();
        parent.AddLogicalChild(child);

        parent.DataContext = vm;

        Assert.That(child.DataContext, Is.SameAs(vm));
    }

    [Test]
    public void DataContext_InheritsWhenAttachedAfterItWasSet()
    {
        var vm = new object();
        var parent = new TextBlock { DataContext = vm };   // set BEFORE the child is attached
        var child = new TextBlock();

        parent.AddLogicalChild(child);

        Assert.That(child.DataContext, Is.SameAs(vm));      // attach-time push (order-independent)
    }

    [Test]
    public void DataContext_InheritsToGrandchild()
    {
        var vm = new object();
        var root = new TextBlock();
        var mid = new TextBlock();
        var leaf = new TextBlock();
        mid.AddLogicalChild(leaf);
        root.AddLogicalChild(mid);

        root.DataContext = vm;

        Assert.That(mid.DataContext, Is.SameAs(vm));
        Assert.That(leaf.DataContext, Is.SameAs(vm));
    }

    [Test]
    public void DataContext_LocalValueIsNotOverriddenByInheritance()
    {
        var inherited = new object();
        var own = new object();
        var parent = new TextBlock();
        var child = new TextBlock { DataContext = own };   // child has its own context
        parent.AddLogicalChild(child);

        parent.DataContext = inherited;

        Assert.That(child.DataContext, Is.SameAs(own));     // local wins
    }

    [Test]
    public void DataContext_ChangePropagates()
    {
        var first = new object();
        var second = new object();
        var parent = new TextBlock();
        var child = new TextBlock();
        parent.AddLogicalChild(child);

        parent.DataContext = first;
        Assert.That(child.DataContext, Is.SameAs(first));

        parent.DataContext = second;
        Assert.That(child.DataContext, Is.SameAs(second));
    }

    [Test]
    public void Binding_ResolvesAgainstInheritedDataContext()
    {
        var vm = new Vm { Name = "Alice" };
        var parent = new TextBlock();
        var child = new TextBlock();
        parent.AddLogicalChild(child);
        child.SetBinding("Text", new Binding("Name"));

        parent.DataContext = vm;                            // DataContext inherits to child -> binding re-resolves

        Assert.That(child.Text, Is.EqualTo("Alice"));

        vm.Name = "Bob";
        BindingUpdateQueue.Flush();   // F2: a runtime source change is applied once per frame (batched); flush to apply now
        Assert.That(child.Text, Is.EqualTo("Bob"));
    }
}
