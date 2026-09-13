using System.ComponentModel;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The Expander's own logic - what it does regardless of how a theme dresses it: folding, the events that
/// announce it, the keys, and the refusal to fold a section that must stay open.</summary>
[TestFixture]
public class ExpanderTests
{
    [Test]
    public void ItStartsFoldedAndTogglesBothWays()
    {
        var expander = new Expander { Header = "Transform" };

        Assert.That(expander.IsExpanded, Is.False, "a section opens folded - a page of them must not come up unrolled");

        Assert.That(expander.Toggle(), Is.True);
        Assert.That(expander.IsExpanded, Is.True);

        Assert.That(expander.Toggle(), Is.True);
        Assert.That(expander.IsExpanded, Is.False);
    }

    [Test]
    public void EachChangeIsAnnouncedOnce()
    {
        var expander = new Expander();
        int expanded = 0, collapsed = 0;
        expander.Expanded += (_, _) => expanded++;
        expander.Collapsed += (_, _) => collapsed++;

        expander.IsExpanded = true;
        expander.IsExpanded = true;   // same value: nothing happened, so nothing is announced
        expander.IsExpanded = false;

        Assert.Multiple(() =>
        {
            Assert.That(expanded, Is.EqualTo(1));
            Assert.That(collapsed, Is.EqualTo(1));
        });
    }

    // A section that must stay open still wants its header - and must not be foldable by any route, including the
    // keyboard. Opening one is always allowed: CanCollapse says "cannot be closed", not "cannot be touched".
    [Test]
    public void ASectionThatCannotCollapseRefusesToClose()
    {
        var expander = new Expander { CanCollapse = false };

        Assert.That(expander.Toggle(), Is.True, "still opens");
        Assert.That(expander.IsExpanded, Is.True);

        Assert.That(expander.Toggle(), Is.False, "and refuses to close again");
        Assert.That(expander.IsExpanded, Is.True);
    }

    [Test]
    [TestCase(Key.Space)]
    [TestCase(Key.Enter)]
    public void SpaceAndEnterFoldIt(Key key)
    {
        var expander = new Expander();
        var args = new KeyEventArgs(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };

        expander.RaiseEvent(args);

        Assert.Multiple(() =>
        {
            Assert.That(expander.IsExpanded, Is.True);
            Assert.That(args.Handled, Is.True, "claimed, so the key does not travel on to a parent");
        });
    }

    [Test]
    public void ADisabledExpanderIgnoresTheKeyboard()
    {
        var expander = new Expander { IsEnabled = false };
        var args = new KeyEventArgs(KeyboardDevice.CurrentDevice, Key.Space, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };

        expander.RaiseEvent(args);

        Assert.That(expander.IsExpanded, Is.False);
    }

    [Test]
    public void ItIsAKeyboardStop()
    {
        Assert.That(new Expander().Focusable, Is.True, "a header that answers Space has to be reachable by Tab");
    }

    private sealed class Fold : INotifyPropertyChanged
    {
        private bool _open;

        public bool Open
        {
            get => _open;
            set
            {
                _open = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Open)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    // Folding state belongs in a view-model: which groups an inspector has open is the user's, and it has to survive
    // the panel being rebuilt. The binding is two-way by default, so the header carries the state BOTH ways - and it
    // has to keep carrying it after the user has clicked, which is what a Local value written by the click would end.
    [Test]
    public void ItsFoldingStateBindsBothWays()
    {
        var model = new Fold();
        var expander = new Expander { DataContext = model };
        expander.SetBinding("IsExpanded", new Binding("Open"));

        Assert.That(expander.IsExpanded, Is.False, "starts where the model is");

        // Source changes are batched and applied on the frame's flush - see BindingBatchingTests.
        model.Open = true;
        BindingUpdateQueue.Flush();
        Assert.That(expander.IsExpanded, Is.True, "the model opens it");

        expander.Toggle();
        Assert.That(model.Open, Is.False, "and a click writes back");

        model.Open = true;
        BindingUpdateQueue.Flush();
        Assert.That(expander.IsExpanded, Is.True, "the model still moves it AFTER a click - the binding survived");
    }
}
