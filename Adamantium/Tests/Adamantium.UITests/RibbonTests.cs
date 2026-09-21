using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU tests for the ribbon's core rules (no GPU, no theme): the strip's containers, what the groups area shows,
// how a group packs commands into columns, and where a command's drawn size comes from.
[TestFixture]
public class RibbonTests
{
    // The ribbon's own template, reduced to the two parts its code looks up.
    private static ControlTemplate RibbonTemplate() => new(() =>
    {
        var grid = new Grid();
        var presenter = new ItemsPresenter();
        var host = new ContentPresenter();
        grid.Children.Add(presenter);
        grid.Children.Add(host);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_ItemsPresenter", presenter);
        result.RegisterName("PART_SelectedContentHost", host);
        return result;
    });

    private static Ribbon WithTabs(params string[] headers)
    {
        var ribbon = new Ribbon { Template = RibbonTemplate() };
        foreach (var header in headers) ribbon.Items.Add(new RibbonTab { Header = header });
        return ribbon;
    }

    // A ribbon always has a tab open: there is no "no tab" state for a command band to show.
    [Test]
    public void TheFirstTab_IsOpenAsSoonAsThereIsOne()
    {
        var ribbon = WithTabs("Home", "View");

        Assert.Multiple(() =>
        {
            Assert.That(ribbon.SelectedIndex, Is.EqualTo(0));
            Assert.That(((RibbonTab)ribbon.SelectedContent).Header, Is.EqualTo("Home"));
        });
    }

    // The groups area shows the TAB; the strip therefore holds headers standing for the tabs, not the tabs.
    [Test]
    public void TheStripsContainers_AreHeaders_NotTheTabs()
    {
        var ribbon = WithTabs("Home", "View");
        ribbon.Measure(new Size(800, 200));
        ribbon.Arrange(new Rect(0, 0, 800, 200));

        var container = ribbon.ItemContainerGenerator.ContainerFromIndex(0);

        Assert.Multiple(() =>
        {
            Assert.That(container, Is.InstanceOf<RibbonTabHeader>());
            Assert.That(((RibbonTabHeader)container).Content, Is.EqualTo("Home"), "the header shows the tab's label");
            Assert.That(ribbon.SelectedContent, Is.SameAs(ribbon.Items[0]), "and the tab itself is the body");
        });
    }

    // Selecting reflects onto the strip: exactly one header is lit, and it is the open tab's.
    [Test]
    public void SelectingATab_LightsItsHeaderAndOnlyThatOne()
    {
        var ribbon = WithTabs("Home", "View", "Help");
        ribbon.Measure(new Size(800, 200));
        ribbon.Arrange(new Rect(0, 0, 800, 200));

        ribbon.SelectedIndex = 2;

        var lit = Enumerable.Range(0, 3)
            .Select(i => (RibbonTabHeader)ribbon.ItemContainerGenerator.ContainerFromIndex(i))
            .Select(h => h.IsSelected)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(lit, Is.EqualTo(new[] { false, false, true }));
            Assert.That(((RibbonTab)ribbon.SelectedContent).Header, Is.EqualTo("Help"));
        });
    }

    // The last group has nothing to its right, and only the tab knows which one that is.
    [Test]
    public void TheLastGroup_DrawsNoDividingRule()
    {
        var tab = new RibbonTab();
        var first = new RibbonGroup { Header = "Clipboard" };
        var last = new RibbonGroup { Header = "Scene" };
        tab.Items.Add(first);
        tab.Items.Add(last);

        Assert.Multiple(() =>
        {
            Assert.That(first.ShowSeparator, Is.True);
            Assert.That(last.ShowSeparator, Is.False);
        });

        tab.Items.Add(new RibbonGroup { Header = "View" });
        Assert.That(last.ShowSeparator, Is.True, "it is no longer the last one");
    }

    private static RibbonGroupPanel PackedGroup(params IMeasurableComponent[] children)
    {
        var panel = new RibbonGroupPanel();
        foreach (var child in children) panel.Children.Add(child);
        panel.Measure(Size.Infinity);
        panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height));
        return panel;
    }

    private static Border Command(RibbonSize max, double width = 20, double height = 20)
    {
        var border = new Border { Width = width, Height = height };
        Ribbon.SetMaxSize(border, max);
        return border;
    }

    // A command's own MaxSize and the ribbon's attached one are ONE storage: the author writes
    // `<RibbonButton MaxSize="Medium"/>`, the group goes on asking Ribbon.GetMaxSize.
    [Test]
    public void ACommandsOwnMaxSize_IsTheRibbonsAttachedOne()
    {
        var button = new RibbonButton();
        var toggle = new RibbonToggleButton();

        button.MaxSize = RibbonSize.Medium;
        Ribbon.SetMaxSize(toggle, RibbonSize.Small);

        Assert.Multiple(() =>
        {
            Assert.That(RibbonButton.MaxSizeProperty, Is.SameAs(Ribbon.MaxSizeProperty), "one property, not a copy");
            Assert.That(RibbonToggleButton.SizeProperty, Is.SameAs(Ribbon.SizeProperty));
            Assert.That(Ribbon.GetMaxSize(button), Is.EqualTo(RibbonSize.Medium), "written as its own, read as attached");
            Assert.That(toggle.MaxSize, Is.EqualTo(RibbonSize.Small), "written as attached, read as its own");
        });
    }

    // ...and both names reach it, which is what a theme trigger and a {TemplateBinding} resolve through.
    [Test]
    public void BothTheBareNameAndTheOwnerPrefixed_ResolveToThatOneProperty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AdamantiumPropertyMap.ResolveProperty(typeof(RibbonButton), "MaxSize"),
                Is.SameAs(Ribbon.MaxSizeProperty));
            Assert.That(AdamantiumPropertyMap.ResolveProperty(typeof(RibbonButton), "Ribbon.MaxSize"),
                Is.SameAs(Ribbon.MaxSizeProperty));
            // A control that is NOT a ribbon command answers the prefixed form - which is why it is attached.
            Assert.That(AdamantiumPropertyMap.ResolveProperty(typeof(Border), "Ribbon.MaxSize"),
                Is.SameAs(Ribbon.MaxSizeProperty));
        });
    }

    // A focus AREA - a region Ctrl+Tab steps between, so the keyboard can leave the band for the page and come back.
    // Declared by a real write in the constructor, as it is for a window and an overlay.
    [Test]
    public void ARibbon_DeclaresItselfAFocusArea()
    {
        Assert.That(KeyboardNavigation.GetIsFocusArea(new Ribbon()), Is.True);
    }

    // Drawn at the largest size its author allows. The author never sets Size - the group does, from the range.
    [Test]
    public void AGroup_DrawsEachCommandAtItsLargestAllowedSize()
    {
        var large = Command(RibbonSize.Large);
        var medium = Command(RibbonSize.Medium);
        PackedGroup(large, medium);

        Assert.Multiple(() =>
        {
            Assert.That(Ribbon.GetSize(large), Is.EqualTo(RibbonSize.Large));
            Assert.That(Ribbon.GetSize(medium), Is.EqualTo(RibbonSize.Medium));
        });
    }

    // The packing that makes a ribbon read as a ribbon: a large command owns a column, smaller ones stack three deep.
    [Test]
    public void SmallCommandsStackThreeDeep_ALargeOneOwnsItsColumn()
    {
        var large = Command(RibbonSize.Large, 40, 60);
        var a = Command(RibbonSize.Medium, 30, 20);
        var b = Command(RibbonSize.Medium, 30, 20);
        var c = Command(RibbonSize.Medium, 30, 20);
        var d = Command(RibbonSize.Medium, 30, 20);
        var panel = PackedGroup(large, a, b, c, d);

        Assert.Multiple(() =>
        {
            // Column 0 = the large one, column 1 = a/b/c stacked, column 2 = d (the fourth starts a new run).
            Assert.That(large.Bounds.X, Is.EqualTo(0));
            Assert.That(a.Bounds.X, Is.EqualTo(40));
            Assert.That(b.Bounds, Is.EqualTo(new Rect(40, 20, 30, 20)));
            Assert.That(c.Bounds, Is.EqualTo(new Rect(40, 40, 30, 20)));
            // A fourth small command starts a new column, on the SAME top line as the others.
            Assert.That(d.Bounds, Is.EqualTo(new Rect(70, 0, 30, 20)), "a fourth small command starts a new column");
            Assert.That(panel.DesiredSize.Width, Is.EqualTo(100));
            Assert.That(panel.DesiredSize.Height, Is.EqualTo(60), "the tallest column is the group's height");
        });
    }

    // The BLOCK is centred in the band and every column starts on that one line. Centring each column separately looks
    // right for two full columns and wrong the moment they differ: a column of one command floats at the middle while
    // the column beside it stacks three from the top, and the rows stop lining up.
    [Test]
    public void ColumnsShareOneTopLine_AndTheBlockIsCentred()
    {
        var large = Command(RibbonSize.Large, 40, 50);
        var a = Command(RibbonSize.Medium, 30, 20);
        var b = Command(RibbonSize.Medium, 30, 20);
        var c = Command(RibbonSize.Medium, 30, 20);
        var lone = Command(RibbonSize.Medium, 30, 20);

        var panel = new RibbonGroupPanel();
        foreach (var child in new IMeasurableComponent[] { large, a, b, c, lone }) panel.Children.Add(child);
        panel.Measure(Size.Infinity);
        panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, 80));   // a band taller than the tallest column

        Assert.Multiple(() =>
        {
            Assert.That(a.Bounds.Y, Is.EqualTo(10), "the tallest column is 60 in an 80 band -> 10 above and below");
            Assert.That(large.Bounds.Y, Is.EqualTo(10), "and the large one starts on the same line");
            Assert.That(lone.Bounds.Y, Is.EqualTo(10), "as does a column holding a single command");
            Assert.That(b.Bounds.Y, Is.EqualTo(30), "rows stack from there");
        });
    }

    // A separator ends the run beside it, so what follows starts a fresh column.
    [Test]
    public void ASeparator_BreaksTheColumnRun()
    {
        var a = Command(RibbonSize.Small, 20, 20);
        var separator = new Separator { Width = 6, Height = 20 };
        var b = Command(RibbonSize.Small, 20, 20);
        PackedGroup(a, separator, b);

        Assert.Multiple(() =>
        {
            Assert.That(a.Bounds.X, Is.EqualTo(0));
            Assert.That(separator.Bounds.X, Is.EqualTo(20), "the separator takes a column of its own");
            Assert.That(b.Bounds, Is.EqualTo(new Rect(26, 0, 20, 20)), "and the run starts again after it");
        });
    }

    // --- The band is a fixed height, and a group fills it -------------------------------------------------------------

    private const double BandHeight = 106;
    private const double GroupBottomPadding = 3;   // the theme's RibbonGroup padding "6 6 6 3"

    // What the theme puts between the band and a group: a fixed-height Border, a stretching ContentPresenter showing the
    // OPEN TAB, the tab's groups in a row. The ribbon itself decides nothing about height, so it is left out.
    private static (Border Band, ContentPresenter Host) Band()
    {
        // Both axes stated exactly as the theme states them: Left across, Stretch down.
        var host = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var band = new Border { Height = BandHeight, Width = 400, Child = host };
        return (band, host);
    }

    private static RibbonTab TabWithGroup(string header, double commandHeight = 60)
    {
        var tab = new RibbonTab
        {
            Header = header,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult { RootComponent = new RibbonGroupsPanel() }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };
        tab.Items.Add(GroupWithCaption(header + " group", commandHeight));
        return tab;
    }

    private static RibbonGroup GroupWithCaption(string caption, double commandHeight)
    {
        var group = new RibbonGroup
        {
            Header = caption,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult { RootComponent = new RibbonGroupPanel() }),
            // Mirrors the theme's RibbonGroup template exactly - the rule beside the commands included. A reduction that
            // drops a piece is a reduction that can be green while the real thing is wrong.
            Template = new ControlTemplate(() =>
            {
                var outer = new Grid();
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var inner = new Grid { Margin = new Thickness(6, 6, 6, 3) };
                inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var presenter = new ItemsPresenter
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                // The caption carries REAL text: an empty presenter is zero-high and would sit on the floor whatever
                // the group did around it.
                var header = new ContentPresenter
                {
                    Content = caption,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 4, 0, 0),
                    FontSize = 11
                };
                Grid.SetRow(header, 1);
                inner.Children.Add(presenter);
                inner.Children.Add(header);

                var rule = new Adamantium.UI.Controls.Shapes.Rectangle
                {
                    Width = 1,
                    Margin = new Thickness(1, 8, 1, 8),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(rule, 1);

                outer.Children.Add(inner);
                outer.Children.Add(rule);

                var result = new TemplateResult { RootComponent = outer };
                result.RegisterName("PART_ItemsPresenter", presenter);
                result.RegisterName("PART_Caption", header);
                return result;
            })
        };
        group.Items.Add(Command(RibbonSize.Large, 40, commandHeight));
        return group;
    }

    // The real layout pass, not a bare Measure/Arrange: swapping the open tab invalidates the NEW child, and a hand-driven
    // pass on an unchanged parent short-circuits before it ever reaches it.
    private static void Layout(Border band)
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(band);
    }

    private static double GroupHeight(RibbonTab tab) => ((RibbonGroup)tab.Items[0]).Bounds.Height;

    // How far the caption's bottom sits above the group's bottom - 0 means it is on the band's floor, which is where a
    // group caption belongs.
    private static double CaptionGapFromFloor(RibbonTab tab)
    {
        var group = (RibbonGroup)tab.Items[0];
        var caption = (ContentPresenter)group.GetTemplateChild("PART_Caption");

        double bottom = caption.Bounds.Y + caption.Bounds.Height;
        for (IUIComponent v = caption.VisualParent; v != null && !ReferenceEquals(v, group); v = v.VisualParent)
            bottom += v.Bounds.Y;

        return group.Bounds.Height - bottom;
    }

    // The band is a METRIC, and its default has to be a real number. Left at NaN it sizes itself to whichever tab is
    // open, and the caption hanging off its floor then rides up and down as the open tab's commands change height -
    // which is what "the group caption creeps up every time I switch tabs" is.
    [Test]
    public void TheBandHeight_HasAConcreteDefault()
    {
        var height = new Ribbon().GroupsAreaHeight;

        Assert.Multiple(() =>
        {
            Assert.That(double.IsNaN(height), Is.False, "a NaN band collapses onto the open tab's content");
            Assert.That(height, Is.EqualTo(Ribbon.DefaultGroupsAreaHeight));
        });
    }

    // Two tabs whose commands are NOT the same height still put their captions on one line, because the band they sit
    // in does not depend on them.
    [Test]
    public void TabsWithDifferentCommandHeights_KeepTheCaptionOnTheSameLine()
    {
        var (band, host) = Band();
        var tall = TabWithGroup("Home", commandHeight: 70);
        var short_ = TabWithGroup("View", commandHeight: 30);

        host.Content = tall;
        Layout(band);
        var tallGap = CaptionGapFromFloor(tall);

        host.Content = short_;
        Layout(band);
        var shortGap = CaptionGapFromFloor(short_);

        Assert.Multiple(() =>
        {
            Assert.That(tallGap, Is.EqualTo(GroupBottomPadding));
            Assert.That(shortGap, Is.EqualTo(GroupBottomPadding), "a tab with shorter commands does not lift its caption");
        });
    }

    private static double CaptionTop(RibbonTab tab)
    {
        var group = (RibbonGroup)tab.Items[0];
        return Top((ContentPresenter)group.GetTemplateChild("PART_Caption"), group);
    }

    // A command taller than the row the group can spare it (a Large drop-down carries a third row for its chevron) must
    // not drag the caption up with it: the caption's line belongs to the BAND, not to whatever the commands need.
    [Test]
    public void ACommandTallerThanItsRow_DoesNotLiftTheCaption()
    {
        var (band, host) = Band();
        var tab = TabWithGroup("Modeling", commandHeight: 87);   // taller than the band leaves for commands
        host.Content = tab;
        Layout(band);

        var group = (RibbonGroup)tab.Items[0];
        Assert.That(CaptionGapFromFloor(tab), Is.EqualTo(GroupBottomPadding),
            $"group={group.Bounds} groupDesired={group.DesiredSize} captionTop={CaptionTop(tab)}");
    }

    // "A couple of pixels on every visit": re-opening a tab has to put its caption back EXACTLY where it was. A drift
    // that small is invisible for one switch and unmistakable after ten.
    [Test]
    public void ReOpeningATab_PutsTheCaptionBackExactlyWhereItWas()
    {
        var (band, host) = Band();
        var home = TabWithGroup("Home");
        var away = TabWithGroup("View");

        var tops = new List<double>();
        for (var visit = 0; visit < 4; visit++)
        {
            host.Content = home;
            Layout(band);
            tops.Add(CaptionTop(home));

            host.Content = away;
            Layout(band);
        }

        Assert.That(tops.Distinct().Count(), Is.EqualTo(1),
            "caption top on each visit: " + string.Join(", ", tops));
    }

    // The other way a caption and its commands end up on one line: the caption stays put and the COMMANDS come down
    // onto it, because a column taller than the row it was given overflows instead of being held above the caption.
    [Test]
    public void CommandsTallerThanTheirRow_DoNotOverlapTheCaption()
    {
        var (band, host) = Band();
        var tab = TabWithGroup("Home", commandHeight: 200);   // far taller than the band can hold
        host.Content = tab;
        Layout(band);

        var group = (RibbonGroup)tab.Items[0];
        var caption = (ContentPresenter)group.GetTemplateChild("PART_Caption");
        var commands = (ItemsPresenter)group.GetTemplateChild("PART_ItemsPresenter");

        Assert.That(Bottom(commands, group), Is.LessThanOrEqualTo(Top(caption, group)),
            "the commands must stop where the caption begins");
    }

    private static double Top(IUIComponent part, RibbonGroup group)
    {
        double y = part.Bounds.Y;
        for (var v = part.VisualParent; v != null && !ReferenceEquals(v, group); v = v.VisualParent) y += v.Bounds.Y;
        return y;
    }

    private static double Bottom(IUIComponent part, RibbonGroup group) => Top(part, group) + part.Bounds.Height;

    // The caption hangs off the BOTTOM of the group, so a group shorter than the band pulls its caption up off the
    // band's floor - and the captions of two groups stop sharing a line.
    [Test]
    public void AGroup_FillsTheBandHeight()
    {
        var (band, host) = Band();
        var tab = TabWithGroup("Home");
        host.Content = tab;
        Layout(band);

        Assert.Multiple(() =>
        {
            Assert.That(GroupHeight(tab), Is.EqualTo(BandHeight));
            Assert.That(CaptionGapFromFloor(tab), Is.EqualTo(GroupBottomPadding), "the caption sits on the band's floor");
        });
    }

    // The band is a constant, so switching tabs cannot change how tall a group is - and re-opening one must not leave it
    // shorter than it was the first time.
    [Test]
    public void SwitchingTabs_LeavesTheGroupHeightAlone()
    {
        var (band, host) = Band();
        var home = TabWithGroup("Home");
        var view = TabWithGroup("View");

        host.Content = home;
        Layout(band);
        var first = GroupHeight(home);

        host.Content = view;
        Layout(band);
        var second = GroupHeight(view);

        var secondGap = CaptionGapFromFloor(view);

        host.Content = home;
        Layout(band);
        var back = GroupHeight(home);
        var backGap = CaptionGapFromFloor(home);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(BandHeight));
            Assert.That(second, Is.EqualTo(BandHeight), "the tab switched to gets the same band");
            Assert.That(back, Is.EqualTo(BandHeight), "and re-opening the first one does not shrink it");
            Assert.That(secondGap, Is.EqualTo(GroupBottomPadding), "the caption of the tab switched to is on the floor");
            Assert.That(backGap, Is.EqualTo(GroupBottomPadding), "and so is the re-opened one's - it does not creep up per switch");
        });
    }

    // Groups sit in a row at their OWN widths - a virtualizing stack would give every one a single probed extent.
    [Test]
    public void GroupsAreLaidOutAtTheirOwnWidths()
    {
        var panel = new RibbonGroupsPanel();
        var narrow = new Border { Width = 30, Height = 50 };
        var wide = new Border { Width = 120, Height = 50 };
        panel.Children.Add(narrow);
        panel.Children.Add(wide);

        panel.Measure(new Size(double.PositiveInfinity, 100));
        panel.Arrange(new Rect(0, 0, 150, 100));

        Assert.Multiple(() =>
        {
            Assert.That(panel.DesiredSize.Width, Is.EqualTo(150));
            Assert.That(narrow.Bounds.Width, Is.EqualTo(30));
            Assert.That(wide.Bounds.X, Is.EqualTo(30));
            Assert.That(wide.Bounds.Width, Is.EqualTo(120));
        });
    }
}
