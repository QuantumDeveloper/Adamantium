using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;

namespace Adamantium.MVVM.Tests;

[TestFixture]
public class MvvmFunctionalTests
{
    [Test]
    public void Bindable_RaisesPropertyChanged_AndCallsHook()
    {
        var vm = new PersonViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.FirstName = "Alice";

        Assert.That(vm.FirstName, Is.EqualTo("Alice"));
        Assert.That(raised, Does.Contain("FirstName"));
        Assert.That(vm.LastChangedProperty, Is.EqualTo("FirstName"));   // OnFirstNameChanged hook fired
    }

    [Test]
    public void Bindable_SameValue_DoesNotNotify()
    {
        var vm = new PersonViewModel { FirstName = "A" };
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.FirstName = "A";

        Assert.That(count, Is.Zero);
    }

    [Test]
    public void Affects_NotifiesDependentProperty()
    {
        var vm = new PersonViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.LastName = "Smith";

        Assert.That(raised, Does.Contain("LastName"));
        Assert.That(raised, Does.Contain("FullName"));
    }

    [Test]
    public void Command_RespectsCanExecute()
    {
        var vm = new PersonViewModel();

        Assert.That(vm.SaveCommand.CanExecute(), Is.False);   // FirstName empty
        vm.SaveCommand.Execute();
        Assert.That(vm.SaveCount, Is.Zero);

        vm.FirstName = "A";
        Assert.That(vm.SaveCommand.CanExecute(), Is.True);
        vm.SaveCommand.Execute();
        Assert.That(vm.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void Command_IsCachedInstance()
    {
        var vm = new PersonViewModel();
        Assert.That(vm.SaveCommand, Is.SameAs(vm.SaveCommand));   // lazy, single instance
    }

    [Test]
    public void ViewModelAttribute_InjectsInpc()
    {
        var vm = new StandaloneViewModel();
        Assert.That(vm, Is.InstanceOf<INotifyPropertyChanged>());

        var raised = new List<string>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.Counter = 5;

        Assert.That(vm.Counter, Is.EqualTo(5));
        Assert.That(raised, Does.Contain("Counter"));
    }
}
