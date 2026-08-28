using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Markup;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Variants written in MARKUP. A theme is authored in a file, so the collection markup fills and the API code calls
/// have to be the same path - two ways in would drift, and the one nobody tests would be the one themes are written
/// with.
/// </summary>
[TestFixture]
public class ThemeVariantMarkupTests
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
    public void Fresh()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    private const string TwoVariants = """
        <Theme xmlns="http://adamantium/ui"
               xmlns:x="http://adamantium/ui/xaml/extensions"
               Name="Fluent">
          <Theme.Variants>
            <ThemeVariantDefinition Key="Light">
              <ThemeVariantDefinition.Colors>
                <PaletteColor Key="Background" Color="#F3F3F3"/>
                <PaletteColor Key="Card" Color="#FFFFFF"/>
              </ThemeVariantDefinition.Colors>
            </ThemeVariantDefinition>
            <ThemeVariantDefinition Key="Dark">
              <ThemeVariantDefinition.Colors>
                <PaletteColor Key="Background" Color="#202020"/>
                <PaletteColor Key="Card" Color="#2D2D2D"/>
              </ThemeVariantDefinition.Colors>
            </ThemeVariantDefinition>
          </Theme.Variants>
        </Theme>
        """;

    [Test]
    public void AThemeFileDeclaresItsVariants()
    {
        var result = AumlLoader.Load(TwoVariants);
        Assert.That(result.Diagnostics, Is.Empty, string.Join(" | ", result.Diagnostics));

        var theme = result.Root as Theme;
        Assert.That(theme, Is.Not.Null, "the root must load as a Theme");
        Assert.That(theme!.VariantsByKey.Keys, Is.EquivalentTo(new[] { ThemeVariant.Light, ThemeVariant.Dark }));
    }

    [Test]
    public void MarkupGoesThroughTheSamePathAsCode_SoThePaletteExistsImmediately()
    {
        var theme = AumlLoader.Load(TwoVariants).Root as Theme;

        // Filling the collection is what declares the variant and creates the brushes - there is no separate "now
        // build the palette" step for a file to forget.
        Assert.That(theme!.Palette.Keys, Is.EquivalentTo(new[] { "Background", "Card" }));
    }

    [Test]
    public void ColoursReadFromMarkupAreTheOnesWritten()
    {
        var theme = AumlLoader.Load(TwoVariants).Root as Theme;
        theme!.ApplyVariant(ThemeVariant.Dark);

        var background = theme.GetResource("Background") as SolidColorBrush;
        Assert.That(background!.Color, Is.EqualTo(Color.FromRgba(32, 32, 32, 255)));
    }

    [Test]
    public void SwitchingAVariantOnAThemeReadFromMarkup_StillKeepsTheBrush()
    {
        var theme = AumlLoader.Load(TwoVariants).Root as Theme;
        theme!.ApplyVariant(ThemeVariant.Light);
        var before = theme.GetResource("Card");

        theme.ApplyVariant(ThemeVariant.Dark);

        Assert.That(theme.GetResource("Card"), Is.SameAs(before),
            "the cheap path has to hold for themes as they are actually authored, not only for ones built in a test");
    }

    [Test]
    public void TheFirstVariantInTheFileIsTheDefault()
    {
        var theme = AumlLoader.Load(TwoVariants).Root as Theme;

        Assert.That(theme!.DefaultVariant, Is.EqualTo(ThemeVariant.Light),
            "file order is the only thing that says which variant a theme opens on");
    }

    [Test]
    public void AVariantSetsTheThemesAccentFromMarkupToo()
    {
        const string withAccents = """
            <Theme xmlns="http://adamantium/ui"
                   xmlns:x="http://adamantium/ui/xaml/extensions"
                   Name="Fluent">
              <Theme.Variants>
                <ThemeVariantDefinition Key="Light">
                  <ThemeVariantDefinition.Values>
                    <ThemeValue Property="AccentColor" Value="#005FB8"/>
                  </ThemeVariantDefinition.Values>
                </ThemeVariantDefinition>
                <ThemeVariantDefinition Key="Dark">
                  <ThemeVariantDefinition.Values>
                    <ThemeValue Property="AccentColor" Value="#0091F7"/>
                  </ThemeVariantDefinition.Values>
                </ThemeVariantDefinition>
              </Theme.Variants>
            </Theme>
            """;

        var theme = AumlLoader.Load(withAccents).Root as Theme;

        // The accent is a theme PROPERTY, not a palette entry - {ThemeResource AccentColor} resolves it off the theme
        // object. Besides their palettes, an accent is the only thing the two Fluent files actually differ by, so a
        // variant that could not carry one would not be able to replace them.
        theme!.ApplyVariant(ThemeVariant.Dark);
        Assert.That((theme.AccentColor as SolidColorBrush)!.Color, Is.EqualTo(Color.FromRgba(0, 145, 247, 255)));

        theme.ApplyVariant(ThemeVariant.Light);
        Assert.That((theme.AccentColor as SolidColorBrush)!.Color, Is.EqualTo(Color.FromRgba(0, 95, 184, 255)));
    }

    [Test]
    public void AKeyMissingFromOneVariantIsReported()
    {
        const string patchy = """
            <Theme xmlns="http://adamantium/ui"
                   xmlns:x="http://adamantium/ui/xaml/extensions"
                   Name="Patchy">
              <Theme.Variants>
                <ThemeVariantDefinition Key="Light">
                  <ThemeVariantDefinition.Colors>
                    <PaletteColor Key="Background" Color="#F3F3F3"/>
                    <PaletteColor Key="Accent" Color="#005FB8"/>
                  </ThemeVariantDefinition.Colors>
                </ThemeVariantDefinition>
                <ThemeVariantDefinition Key="Dark">
                  <ThemeVariantDefinition.Colors>
                    <PaletteColor Key="Background" Color="#202020"/>
                  </ThemeVariantDefinition.Colors>
                </ThemeVariantDefinition>
              </Theme.Variants>
            </Theme>
            """;

        var theme = AumlLoader.Load(patchy).Root as Theme;

        Assert.That(theme!.ValidateVariants().Any(p => p.Contains("Accent")), Is.True,
            "a theme author who forgets a key in one variant must be told, not left with a palette that keeps "
            + "whatever the previous variant put there");
    }
}
