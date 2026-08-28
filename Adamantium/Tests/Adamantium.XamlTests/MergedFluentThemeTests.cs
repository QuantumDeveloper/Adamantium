using System.Linq;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.FluentTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The merged <c>Fluent</c> theme - light and dark as VARIANTS of one theme rather than as two themes.
/// <para>The two files it replaces declared the same 49 style includes, the same icons and the same metrics, and
/// differed by a palette and four accent values. The engine was therefore doing a full theme swap - every template in
/// the application rebuilt, every style re-applied - to change a hundred colours. These tests hold the merged theme to
/// the properties that make that unnecessary.</para>
/// </summary>
[TestFixture]
public class MergedFluentThemeTests
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
        // A Theme takes the resource manager from the context in its constructor, so the context has to be the OURS
        // before any theme in these tests is built.
        _app.ResourceManager = new ResourceManager();
        typeof(Adamantium.UI.Core.UIAppContext)
            .GetProperty(nameof(Adamantium.UI.Core.UIAppContext.Current))
            .SetValue(null, _app);
    }

    [Test]
    public void TheKeyEVERYPIECEOFTEXTDependsOn_Resolves()
    {
        // Window sets Foreground = {ResourceReference TextFillColorPrimary} and every plain TextBlock INHERITS it. If
        // that one key does not resolve, every such TextBlock has a null Foreground and the render walk throws on it -
        // which is a blank tab, not a wrong colour. (Text inside a template survives, because a template names its own
        // Foreground; that is why the tab STRIP looked fine while the tab CONTENT was empty.)
        var themes = new ThemeManager(new Adamantium.Core.DependencyInjection.AdamantiumDependencyContainer());
        _app.ThemeManager = themes;

        var theme = new Fluent();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        var element = new Adamantium.UI.Controls.Decorators.Border();

        Assert.That(_app.ResourceManager.FindResource(element, "TextFillColorPrimary"),
            Is.InstanceOf<SolidColorBrush>(), "resolved from the requesting element");
        Assert.That(_app.ResourceManager.FindResource("TextFillColorPrimary"),
            Is.InstanceOf<SolidColorBrush>(), "...and with no requester at all");
    }

    [Test]
    public void AnInitialisedTheme_ALREADYHASAVariant()
    {
        // Declaring a variant only creates the palette BRUSHES. The accent, the on-accent text colour and the focus
        // strokes are theme PROPERTIES, and nothing but ApplyVariant sets them - so a theme that came up on no variant
        // had all of them null, and every {ThemeResource AccentForegroundColor} (31 uses) and
        // {ThemeResource AccentFillColorDefault} (72) resolved to nothing. The window's title text then had no
        // Foreground and the render walk threw on it: a blank tab and empty fills, saying nothing about the cause.
        // Every test here used to call ApplyVariant by hand, which is exactly why none of them noticed.
        var theme = new Fluent();
        theme.Initialize();

        Assert.That(theme.CurrentVariant, Is.EqualTo(theme.DefaultVariant));
        Assert.That(theme.AccentColor, Is.Not.Null, "the accent seed the whole ramp derives from");
        Assert.That(theme.AccentForegroundColor, Is.Not.Null, "the colour text on an accent is drawn in");
        Assert.That(theme.AccentFillColorDefault, Is.Not.Null);
        Assert.That(theme.FocusStrokeColorOuter, Is.Not.Null);
    }

    [Test]
    public void ItDeclaresBothVariants()
    {
        var theme = new Fluent();

        Assert.That(theme.VariantsByKey.Keys, Is.EquivalentTo(new[] { ThemeVariant.Dark, ThemeVariant.Light }));
    }

    /// <summary>Each variant is written in its OWN markup file and named by the theme as an element. That is a compiler
    /// capability, not just a file layout: a variant root has to be recognised as something that GENERATES a class (a
    /// fragment root generates none), and the class has to exist before the theme that names it is generated - which is
    /// not file order, since "Fluent" sorts before "FluentDark".</summary>
    [Test]
    public void EachVariantIsItsOwnGeneratedClass()
    {
        var dark = new FluentDark();
        var light = new FluentLight();

        Assert.Multiple(() =>
        {
            Assert.That(dark, Is.InstanceOf<ThemeVariantDefinition>());
            Assert.That(dark.Key, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(dark.Colors, Is.Not.Empty, "the palette has to have been built by the generated constructor");
            Assert.That(dark.Values, Is.Not.Empty, "...and so do the theme values");

            Assert.That(light.Key, Is.EqualTo(ThemeVariant.Light));
            Assert.That(light.Colors.Count, Is.EqualTo(dark.Colors.Count));
        });
    }

    [Test]
    public void BothVariantsAnswerTheSameKeys()
    {
        var theme = new Fluent();

        // A key one variant declares and another does not would leave the palette holding whatever the previous
        // variant put there - so the application's appearance would depend on which variant it was switched FROM.
        Assert.That(theme.ValidateVariants(), Is.Empty,
            string.Join(" | ", theme.ValidateVariants()));
    }

    [Test]
    public void ThePaletteCarriesEveryColourTheOldPairDeclared()
    {
        var theme = new Fluent();

        // The two palette files had 35 brushes each, under identical keys. Nothing may be lost in the merge: a missing
        // key does not fail loudly, it paints nothing.
        Assert.That(theme.Palette.Count, Is.EqualTo(35));
    }

    [Test]
    public void ItOpensOnDark_LikeTheApplicationAlwaysHas()
    {
        var theme = new Fluent();

        Assert.That(theme.DefaultVariant, Is.EqualTo(ThemeVariant.Dark),
            "file order is what says which variant a theme opens on, and the application opened on FluentDark");
    }

    [Test]
    public void SwitchingItsVariant_KeepsEveryBrushAndOnlyRecolours()
    {
        var theme = new Fluent();
        theme.ApplyVariant(ThemeVariant.Dark);

        var before = theme.Palette.ToDictionary(p => p.Key, p => (Brush)p.Value);
        var darkBackground = (theme.GetResource("SolidBackgroundFillColorBase") as SolidColorBrush)!.Color;

        theme.ApplyVariant(ThemeVariant.Light);

        foreach (var pair in before)
        {
            Assert.That(theme.Palette[pair.Key], Is.SameAs(pair.Value),
                $"'{pair.Key}' must be the same brush object - a new one is a property write on every element using it");
        }

        var lightBackground = (theme.GetResource("SolidBackgroundFillColorBase") as SolidColorBrush)!.Color;
        Assert.That(lightBackground, Is.Not.EqualTo(darkBackground), "...and the colours must actually have changed");
    }

    [Test]
    public void EachVariantCarriesItsOwnAccent()
    {
        var theme = new Fluent();

        theme.ApplyVariant(ThemeVariant.Dark);
        var darkAccent = (theme.AccentColor as SolidColorBrush)!.Color;

        theme.ApplyVariant(ThemeVariant.Light);
        var lightAccent = (theme.AccentColor as SolidColorBrush)!.Color;

        // Besides their palettes, the accent is what the two old theme files actually differed by - a variant that
        // could not carry one would not be able to replace them.
        Assert.That(lightAccent, Is.Not.EqualTo(darkAccent));
    }

    [Test]
    public void ItCarriesEveryStyleSetTheThemesItReplacedHad()
    {
        var merged = new Fluent();

        // The styles are the expensive half - the half a theme swap rebuilds and a variant switch must not touch. The
        // pair this replaced listed 49 style sets each (identical lists); losing one would leave a control unstyled in
        // a way no colour test would notice.
        Assert.That(merged.StyleIncludes.Count, Is.EqualTo(49));
    }

    [Test]
    public void ItKeepsTheKeysTHATARENOTBRUSHES()
    {
        var theme = new Fluent();

        // A gradient STOP takes a colour, not a brush. Four palette tokens have always been raw colours, and the first
        // merge dropped all four - they were declared differently in the file, so the extraction never saw them. The
        // symptom was surfaces that painted nothing, which says nothing about the cause.
        Assert.That(theme.RawColors.Keys, Is.EquivalentTo(new[]
        {
            "ShimmerPeakColor", "ShimmerTrackColor", "EdgeFadeColor", "EdgeFadeColorTransparent"
        }));
    }

    [Test]
    public void EveryKeyTheOldPalettesHad_IsAnsweredByTheMergedOne()
    {
        var theme = new Fluent();

        // 35 brushes + 4 colours = the 39 keys each of the two palette files declared. Counting only the brushes is
        // what let four keys go missing unnoticed the first time.
        Assert.That(theme.Palette.Count + theme.RawColors.Count, Is.EqualTo(39));
    }

    [Test]
    public void ARawColourFollowsTheVariantToo()
    {
        var theme = new Fluent();

        theme.ApplyVariant(ThemeVariant.Dark);
        var dark = theme.GetResource("EdgeFadeColor");

        theme.ApplyVariant(ThemeVariant.Light);
        var light = theme.GetResource("EdgeFadeColor");

        Assert.That(light, Is.Not.EqualTo(dark));
    }

    [Test]
    public void ItKnowsWhichOfItsVariantsTheSystemMeansByLightAndDark()
    {
        var theme = new Fluent();

        Assert.That(theme.ResolveSystemVariant(osPrefersDark: true), Is.EqualTo(ThemeVariant.Dark));
        Assert.That(theme.ResolveSystemVariant(osPrefersDark: false), Is.EqualTo(ThemeVariant.Light));
    }
}
