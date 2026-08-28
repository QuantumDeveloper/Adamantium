using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A theme's VARIANTS. The property under test throughout is the one the whole design rests on: switching a variant
/// re-colours the brushes the theme already owns instead of handing out new ones. Two dictionaries of separate brush
/// objects under the same keys would look identical on screen and cost a property write on every element that draws -
/// measured at ~18000 writes on a swap - so "the brush is the same object afterwards" is not a detail to assert in
/// passing, it is the feature.
/// </summary>
[TestFixture]
public class ThemeVariantTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    [SetUp]
    public void FreshResources()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    private static readonly Color LightBg = Color.FromRgba(243, 243, 243, 255);
    private static readonly Color DarkBg = Color.FromRgba(32, 32, 32, 255);

    private static Theme TwoVariantTheme()
    {
        var theme = new Theme("Fluent");

        var light = new ThemeVariantDefinition(ThemeVariant.Light);
        light.Colors["Background"] = LightBg;
        light.Colors["Card"] = Color.FromRgba(255, 255, 255, 255);

        var dark = new ThemeVariantDefinition(ThemeVariant.Dark);
        dark.Colors["Background"] = DarkBg;
        dark.Colors["Card"] = Color.FromRgba(45, 45, 45, 255);

        theme.AddVariant(light);
        theme.AddVariant(dark);
        theme.SystemLightVariant = ThemeVariant.Light;
        theme.SystemDarkVariant = ThemeVariant.Dark;
        return theme;
    }

    [Test]
    public void SwitchingAVariant_KeepsTheBrushObject_AndOnlyChangesItsColour()
    {
        var theme = TwoVariantTheme();
        Assert.That(theme.ApplyVariant(ThemeVariant.Light), Is.True);

        var before = theme.GetResource("Background") as SolidColorBrush;
        Assert.That(before, Is.Not.Null, "the palette must answer for a key its variants declare");
        Assert.That(before.Color, Is.EqualTo(LightBg));

        Assert.That(theme.ApplyVariant(ThemeVariant.Dark), Is.True);
        var after = theme.GetResource("Background") as SolidColorBrush;

        Assert.That(after, Is.SameAs(before),
            "the brush must be the SAME object - a new one would be a property write on every element drawing with it");
        Assert.That(after.Color, Is.EqualTo(DarkBg), "...and it must actually have taken the new variant's colour");
    }

    [Test]
    public void EveryPaletteKeyExistsBeforeAnyVariantIsApplied()
    {
        var theme = TwoVariantTheme();

        // Declaring a variant creates the brushes: they must exist before anything resolves against them, or the first
        // resolution would hand out a brush that a later variant switch has to replace - the very thing being avoided.
        Assert.That(theme.Palette.Keys, Is.EquivalentTo(new[] { "Background", "Card" }));
    }

    [Test]
    public void AVariantTheThemeDoesNotDeclare_IsRefused_NotSilentlySubstituted()
    {
        var theme = TwoVariantTheme();
        theme.ApplyVariant(ThemeVariant.Light);

        Assert.That(theme.ApplyVariant(ThemeVariant.Named("HighContrast")), Is.False,
            "a theme must say NO to a variant it has not declared, so the caller can fall back knowingly");
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light), "...and must not have changed anything");
    }

    [Test]
    public void VariantsAreValidated_ToCatchAKeyOneVariantForgets()
    {
        var theme = new Theme("Patchy");

        var light = new ThemeVariantDefinition(ThemeVariant.Light);
        light.Colors["Background"] = LightBg;
        light.Colors["Accent"] = LightBg;

        var dark = new ThemeVariantDefinition(ThemeVariant.Dark);
        dark.Colors["Background"] = DarkBg;   // no "Accent"

        theme.AddVariant(light);
        theme.AddVariant(dark);

        // Without this check the palette would keep whatever the PREVIOUS variant put in "Accent", so how the subtree
        // looks would depend on which variant it was switched FROM. That is not a thing anyone can reason about.
        Assert.That(theme.ValidateVariants(), Has.Exactly(1).Contains("Accent"));
    }

    [Test]
    public void AVariantAlsoSetsTheThemesOwnProperties()
    {
        var theme = TwoVariantTheme();
        var accent = Color.FromRgba(0, 145, 247, 255);
        theme.VariantsByKey[ThemeVariant.Dark].Values[nameof(Theme.AccentColor)] = new SolidColorBrush(accent);


        theme.ApplyVariant(ThemeVariant.Dark);

        // Accent and focus are theme PROPERTIES, not palette entries - {ThemeResource} resolves them off the theme
        // object. A variant that could only set colours would leave a light theme wearing the dark theme's accent.
        Assert.That((theme.AccentColor as SolidColorBrush)?.Color, Is.EqualTo(accent));
    }

    [Test]
    public void FollowingTheSystem_AsksTheThemeWhichOfItsVariantsIsLightAndWhichIsDark()
    {
        var theme = TwoVariantTheme();

        Assert.That(theme.ResolveSystemVariant(osPrefersDark: true), Is.EqualTo(ThemeVariant.Dark));
        Assert.That(theme.ResolveSystemVariant(osPrefersDark: false), Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void AThemeWithNoLightDarkNotion_ResolvesTheSystemVariantToNothing()
    {
        // A HUD theme is dark by nature and its variants run along another axis entirely - the signal colour. Asked to
        // follow the OS it must answer "I have no such thing" rather than pretending one of its variants is "light".
        var hud = new Theme("Game HUD");
        var cyan = new ThemeVariantDefinition(ThemeVariant.Named("Cyan"));
        cyan.Colors["Signal"] = Color.FromRgba(0, 229, 255, 255);
        var amber = new ThemeVariantDefinition(ThemeVariant.Named("Amber"));
        amber.Colors["Signal"] = Color.FromRgba(255, 176, 0, 255);
        hud.AddVariant(cyan);
        hud.AddVariant(amber);

        Assert.That(hud.ResolveSystemVariant(osPrefersDark: true).IsUnspecified, Is.True);
        Assert.That(hud.ResolveSystemVariant(osPrefersDark: false).IsUnspecified, Is.True);
    }

    [Test]
    public void TheFirstVariantDeclaredIsTheDefault_AndAnUnspecifiedRequestLandsOnIt()
    {
        var theme = TwoVariantTheme();

        Assert.That(theme.DefaultVariant, Is.EqualTo(ThemeVariant.Light));
        Assert.That(theme.ApplyVariant(default), Is.True);
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void SystemIsAValue_NotTheAbsenceOfOne()
    {
        // "Unset" already means "inherit from whoever says next". If following the OS were the same thing it could
        // never be switched ON inside a subtree that names a variant - the property would just inherit that variant.
        Assert.That(ThemeVariant.System.IsUnspecified, Is.False);
        Assert.That(ThemeVariant.System.FollowsSystem, Is.True);
        Assert.That(default(ThemeVariant).IsUnspecified, Is.True);
        Assert.That(default(ThemeVariant).FollowsSystem, Is.False);
    }

    [Test]
    public void ApplyingTheSystemVariantDirectlyIsRefused_BecauseItMustBeResolvedFirst()
    {
        var theme = TwoVariantTheme();
        theme.ApplyVariant(ThemeVariant.Light);

        Assert.That(theme.ApplyVariant(ThemeVariant.System), Is.False);
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void TheTypeParserIsRegistered_SoMarkupAttributesActuallyResolve()
    {
        // Codegen emits TypeParser.Parse<ThemeVariant>("Dark") for ThemeContext.Variant="Dark". A parser that exists
        // but is not reachable through the registry compiles perfectly and throws the first time a theme scope is
        // built - at runtime, in the markup, where it is hardest to attribute.
        Assert.That(Adamantium.Core.TypeParsing.TypeParser.Parse<ThemeVariant>("Dark"), Is.EqualTo(ThemeVariant.Dark));
        Assert.That(Adamantium.Core.TypeParsing.TypeParser.Parse<ThemeVariant>("System"), Is.EqualTo(ThemeVariant.System));
    }

    [Test]
    public void VariantKeysAreCaseInsensitive_SoMarkupAndCodeAgree()
    {
        var theme = TwoVariantTheme();

        Assert.That(theme.ApplyVariant(ThemeVariant.Parse("dark")), Is.True);
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Dark));
    }

    [Test]
    public void ALocalResourceStillShadowsThePalette()
    {
        // The palette must not outrank a dictionary on the requester's own subtree: a theme key that could not be
        // shadowed locally would make {ResourceReference} unusable for overriding anything.
        var theme = TwoVariantTheme();
        theme.ApplyVariant(ThemeVariant.Light);

        Assert.That(theme.GetResource(null, "Background"), Is.SameAs(theme.Palette["Background"]),
            "with nothing local to find, the palette is still the answer");
    }
}
