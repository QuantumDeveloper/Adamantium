using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The quick-access bar is hosted in BOTH slots at once, bound to the one collection, and each instance shows
/// itself only while it is standing in the slot the placement names. Which slot an instance is in is a fact about the
/// tree - the application states the placement once, not twice.</summary>
[TestFixture]
public class RibbonQuickAccessPlacementTests
{
    // In the caption: a TitleBar somewhere above it, as TitleBar.LeadingContent gives.
    private static RibbonQuickAccess InCaption(RibbonQuickAccessPlacement placement)
    {
        var bar = new RibbonQuickAccess { Placement = placement };
        var titleBar = new TitleBar
        {
            Template = new ControlTemplate(() => new TemplateResult { RootComponent = new Border { Child = bar } })
        };

        return Realize(bar, titleBar);
    }

    // Under the ribbon: the band's own footer row, with no caption above it.
    private static RibbonQuickAccess BelowRibbon(RibbonQuickAccessPlacement placement)
    {
        var bar = new RibbonQuickAccess { Placement = placement };
        return Realize(bar, new Border { Child = bar });
    }

    // Through a real window: which slot the bar stands in is answered against the tree it ends up ATTACHED to, and
    // measuring alone never attaches anything.
    private static RibbonQuickAccess Realize(RibbonQuickAccess bar, IUIComponent content)
    {
        var window = new Window { Width = 400, Height = 200, Content = content };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return bar;
    }

    [Test]
    public void TheCaptionInstance_ShowsWhileThePlacementIsCaption()
    {
        Assert.That(InCaption(RibbonQuickAccessPlacement.Caption).Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void TheCaptionInstance_StandsDownWhenTheBarBelongsBelowTheRibbon()
    {
        Assert.That(InCaption(RibbonQuickAccessPlacement.BelowRibbon).Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void TheFooterInstance_ShowsWhileThePlacementIsBelowRibbon()
    {
        Assert.That(BelowRibbon(RibbonQuickAccessPlacement.BelowRibbon).Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void TheFooterInstance_StandsDownWhenTheBarBelongsInTheCaption()
    {
        Assert.That(BelowRibbon(RibbonQuickAccessPlacement.Caption).Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    // The bar moves at RUNTIME - it is a user's choice from a menu, not something fixed when the window was built.
    [Test]
    public void ChangingThePlacement_MovesTheBarWithoutRebuildingAnything()
    {
        var caption = InCaption(RibbonQuickAccessPlacement.Caption);
        var footer = BelowRibbon(RibbonQuickAccessPlacement.Caption);

        caption.Placement = RibbonQuickAccessPlacement.BelowRibbon;
        footer.Placement = RibbonQuickAccessPlacement.BelowRibbon;

        Assert.Multiple(() =>
        {
            Assert.That(caption.Visibility, Is.EqualTo(Visibility.Collapsed));
            Assert.That(footer.Visibility, Is.EqualTo(Visibility.Visible));
        });
    }
}
