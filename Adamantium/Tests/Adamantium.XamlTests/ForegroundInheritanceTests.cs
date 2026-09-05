using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Text colour reaches text by INHERITANCE: the window sets <c>Foreground</c> once and every plain TextBlock under it
/// takes that value. Nothing else does - a TextBlock has a white default of its own, so the moment inheritance stops
/// delivering, every piece of text quietly falls back to white and stays white through any theme change. It looks like
/// "the colours did not update" and says nothing about inheritance.
/// </summary>
[TestFixture]
public class ForegroundInheritanceTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void Fresh()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    [Test]
    public void AChildTakesItsForegroundFromItsParent()
    {
        var ink = new SolidColorBrush(Color.FromRgba(228, 0, 0, 0));
        var child = new TextBlock { Text = "text" };
        var parent = new Border { Child = child };

        parent.Foreground = ink;

        Assert.That(child.Foreground, Is.SameAs(ink),
            "a TextBlock has its own white default, so inheritance failing is invisible until the theme changes");
    }

    [Test]
    public void AChildAddedAFTERTheParentWasGivenAForeground_StillInheritsIt()
    {
        // The order the application actually does it in: the window is styled first, and the tree is built into it
        // afterwards.
        var ink = new SolidColorBrush(Color.FromRgba(228, 0, 0, 0));
        var parent = new Border { Foreground = ink };

        var child = new TextBlock { Text = "text" };
        parent.Child = child;

        Assert.That(child.Foreground, Is.SameAs(ink));
    }

    [Test]
    public void ItReachesThroughSeveralLevels()
    {
        var ink = new SolidColorBrush(Color.FromRgba(228, 0, 0, 0));
        var text = new TextBlock { Text = "text" };
        var inner = new Border { Child = text };
        var outer = new Border { Child = inner };

        outer.Foreground = ink;

        Assert.That(text.Foreground, Is.SameAs(ink),
            "the window is several levels above the text that takes its colour from it");
    }

    /// <summary>
    /// An ancestor LETTING GO of its ink is not the same as an ancestor stating a new one. The push down the tree writes
    /// into each descendant's Inherited slot, which outranks TypeDefault - where a bare-type style puts a control's own
    /// colour. So a push that carries the ancestor's DEFAULT pins that default into the whole subtree permanently: a
    /// later re-resolve walks up, finds no ancestor holding an explicit value, and leaves the stale slot standing.
    /// Measured on a theme swap: every ribbon command drawn at its right size, hoverable, pressable, and transparent.
    /// </summary>
    [Test]
    public void AnAncestorLettingGoOfItsInkDoesNotPinItsDefaultOnTheSubtree()
    {
        var themeInk = new SolidColorBrush(Color.FromRgba(237, 241, 245, 255));
        var child = new Adamantium.UI.Controls.Buttons.Button();
        var parent = new Border { Child = child };

        child.SetValue(UIComponent.ForegroundProperty, themeInk, ValuePriority.TypeDefault);
        parent.Foreground = new SolidColorBrush(Color.FromRgba(255, 255, 255, 255));
        Assert.That(child.Foreground, Is.Not.SameAs(themeInk), "an explicit ancestor value outranks a type default");

        parent.ClearValue(UIComponent.ForegroundProperty);

        Assert.That(child.Foreground, Is.SameAs(themeInk),
            "with no ancestor stating a colour, the control's own type default is what it wears again");
    }
}
