using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The macOS theme's transient surfaces are LIQUID GLASS, and they say so under Fluent's key names.
/// <para>The keys are the contract: <c>FlyoutSurfaceFill</c> names the surface a menu, a drop-down, a ribbon flyout or
/// a slide panel is made of, and three of those sets are still Fluent's own. Overriding the KEY is what changes the
/// material for all of them without touching a line of those templates - and it only works because the macOS
/// dictionary is linked AFTER Fluent's. Link order is not a thing anyone will remember, so it is pinned here: if the
/// two links are ever reordered, this fails instead of the theme quietly going back to frosted plastic.</para>
/// </summary>
[TestFixture]
public class MacOsGlassMaterialTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer())
        {
            ResourceManager = new ResourceManager()
        };
        Adamantium.UI.Core.UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void Fresh()
    {
        // A Theme takes the resource manager from the context in its constructor, so the context has to be ours before
        // any theme here is built - see MergedFluentThemeTests.
        _app.ResourceManager = new ResourceManager();
        typeof(Adamantium.UI.Core.UIAppContext)
            .GetProperty(nameof(Adamantium.UI.Core.UIAppContext.Current))
            .SetValue(null, _app);
    }

    private MaterialBrush Surface(string key)
    {
        var themes = new ThemeManager(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer());
        _app.ThemeManager = themes;

        var theme = new MacOs();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        var found = _app.ResourceManager.FindResource(key);
        TestContext.WriteLine($"{key} -> {found?.GetType().Name ?? "<null>"}" +
                              (found is MaterialBrush m ? $" ({m.Material}, refraction {m.Refraction})" : ""));
        return found as MaterialBrush;
    }

    /// <summary>The same key, asked the way a TEMPLATE asks it - from an element. Two lookups, and only one of them was
    /// measured at first: a style writes {ResourceReference FlyoutSurfaceFill} on a part, which resolves tree-scoped
    /// from that part, while the assertion above asks the manager with no requester at all. If those two ever disagree,
    /// the theme resolves one material and every control wears the other - which is exactly the shape of "I renamed the
    /// key, the test is green, and the panel on screen is still acrylic".</summary>
    private MaterialBrush SurfaceFromAnElement(string key)
    {
        var themes = new ThemeManager(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer());
        _app.ThemeManager = themes;

        var theme = new MacOs();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        var element = new Adamantium.UI.Controls.Decorators.Border();
        var found = _app.ResourceManager.FindResource(element, key);
        TestContext.WriteLine($"{key} from an element -> {found?.GetType().Name ?? "<null>"}" +
                              (found is MaterialBrush m ? $" ({m.Material}, refraction {m.Refraction})" : ""));
        return found as MaterialBrush;
    }

    [TestCase("FlyoutSurfaceFill")]
    [TestCase("TooltipSurfaceFill")]
    public void AskedFromAnElement_ItIsTheSameGlass(string key)
    {
        var surface = SurfaceFromAnElement(key);

        Assert.That(surface, Is.Not.Null, $"{key} must resolve for a requesting element too");
        Assert.That(surface.Material, Is.EqualTo(MaterialType.LiquidGlass),
            "a template's {ResourceReference} must reach the same material the manager reports");
    }

    [TestCase("FlyoutSurfaceFill")]
    [TestCase("TooltipSurfaceFill")]
    public void TheTransientSurfaces_AreLiquidGlass(string key)
    {
        var surface = Surface(key);

        Assert.That(surface, Is.Not.Null, $"{key} must resolve to a material brush");
        Assert.That(surface.Material, Is.EqualTo(MaterialType.LiquidGlass),
            "the macOS dictionary is linked after Fluent's, so its answer to this key is the one in force");
    }

    /// <summary>The lens has to be ON. Refraction is what separates this material from plain frosting - the pass says
    /// so itself ("refraction at zero degrades gracefully into plain acrylic") - so a zero here would leave the theme
    /// wearing acrylic under a glass name, which is worse than wearing acrylic.</summary>
    [Test]
    public void TheLensIsActuallyOn()
    {
        Assert.That(Surface("FlyoutSurfaceFill").Refraction, Is.GreaterThan(0));
    }

    /// <summary>...and a TOOLTIP bends less than a flyout. The displacement is measured in device pixels from the edge,
    /// so the same strength on a small card reaches its middle and leaves no flat part to read text on.</summary>
    [Test]
    public void ATooltipBendsLessThanAFlyout()
    {
        Assert.That(Surface("TooltipSurfaceFill").Refraction,
            Is.LessThan(Surface("FlyoutSurfaceFill").Refraction));
    }
}
