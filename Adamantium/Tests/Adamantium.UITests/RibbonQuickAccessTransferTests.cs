using System;
using System.Linq;
using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Extensions;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Putting a ribbon command in the quick-access bar. The bar's collection belongs to the APPLICATION and holds
/// whatever type it chose, so the ribbon never writes into it - it reports the request, says what the command looks
/// like, and the application builds its own kind of item out of that.</summary>
[TestFixture]
public class RibbonQuickAccessTransferTests
{
    // A command that records what it was handed.
    private sealed class Spy : ICommand
    {
        private readonly Action<object> _run;

        public Spy(Action<object> run = null) => _run = run;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter = null) => true;

        public void Execute(object parameter = null) => _run?.Invoke(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static RibbonButton Command(string label = "Save") => new()
    {
        Content = label,
        Icon = "M3,2 L11,2 L13,4 L13,13 L3,13 Z",
        Command = new Spy()
    };

    private static RibbonTab TabWith(string header, params RibbonButton[] commands)
    {
        var group = new RibbonGroup { Header = "Group" };
        foreach (var command in commands) group.Items.Add(command);

        var tab = new RibbonTab { Header = header };
        tab.Items.Add(group);
        return tab;
    }

    // The command is bound ONCE, above, and every command under it finds the same one - a ribbon holds hundreds, and
    // wiring each would be the kind of repetition nobody keeps up. That is what INHERITS buys.
    [Test]
    public void TheCommandBoundAbove_ReachesACommandBeneathIt()
    {
        RibbonQuickAccessEventArgs seen = null;
        var button = Command();
        var band = new StackPanel();
        band.Children.Add(button);
        Ribbon.SetAddToQuickAccessCommand(band, new Spy(a => seen = a as RibbonQuickAccessEventArgs));

        band.Measure(new Size(400, 200));
        band.Arrange(new Rect(0, 0, 400, 200));

        Ribbon.RequestQuickAccess(button, add: true);

        Assert.That(seen, Is.Not.Null, "the request never reached the view model");
    }

    // What crosses over is a DESCRIPTION, not the control: an application storing the control would be holding
    // something a re-template throws away.
    [Test]
    public void TheRequest_CarriesWhatTheCommandLooksLikeAndWhatItDoes()
    {
        RibbonQuickAccessEventArgs seen = null;
        var action = new Spy();
        var button = new RibbonButton { Content = "Save", Icon = "M3,2 L11,2", Command = action, CommandParameter = 42 };
        Ribbon.SetAddToQuickAccessCommand(button, new Spy(a => seen = a as RibbonQuickAccessEventArgs));

        Ribbon.RequestQuickAccess(button, add: true);

        Assert.Multiple(() =>
        {
            Assert.That(seen.Icon, Is.EqualTo("M3,2 L11,2"));
            Assert.That(seen.Action, Is.SameAs(action), "the bar's button has to run what the ribbon's button runs");
            Assert.That(seen.ActionParameter, Is.EqualTo(42));
            Assert.That(seen.Command, Is.SameAs(button), "and the source is there for anything else it needs");
        });
    }

    // The routed event is the other half: a host with code hears it without binding anything.
    [Test]
    public void TheRequest_AlsoRaisesTheRoutedEvent()
    {
        RibbonQuickAccessEventArgs seen = null;
        var button = Command();
        ((IObservableComponent)button).AddHandler(Ribbon.AddToQuickAccessRequestedEvent,
            new EventHandler<RibbonQuickAccessEventArgs>((_, a) => seen = a));

        Ribbon.RequestQuickAccess(button, add: true);

        Assert.That(seen, Is.Not.Null);
    }

    // Removing travels the same road, and must not be confused with adding.
    [Test]
    public void RemovingRunsTheOtherCommand()
    {
        var added = 0;
        var removed = 0;
        var button = Command();
        Ribbon.SetAddToQuickAccessCommand(button, new Spy(_ => added++));
        Ribbon.SetRemoveFromQuickAccessCommand(button, new Spy(_ => removed++));

        Ribbon.RequestQuickAccess(button, add: false);

        Assert.That((added, removed), Is.EqualTo((0, 1)));
    }

    // A command that says it does not belong in the bar is not offered to it, however it was asked.
    [Test]
    public void ACommandThatRefuses_IsNeverHandedOver()
    {
        var asked = 0;
        var button = Command();
        Ribbon.SetCanAddToQuickAccess(button, false);
        Ribbon.SetAddToQuickAccessCommand(button, new Spy(_ => asked++));

        Ribbon.RequestQuickAccess(button, add: true);

        Assert.That(asked, Is.Zero);
    }

    // The ribbon holds NO list of its own: it is POINTED at the application's, and reads its own commands out of it.
    // Nobody writes a mark back - a view model that did would have to keep the ribbon's control to write it on.
    private sealed class BarItem : IQuickAccessItem
    {
        public Adamantium.UI.Core.Templates.DataTemplate QuickAccessTemplate => null;

        public object Key { get; set; }

        public ICommand Action { get; set; }
    }

    [Test]
    public void ACommandIsRecognisedInTheBarByTheCommandItRuns()
    {
        var run = new Spy();
        var button = Command();
        button.Command = run;

        Ribbon.SetQuickAccessItems(button, new object[] { new BarItem { Action = run } });

        Assert.That(Ribbon.IsShownInQuickAccess(button), Is.True);
    }

    [Test]
    public void ACommandThatRunsNothingIsRecognisedByItsKey()
    {
        var toggle = Command();
        Ribbon.SetQuickAccessKey(toggle, "ShowGrid");

        Ribbon.SetQuickAccessItems(toggle, new object[] { new BarItem { Key = "ShowGrid" } });

        Assert.That(Ribbon.IsShownInQuickAccess(toggle), Is.True);
    }

    [Test]
    public void ACommandTheBarDoesNotHoldIsNotInIt()
    {
        var button = Command();
        button.Command = new Spy();
        Ribbon.SetQuickAccessKey(button, "Save");

        Ribbon.SetQuickAccessItems(button, new object[] { new BarItem { Key = "Open", Action = new Spy() } });

        Assert.That(Ribbon.IsShownInQuickAccess(button), Is.False);
    }

    // The bar's own buttons are IN it by standing there - they carry no key of the ribbon's and run the application's
    // command, not the ribbon command's.
    [Test]
    public void AVisualThatStatesItOutrightIsInTheBar()
    {
        var button = Command();

        Assert.That(Ribbon.IsShownInQuickAccess(button), Is.False, "nothing is in the bar until something says so");

        Ribbon.SetIsInQuickAccess(button, true);

        Assert.That(Ribbon.IsShownInQuickAccess(button), Is.True);
    }

    // The one place commands are moved from has to offer the WHOLE band, not the tab that happens to be open: only the
    // open tab is ever realized, so the list is walked over the items rather than over what exists on screen.
    [Test]
    public void TheCandidateList_ReachesCommandsInTabsThatWereNeverOpened()
    {
        var onHome = Command("Save");
        var onView = Command("Wireframe");
        var ribbon = new Ribbon();
        ribbon.Items.Add(TabWith("Home", onHome));
        ribbon.Items.Add(TabWith("View", onView));

        Assert.That(ribbon.QuickAccessCandidates, Is.EquivalentTo(new IUIComponent[] { onHome, onView }));
    }

    // ...and a command that refuses is not offered there either, or the list would promise what the transfer denies.
    [Test]
    public void TheCandidateList_LeavesOutWhatRefuses()
    {
        var offered = Command("Save");
        var refuses = Command("Grid size");
        Ribbon.SetCanAddToQuickAccess(refuses, false);
        var ribbon = new Ribbon();
        ribbon.Items.Add(TabWith("Home", offered, refuses));

        Assert.That(ribbon.QuickAccessCandidates, Is.EquivalentTo(new IUIComponent[] { offered }));
    }

    // The icon is ATTACHED, so a plain control dropped into a group hands one over too - the bar draws what it is
    // given, whatever type gave it.
    [Test]
    public void AnOrdinaryControlInAGroup_CanStateAnIconToo()
    {
        RibbonQuickAccessEventArgs seen = null;
        var slider = new Slider();
        Ribbon.SetIcon(slider, "M2,8 L14,8");
        Ribbon.SetAddToQuickAccessCommand(slider, new Spy(a => seen = a as RibbonQuickAccessEventArgs));

        Ribbon.RequestQuickAccess(slider, add: true);

        Assert.That(seen.Icon, Is.EqualTo("M2,8 L14,8"));
    }
}
