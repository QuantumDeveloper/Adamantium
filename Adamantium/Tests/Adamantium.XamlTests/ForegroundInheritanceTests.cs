using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
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
}
