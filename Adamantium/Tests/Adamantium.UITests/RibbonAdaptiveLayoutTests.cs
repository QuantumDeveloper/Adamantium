using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Which variant each group is drawn at as the tab gets narrower. The answer must come from the ARRANGE slot,
/// must degrade in steps rather than clipping, and must not change its mind twice about the same width.</summary>
[TestFixture]
public class RibbonAdaptiveLayoutTests
{
    // A command whose width is decided by the size the group gave it - what a real command's Ribbon.Size triggers do.
    private sealed class SizedCommand : Border
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            Width = Ribbon.GetSize(this) switch
            {
                RibbonSize.Large => 60,
                RibbonSize.Medium => 40,
                _ => 20
            };
            Height = 60;
            return base.MeasureOverride(availableSize);
        }
    }

    /// <summary>What the theme wraps a group's commands in: padding, and the rule down its right edge. A group laid out
    /// at what its COMMANDS cost has nowhere to put this, and the rule walks back into the commands.</summary>
    private const double Chrome = 15;   // 6 + 6 padding, 1 rule, 1 + 1 rule margin

    private static RibbonGroup Group(int commands, int shrinkPriority = 0, bool fixedSize = false)
    {
        var group = new RibbonGroup
        {
            ShrinkPriority = shrinkPriority,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult { RootComponent = new RibbonGroupPanel() }),
            Template = new ControlTemplate(() =>
            {
                var outer = new Grid();
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var presenter = new ItemsPresenter { Margin = new Thickness(6, 6, 6, 3) };
                var rule = new Adamantium.UI.Controls.Shapes.Rectangle
                {
                    Width = 1,
                    Margin = new Thickness(1, 8, 1, 8),
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(rule, 1);

                outer.Children.Add(presenter);
                outer.Children.Add(rule);

                var result = new TemplateResult { RootComponent = outer };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        for (var i = 0; i < commands; i++)
        {
            var command = new SizedCommand();
            if (fixedSize)
            {
                Ribbon.SetCollapseToMedium(command, RibbonCollapseThreshold.Never);
                Ribbon.SetCollapseToSmall(command, RibbonCollapseThreshold.Never);
            }

            group.Items.Add(command);
        }

        return group;
    }

    private static (RibbonGroupsPanel Panel, RibbonGroup[] Groups) Row(params RibbonGroup[] groups)
    {
        var panel = new RibbonGroupsPanel();
        foreach (var group in groups) panel.Children.Add(group);
        return (panel, groups);
    }

    private static void LayoutAt(RibbonGroupsPanel panel, double width)
    {
        panel.Measure(new Size(double.PositiveInfinity, 106));
        panel.Arrange(new Rect(0, 0, width, 106));
    }

    private static IEnumerable<int> Variants(RibbonGroup[] groups) => groups.Select(g => g.CurrentVariant);

    private static double Occupied(RibbonGroup[] groups) => groups.Sum(g => g.WidthAt(g.CurrentVariant));

    // The width a group is laid out at has to be the GROUP's, not its commands'. Missing the chrome, every group is
    // arranged narrower than it needs and the rule down its edge is pushed back over the commands - which is what the
    // separators "sliding" looked like on screen.
    [Test]
    public void AGroupIsArrangedAtItsOwnWidth_ChromeIncluded()
    {
        var (panel, groups) = Row(Group(2), Group(2));
        LayoutAt(panel, 2000);

        Assert.Multiple(() =>
        {
            Assert.That(groups[0].WidthAt(0), Is.EqualTo(groups[0].DesiredSize.Width),
                "the roomiest variant costs exactly what the group asked to be measured at");
            Assert.That(groups[0].Bounds.Width, Is.EqualTo(groups[0].DesiredSize.Width), "and that is what it got");
            Assert.That(groups[1].Bounds.X, Is.EqualTo(groups[0].Bounds.Width), "so the next group starts after it");
        });
    }

    // ...at every variant, not just the roomiest: the chrome does not shrink with the commands.
    [Test]
    public void TheChromeIsCountedAtEveryVariant()
    {
        var (panel, groups) = Row(Group(2));
        LayoutAt(panel, 2000);
        var group = groups[0];

        for (var i = 0; i < group.Variants.Count - 1; i++)
        {
            Assert.That(group.WidthAt(i), Is.GreaterThanOrEqualTo(Chrome),
                $"variant {i} has to leave room for the padding and the rule");
        }
    }

    // A group must be DRAWN at the variant it was LAID OUT at. Two owners for that number is what let a group keep
    // drawing its commands large while the row gave it the narrow slot - so the next group started inside it.
    [Test]
    public void AGroupIsDrawnAtTheVariantItWasLaidOutAt()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);
        LayoutAt(panel, 260);

        foreach (var group in groups)
        {
            var expected = group.Variants[group.CurrentVariant].Sizes;
            var drawn = group.Items.Cast<IMeasurableComponent>().Select(Ribbon.GetSize).ToArray();

            Assert.That(drawn, Is.EqualTo(expected),
                $"group at variant {group.CurrentVariant} is drawing {string.Join(",", drawn)}");
        }
    }

    // ...and the groups therefore do not overlap: each starts where the one before it ended.
    [Test]
    public void GroupsDoNotOverlap_HoweverNarrowTheTab()
    {
        var (panel, groups) = Row(Group(3), Group(3), Group(2));

        foreach (var width in new[] { 2000.0, 600, 400, 260, 160, 400, 2000 })
        {
            LayoutAt(panel, width);

            for (var i = 1; i < groups.Length; i++)
            {
                Assert.That(groups[i].Bounds.X, Is.GreaterThanOrEqualTo(groups[i - 1].Bounds.X + groups[i - 1].Bounds.Width),
                    $"at width {width}, group {i} starts inside group {i - 1}");
            }
        }
    }

    // A layout pass repeated at an unchanged width must not keep changing its mind - that is what a band twitching while
    // a window is dragged looks like from the outside.
    [Test]
    public void RepeatedPassesAtOneWidth_Settle()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 320);
        var first = groups.Select(g => g.CurrentVariant).ToArray();

        for (var pass = 0; pass < 8; pass++)
        {
            LayoutAt(panel, 320);
            Assert.That(groups.Select(g => g.CurrentVariant), Is.EqualTo(first), $"pass {pass} changed its mind");
        }
    }

    // With room to spare every group is drawn the way its author asked - shrinking is something the WIDTH does to the
    // tab, never a default.
    [Test]
    public void WithRoomToSpare_EveryGroupIsAtItsRoomiest()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);

        Assert.That(Variants(groups), Is.All.EqualTo(0));
    }

    // The tab narrows and the layout DEGRADES instead of being clipped: something gives way, and what is left still
    // fits the slot.
    [Test]
    public void ANarrowerTab_ShrinksAGroupInsteadOfOverflowing()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);
        var roomy = Occupied(groups);

        LayoutAt(panel, roomy - 60);

        Assert.Multiple(() =>
        {
            Assert.That(Variants(groups), Is.Not.All.EqualTo(0), "something had to give");
            Assert.That(Occupied(groups), Is.LessThanOrEqualTo(roomy - 60));
        });
    }

    // The END of a tab yields first: the group an author put first keeps its full layout longest.
    [Test]
    public void TheRightmostGroupGivesWayFirst()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);

        LayoutAt(panel, Occupied(groups) - 20);

        Assert.Multiple(() =>
        {
            Assert.That(groups[0].CurrentVariant, Is.EqualTo(0), "the leading group is untouched");
            Assert.That(groups[1].CurrentVariant, Is.GreaterThan(0));
        });
    }

    // ShrinkPriority overrides that: an author names the group they are willing to lose room in.
    [Test]
    public void ShrinkPriority_DecidesWhichGroupGivesWay()
    {
        var (panel, groups) = Row(Group(3, shrinkPriority: -1), Group(3));
        LayoutAt(panel, 2000);

        LayoutAt(panel, Occupied(groups) - 20);

        Assert.Multiple(() =>
        {
            Assert.That(groups[0].CurrentVariant, Is.GreaterThan(0), "the one offered up goes first");
            Assert.That(groups[1].CurrentVariant, Is.EqualTo(0));
        });
    }

    // Room given back is taken back - the degradation is not one-way.
    [Test]
    public void WideningTheTab_RestoresWhatWasGivenUp()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);
        var roomy = Occupied(groups);

        LayoutAt(panel, roomy - 60);
        Assert.That(Variants(groups), Is.Not.All.EqualTo(0), "precondition: something shrank");

        LayoutAt(panel, 2000);

        Assert.That(Variants(groups), Is.All.EqualTo(0));
    }

    // Sweeping the width one way must give a monotonic answer - a band that flickers while a window is dragged is the
    // failure. This passes with the grow-back margin at zero too, so it does NOT stand in for the hysteresis: it says
    // the choice is stable, not that the deadband is what makes it so.
    [Test]
    public void SweepingTheWidthDown_NeverGrowsAGroupBackMidSweep()
    {
        var (panel, groups) = Row(Group(3), Group(3), Group(2));
        LayoutAt(panel, 2000);

        var seen = new List<(double Width, int[] Variants)>();
        for (var width = 700.0; width >= 120; width -= 1)
        {
            LayoutAt(panel, width);
            seen.Add((width, groups.Select(g => g.CurrentVariant).ToArray()));
        }

        for (var i = 1; i < seen.Count; i++)
        {
            for (var g = 0; g < groups.Length; g++)
            {
                Assert.That(seen[i].Variants[g], Is.GreaterThanOrEqualTo(seen[i - 1].Variants[g]),
                    $"group {g} grew back at width {seen[i].Width} (was {seen[i - 1].Variants[g]} " +
                    $"at {seen[i - 1].Width}, now {seen[i].Variants[g]})");
            }
        }
    }

    // The same width twice has to give the same answer, whichever side it was approached from - within the deadband the
    // choice is allowed to differ, outside it, it is not.
    [Test]
    public void ASettledWidth_DoesNotChangeUnderRepeatedPasses()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 300);
        var settled = groups.Select(g => g.CurrentVariant).ToArray();

        for (var pass = 0; pass < 5; pass++) LayoutAt(panel, 300);

        Assert.That(Variants(groups), Is.EqualTo(settled));
    }

    // Everything shrinks before ANYTHING is given up wholesale: a tab with one collapsed group beside three roomy ones
    // has thrown away more than the width asked it to.
    [Test]
    public void SizesAreExhaustedBeforeAnyGroupCollapses()
    {
        var (panel, groups) = Row(Group(3), Group(3));
        LayoutAt(panel, 2000);
        var tightest = groups[0].Variants.Count - 2;

        // Wide enough for two tightest groups, and not a step more.
        LayoutAt(panel, groups[0].WidthAt(tightest) + groups[1].WidthAt(tightest));

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(g => g.IsCollapsed), Is.All.False);
            Assert.That(Variants(groups), Is.All.EqualTo(tightest));
        });
    }

    // Out of size steps and still too wide: a group gives itself up entirely, and its commands go to the flyout its
    // button opens rather than off the edge of the tab. Commands that cannot shrink at all, so collapsing is the only
    // lever there is.
    [Test]
    public void OutOfSizeSteps_AGroupCollapsesRatherThanOverflowing()
    {
        var (panel, groups) = Row(Group(3, fixedSize: true), Group(3, fixedSize: true));
        LayoutAt(panel, 2000);

        LayoutAt(panel, 200);

        Assert.That(groups.Select(g => g.IsCollapsed), Has.Some.True);
    }

    // ...and the rightmost is the one to go, same rule as shrinking.
    [Test]
    public void TheRightmostGroupCollapsesFirst()
    {
        var (panel, groups) = Row(Group(3, fixedSize: true), Group(3, fixedSize: true));
        LayoutAt(panel, 2000);

        // Room for one group whole beside one collapsed button, and not for two whole ones.
        LayoutAt(panel, groups[0].WidthAt(0) + groups[1].CollapsedWidth + 6);

        Assert.Multiple(() =>
        {
            Assert.That(groups[1].IsCollapsed, Is.True);
            Assert.That(groups[0].IsCollapsed, Is.False, "the leading group keeps its commands");
        });
    }

    // Room given back un-collapses too - a group does not stay a button once the tab can hold it again.
    [Test]
    public void WideningTheTab_UncollapsesAGroup()
    {
        var (panel, groups) = Row(Group(3, fixedSize: true), Group(3, fixedSize: true));
        LayoutAt(panel, 2000);
        LayoutAt(panel, 200);
        Assert.That(groups.Select(g => g.IsCollapsed), Has.Some.True, "precondition: something collapsed");

        LayoutAt(panel, 2000);

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(g => g.IsCollapsed), Is.All.False);
            Assert.That(Variants(groups), Is.All.EqualTo(0));
        });
    }

    // A tab narrower than even the collapsed buttons still lays out and still answers - it runs out of variants, not out
    // of answers. (Scrolling the band is the last resort, and a later step.)
    [Test]
    public void ATabTooNarrowForEvenTheButtons_StopsAtCollapsed()
    {
        var (panel, groups) = Row(Group(3, fixedSize: true), Group(3, fixedSize: true));

        Assert.DoesNotThrow(() => LayoutAt(panel, 10));
        Assert.That(groups.Select(g => g.IsCollapsed), Is.All.True);
    }
}
