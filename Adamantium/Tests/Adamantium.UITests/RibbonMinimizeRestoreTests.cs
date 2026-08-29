using System;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Minimizing the ribbon MOVES the open tab's content into the flyout rather than rebuilding it, so the groups keep the
/// variants and widths they worked out. Restoring has to move it back, and the band that was collapsed has to come
/// back with it.
/// <para>Both halves are covered here, and the SECOND is the one worth having: the move is driven by the control and
/// was easy to believe broken, while hiding the band is done by a trigger in the THEME - which is why the same failure
/// shows in both themes. A double that leaves the trigger out passes happily while the application is broken.</para>
/// </summary>
[TestFixture]
public class RibbonMinimizeRestoreTests
{
    private Window _window;
    private Ribbon _ribbon;

    // Shaped like both themes': the band host sits inside a Band that the theme's trigger collapses.
    private static ControlTemplate RibbonTemplate() => new(() =>
    {
        var content = new ContentPresenter();
        var bandHost = new Border { Child = content };
        var band = new Border { Child = bandHost };
        // The transform the themes put here is what the band slides on; without it this double cannot see the slide.
        var flyoutHost = new Border { RenderTransform = new Adamantium.UI.Core.Media.Transform() };
        var popup = new Popup { Child = flyoutHost };

        var root = new StackPanel();
        root.Children.Add(band);
        root.Children.Add(popup);

        var result = new TemplateResult { RootComponent = root };
        result.RegisterName("PART_SelectedContentHost", content);
        result.RegisterName("PART_BandHost", bandHost);
        result.RegisterName("PART_FlyoutHost", flyoutHost);
        result.RegisterName("PART_Flyout", popup);
        result.RegisterName("Band", band);
        return result;
    });

    // The theme's rule, stated the way the theme states it.
    private static Style CollapseTheBandWhenMinimized()
    {
        var style = new Style { Selector = new StyleSelector() };
        style.Selector.Types.Add(typeof(Ribbon));

        var trigger = new PropertyTrigger { Property = nameof(Ribbon.IsMinimized), Value = true, Setters = [] };
        trigger.Setters.Add(new Setter { TargetName = "Band", Property = "Visibility", Value = Visibility.Collapsed });
        style.Triggers.Add(trigger);

        return style;
    }

    [SetUp]
    public void Setup()
    {
        _ribbon = new Ribbon { Template = RibbonTemplate() };
        _ribbon.Styles.Add(CollapseTheBandWhenMinimized());

        _window = new Window { Width = 800, Height = 400, Content = _ribbon };
        Pump();
    }

    private void Pump()
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(_window);
    }

    private Border Band => (Border)_ribbon.GetTemplateChild("Band");
    private Border BandHost => (Border)_ribbon.GetTemplateChild("PART_BandHost");
    private Border FlyoutHost => (Border)_ribbon.GetTemplateChild("PART_FlyoutHost");
    private ContentPresenter Content => (ContentPresenter)_ribbon.GetTemplateChild("PART_SelectedContentHost");

    /// <summary>The reported bug, in the shape it was reported: hide the band, show it again, and the commands never
    /// come back.</summary>
    [Test]
    public void TheBandComesBackWhenTheRibbonIsRestored()
    {
        _ribbon.IsMinimized = true;
        Pump();

        Assert.That(Band.Visibility, Is.EqualTo(Visibility.Collapsed), "minimizing did not hide the band at all");

        _ribbon.IsMinimized = false;
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(Band.Visibility, Is.EqualTo(Visibility.Visible),
                "the band stayed collapsed - the trigger that hid it was never withdrawn");
            Assert.That(BandHost.Child, Is.SameAs(Content));
            Assert.That(Content.LogicalParent, Is.SameAs(BandHost));
        });
    }

    /// <summary>One owner at a time, in both directions. A control claimed by two parents renders under whichever one
    /// happened to take it last.</summary>
    [Test]
    public void OnlyOneHostEverHoldsTheContent()
    {
        Assert.That(FlyoutHost.Child, Is.Null, "before anything is minimized the flyout holds nothing");

        _ribbon.IsMinimized = true;
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(FlyoutHost.Child, Is.SameAs(Content));
            Assert.That(BandHost.Child, Is.Null, "the band still claims content the flyout has taken");
            Assert.That(Content.LogicalParent, Is.SameAs(FlyoutHost));
        });
    }

    /// <summary>The sequence a person actually performs: minimize, OPEN the band - that is what a minimized ribbon is
    /// for - then restore. The move back happens while the popup is still open and the popup is closed right after,
    /// so anything the closing tears down, it tears down from content that by then belongs to the band.</summary>
    [Test]
    public void ItComesBackAfterTheFlyoutHasActuallyBeenOpened()
    {
        var popup = (Popup)_ribbon.GetTemplateChild("PART_Flyout");

        _ribbon.IsMinimized = true;
        popup.IsOpen = true;
        Pump();

        _ribbon.IsMinimized = false;
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(Band.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(BandHost.Child, Is.SameAs(Content), "the band host lost the content when the flyout closed");
            Assert.That(Content.LogicalParent, Is.SameAs(BandHost));
        });
    }

    /// <summary>The presenter carries a LIVE control - the open tab itself, which is also an item of the ribbon - and
    /// moving the presenter between hosts must not cost it that content. A presenter that rebuilds on re-parenting
    /// would leave the tab belonging to nothing, which is a band that shows exactly nothing while every other check
    /// still passes.</summary>
    [Test]
    public void TheContentInsideThePresenterSurvivesTheMove()
    {
        var page = new Border { Width = 40, Height = 20 };
        Content.Content = page;
        Pump();

        _ribbon.IsMinimized = true;
        Pump();
        _ribbon.IsMinimized = false;
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(Content.Content, Is.SameAs(page), "the presenter lost what it was showing");
            Assert.That(page.LogicalParent, Is.Not.Null, "the tab came back belonging to nothing - it cannot draw");
        });
    }

    /// <summary>The band SLIDES in, and the slide is armed by parking it off-screen and letting the popup's first layer
    /// pass start the motion. That pass only comes when the popup is actually (re)opened - and clicking a DIFFERENT tab
    /// header while the band is already showing asks it to open again, which for an already-open popup is not a change
    /// at all. So nothing ever brought it back: the popup stayed up and its contents sat a band's height above the top
    /// of the clip. What that looks like is commands vanishing from a flyout that is plainly still there, after nothing
    /// more than clicking between tabs.</summary>
    [Test]
    public void SwitchingTabsWhileTheBandIsShowingDoesNotParkItOffScreen()
    {
        _ribbon.IsMinimized = true;
        _ribbon.ClickTab(new RibbonTabHeader());   // opens it
        Pump();

        var slide = FlyoutHost.RenderTransform;
        Assert.That(slide, Is.Not.Null, "no transform: this template does not exercise the slide at all");

        // The band has arrived. Stated rather than awaited: the motion is started by the popup's first LAYER PASS,
        // which belongs to the real render loop and never runs here - so this is where the application would be after
        // the slide finished, which is the only interesting starting point for what follows.
        slide.TranslateY = 0;

        // Now the second click, on ANOTHER tab, while the band is up.
        _ribbon.ClickTab(new RibbonTabHeader());
        Pump();

        Assert.That(slide.TranslateY, Is.EqualTo(0).Within(0.01),
            "the band was parked off-screen again, and only that first layer pass could have brought it back - so it "
            + "never came: the flyout stays up with its commands a band's height above the clip");
    }

    /// <summary>Restoring puts the flyout away AT ONCE, not over a transition.
    /// <para>The slide is for a band being PUT AWAY. On restore the band is not going anywhere - it has just gone back
    /// into the ribbon - so what would travel down is an empty plate its own height, under the band already showing
    /// above it. Measured on the stand: band 106 visible, popup still open with a 106-tall child, which on screen is a
    /// ribbon twice as tall until the ghost finally leaves.</para></summary>
    [Test]
    public void RestoringPutsTheFlyoutAwayAtOnce()
    {
        var popup = (Popup)_ribbon.GetTemplateChild("PART_Flyout");

        _ribbon.IsMinimized = true;
        popup.IsOpen = true;
        Pump();

        _ribbon.IsMinimized = false;

        Assert.That(popup.IsOpen, Is.False,
            "the flyout is still up while the band is already back - two bands' worth of height until it slides away");
    }

    /// <summary>Going back and forth keeps working - the state must not depend on how many times it was toggled.</summary>
    [Test]
    public void ItSurvivesRepeatedToggling()
    {
        for (var i = 0; i < 4; i++)
        {
            _ribbon.IsMinimized = true;
            Pump();
            _ribbon.IsMinimized = false;
            Pump();
        }

        Assert.Multiple(() =>
        {
            Assert.That(Band.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(BandHost.Child, Is.SameAs(Content));
            Assert.That(FlyoutHost.Child, Is.Null);
        });
    }
}
