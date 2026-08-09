using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A minimized ribbon keeps its strip and puts the open tab's groups in a flyout. The rule that matters is
/// that minimizing is a change of WHERE the tab is shown, not of WHICH tab is open.</summary>
[TestFixture]
public class RibbonMinimizeTests
{
    private static ContentPresenter _host;
    private static Border _bandHost;
    private static Border _flyoutHost;
    private static Popup _flyout;

    // The ribbon's own template reduced to the parts its code looks up. The content host starts inside the band, the
    // way the theme has it.
    private static ControlTemplate RibbonTemplate() => new(() =>
    {
        var grid = new Grid();
        var presenter = new ItemsPresenter();

        _host = new ContentPresenter();
        _bandHost = new Border { Child = _host };
        _flyoutHost = new Border();
        _flyout = new Popup { Child = _flyoutHost };

        grid.Children.Add(presenter);
        grid.Children.Add(_bandHost);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_ItemsPresenter", presenter);
        result.RegisterName("PART_SelectedContentHost", _host);
        result.RegisterName("PART_BandHost", _bandHost);
        result.RegisterName("PART_FlyoutHost", _flyoutHost);
        result.RegisterName("PART_Flyout", _flyout);
        return result;
    });

    private static Ribbon WithTabs(params string[] headers)
    {
        var ribbon = new Ribbon { Template = RibbonTemplate() };
        foreach (var header in headers) ribbon.Items.Add(new RibbonTab { Header = header });

        ribbon.Measure(new Size(800, 200));
        ribbon.Arrange(new Rect(0, 0, 800, 200));
        return ribbon;
    }

    private static void Press(RibbonTabHeader header, int clickCount)
    {
        ((IObservableComponent)header).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0)
        { RoutedEvent = InputUIComponent.MouseLeftButtonDownEvent, ClickCount = clickCount });
    }

    private static RibbonTabHeader HeaderAt(Ribbon ribbon, int index) =>
        (RibbonTabHeader)ribbon.ItemContainerGenerator.ContainerFromIndex(index);

    // The whole point: the band goes away, the open tab does not change. Anything else would rearrange the window every
    // time someone wanted more room.
    [Test]
    public void Minimizing_DoesNotChangeWhichTabIsOpen()
    {
        var ribbon = WithTabs("Home", "View", "Help");
        ribbon.SelectedIndex = 2;

        ribbon.IsMinimized = true;

        Assert.Multiple(() =>
        {
            Assert.That(ribbon.SelectedIndex, Is.EqualTo(2));
            Assert.That(((RibbonTab)ribbon.SelectedContent).Header, Is.EqualTo("Help"));
        });
    }

    // The tab is MOVED rather than rebuilt: its groups keep the variants and widths they worked out, the same trade a
    // collapsed group makes.
    [Test]
    public void Minimizing_MovesTheOpenTabIntoTheFlyout()
    {
        var ribbon = WithTabs("Home", "View");
        Assert.That(_host.VisualParent, Is.SameAs(_bandHost), "precondition: the tab starts in the band");

        ribbon.IsMinimized = true;

        Assert.That(_host.VisualParent, Is.SameAs(_flyoutHost));
    }

    [Test]
    public void Restoring_BringsTheTabBackIntoTheBand()
    {
        var ribbon = WithTabs("Home", "View");
        ribbon.IsMinimized = true;

        ribbon.IsMinimized = false;

        Assert.Multiple(() =>
        {
            Assert.That(_host.VisualParent, Is.SameAs(_bandHost));
            Assert.That(_flyout.IsOpen, Is.False, "the flyout has nothing left to show");
        });
    }

    // Double-click is the gesture Office trained everyone on, and the first click of it has already opened the tab.
    [Test]
    public void DoubleClickingAHeader_TogglesTheBand()
    {
        var ribbon = WithTabs("Home", "View");

        Press(HeaderAt(ribbon, 0), 2);
        Assert.That(ribbon.IsMinimized, Is.True);

        Press(HeaderAt(ribbon, 0), 2);
        Assert.That(ribbon.IsMinimized, Is.False);
    }

    // While minimized a header press is the way BACK to the commands - it drops them over the content instead of
    // restoring the band, so the window below keeps its size.
    [Test]
    public void AHeaderPressWhileMinimized_DropsTheGroupsDown()
    {
        var ribbon = WithTabs("Home", "View");
        ribbon.IsMinimized = true;

        Press(HeaderAt(ribbon, 1), 1);

        Assert.Multiple(() =>
        {
            Assert.That(_flyout.IsOpen, Is.True);
            Assert.That(ribbon.SelectedIndex, Is.EqualTo(1), "and it opens the tab that was pressed");
            Assert.That(ribbon.IsMinimized, Is.True, "the band itself stays away");
        });
    }

    // ...and pressing the SAME header again puts them away, so the gesture is its own undo.
    [Test]
    public void PressingTheShowingTabAgain_PutsTheGroupsAway()
    {
        var ribbon = WithTabs("Home", "View");
        ribbon.IsMinimized = true;

        Press(HeaderAt(ribbon, 0), 1);
        Assert.That(_flyout.IsOpen, Is.True, "precondition: the groups are showing");

        Press(HeaderAt(ribbon, 0), 1);

        Assert.That(_flyout.IsOpen, Is.False);
    }

    // Pressing a DIFFERENT header while the flyout is showing switches tabs inside it rather than closing it - the
    // press means "show me this one", and it already looks open.
    [Test]
    public void PressingAnotherHeaderWhileShowing_SwitchesTheTabAndStaysOpen()
    {
        var ribbon = WithTabs("Home", "View");
        ribbon.IsMinimized = true;
        Press(HeaderAt(ribbon, 0), 1);

        Press(HeaderAt(ribbon, 1), 1);

        Assert.Multiple(() =>
        {
            Assert.That(_flyout.IsOpen, Is.True);
            Assert.That(ribbon.SelectedIndex, Is.EqualTo(1));
        });
    }

    // A press on a header while the band is OPEN must not conjure a flyout - the groups are already on screen.
    [Test]
    public void AHeaderPressWhileTheBandIsOpen_ShowsNoFlyout()
    {
        var ribbon = WithTabs("Home", "View");

        Press(HeaderAt(ribbon, 1), 1);

        Assert.Multiple(() =>
        {
            Assert.That(_flyout.IsOpen, Is.False);
            Assert.That(ribbon.SelectedIndex, Is.EqualTo(1));
        });
    }
}
