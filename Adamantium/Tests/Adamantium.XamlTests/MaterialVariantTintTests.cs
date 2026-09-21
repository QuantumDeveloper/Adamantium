using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A material's TINT has to follow the theme's variant, and it did not.
/// <para>The colour was in both palettes all along - light and dark each declare AcrylicFillColorDefault - but the brush
/// took it with <c>{ResourceReference}</c>, which resolves ONCE, and a brush does not live in the visual tree for a
/// later pass to catch. So every acrylic and liquid-glass surface kept the colour of the variant it was built under and
/// stayed dark through a switch to light, while the solid fills beside it followed. Gradient stops fell into exactly
/// this trap before, which is what <c>{ObservableResource}</c> was written for.</para>
/// <para>Asked of the BRUSH after a variant change rather than of the palette: the palette was never wrong.</para>
/// </summary>
[TestFixture]
public class MaterialVariantTintTests
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
        _app.ResourceManager = new ResourceManager();
        typeof(Adamantium.UI.Core.UIAppContext)
            .GetProperty(nameof(Adamantium.UI.Core.UIAppContext.Current))
            .SetValue(null, _app);
    }

    private (Color OnDark, Color OnLight) TintAcrossVariants(Theme theme, string key)
    {
        var themes = new ThemeManager(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        // The flush is what the LOOP does once a pass: a resource change is marked and announced together, so a burst of
        // them costs one announcement rather than one each. With no loop here the test has to do that pass itself.
        theme.ApplyVariant(ThemeVariant.Dark);
        _app.ResourceManager.FlushResourceChanges();
        var onDark = ((MaterialBrush)_app.ResourceManager.FindResource(key)).TintColor;

        theme.ApplyVariant(ThemeVariant.Light);
        _app.ResourceManager.FlushResourceChanges();
        var onLight = ((MaterialBrush)_app.ResourceManager.FindResource(key)).TintColor;

        TestContext.WriteLine($"{theme.Name} {key}: dark={onDark} light={onLight}");
        return (onDark, onLight);
    }

    [TestCase("FlyoutSurfaceFill")]
    [TestCase("TooltipSurfaceFill")]
    public void FluentsAcrylic_FollowsTheVariant(string key)
    {
        var (onDark, onLight) = TintAcrossVariants(new Adamantium.UI.Themes.FluentTheme.Fluent(), key);

        Assert.That(onLight, Is.Not.EqualTo(onDark),
            "the surface kept the colour of the variant it was built under");
    }

    [TestCase("FlyoutSurfaceFill")]
    [TestCase("TooltipSurfaceFill")]
    public void MacOsGlass_FollowsTheVariant(string key)
    {
        var (onDark, onLight) = TintAcrossVariants(new Adamantium.UI.Themes.MacOsTheme.MacOs(), key);

        Assert.That(onLight, Is.Not.EqualTo(onDark),
            "the surface kept the colour of the variant it was built under");
    }

    /// <summary>And it is the palette's colour it follows, not some colour of its own - so a variant that changes the
    /// palette entry moves the surface with it.</summary>
    [Test]
    public void TheTint_IsThePalettesOwnColour()
    {
        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();
        var themes = new ThemeManager(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
        theme.ApplyVariant(ThemeVariant.Light);
        _app.ResourceManager.FlushResourceChanges();

        var surface = (MaterialBrush)_app.ResourceManager.FindResource("FlyoutSurfaceFill");
        var palette = theme.GetResource("AcrylicFillColorDefault");

        TestContext.WriteLine($"surface tint={surface.TintColor} palette={palette}");
        Assert.That(surface.TintColor, Is.EqualTo(palette));
    }
}
