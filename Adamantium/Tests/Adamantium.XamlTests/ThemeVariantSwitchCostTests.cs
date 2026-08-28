using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// What a variant switch must NOT do. Asserting that the colour changed proves nothing - it would change just as well
/// if every template in the application were rebuilt, which is precisely the four-second, twenty-thousand-property-
/// write path that variants exist to avoid. The screen looks the same either way, so the cheap path can only be held
/// in place by tests that name the work that must not happen.
/// </summary>
[TestFixture]
public class ThemeVariantSwitchCostTests
{
    private FakeApp _app;
    private ThemeManager _themes;

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
        SystemAppearance.PrefersDark = false;
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
    }

    private static readonly Color LightBg = Color.FromRgba(243, 243, 243, 255);
    private static readonly Color DarkBg = Color.FromRgba(32, 32, 32, 255);

    private Theme CurrentTwoVariantTheme()
    {
        var theme = new Theme("Fluent");

        var light = new ThemeVariantDefinition(ThemeVariant.Light);
        light.Colors["Background"] = LightBg;

        var dark = new ThemeVariantDefinition(ThemeVariant.Dark);
        dark.Colors["Background"] = DarkBg;

        theme.AddVariant(light);
        theme.AddVariant(dark);
        theme.SystemLightVariant = ThemeVariant.Light;
        theme.SystemDarkVariant = ThemeVariant.Dark;
        theme.ApplyVariant(ThemeVariant.Light);

        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
        return theme;
    }

    [Test]
    public void SwitchingVariant_DoesNotMoveTheThemeVersion()
    {
        var theme = CurrentTwoVariantTheme();
        var before = ThemeManager.Version;

        Assert.That(_themes.SetVariant(ThemeVariant.Dark), Is.True);

        // The version is how a parked subtree asks "did the theme change while I was away". After a VARIANT change the
        // answer must be no: it is holding the very brushes whose colour changed, so it is already correct, and saying
        // yes would make it re-style a whole tab for nothing.
        Assert.That(ThemeManager.Version, Is.EqualTo(before));
    }

    [Test]
    public void SwitchingVariant_DoesNotRaiseASwapNorEnterTheChangingState()
    {
        var theme = CurrentTwoVariantTheme();
        var changing = 0;
        _themes.ThemeChanging += (_, _) => changing++;

        _themes.SetVariant(ThemeVariant.Dark);

        Assert.That(changing, Is.Zero, "a variant change is not a swap; a busy overlay must not appear for one");
        Assert.That(_themes.IsThemeChanging, Is.False);
    }

    [Test]
    public void SwitchingVariant_DoesNotUnStyleAnythingInTheTree()
    {
        var theme = CurrentTwoVariantTheme();
        var element = new Border();
        element.ApplyCurrentTheme();
        Assume.That(element.IsStyleApplied, Is.True, "precondition: the element starts styled");

        _themes.SetVariant(ThemeVariant.Dark);

        // Clearing this flag is what queues an element for re-theming. If a variant switch cleared it, every element in
        // the application would be re-styled on the next layout pass - the expensive path wearing a cheap name.
        Assert.That(element.IsStyleApplied, Is.True);
    }

    [Test]
    public void SwitchingVariant_KeepsTheBrushAndChangesItsColour()
    {
        var theme = CurrentTwoVariantTheme();
        var brush = theme.GetResource("Background") as SolidColorBrush;

        _themes.SetVariant(ThemeVariant.Dark);

        Assert.That(theme.GetResource("Background"), Is.SameAs(brush));
        Assert.That(brush!.Color, Is.EqualTo(DarkBg));
    }

    [Test]
    public void SwitchingVariant_TellsTheBrushesOwnersOnce()
    {
        var theme = CurrentTwoVariantTheme();
        var brush = theme.GetResource("Background") as SolidColorBrush;
        var raised = 0;
        brush!.Changed += (_, _) => raised++;

        _themes.SetVariant(ThemeVariant.Dark);

        // This is the whole notification budget of a variant switch: one announcement per palette brush that actually
        // changed. Everything drawing with it repaints off that, with no element written to.
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void AVariantTheThemeLacks_IsRefusedAndChangesNothing()
    {
        var theme = CurrentTwoVariantTheme();

        Assert.That(_themes.SetVariant(ThemeVariant.Named("HighContrast")), Is.False);
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void FollowingTheSystemIsResolvedByTheManager()
    {
        var theme = CurrentTwoVariantTheme();
        SystemAppearance.PrefersDark = true;

        Assert.That(_themes.SetVariant(ThemeVariant.System), Is.True);
        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Dark));
    }

    [Test]
    public void OnceFollowingTheSystem_TheApplicationKEEPSFollowingIt()
    {
        var theme = CurrentTwoVariantTheme();
        SystemAppearance.PrefersDark = false;
        _themes.SetVariant(ThemeVariant.System);
        Assume.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light));

        // Day turns to night. An application that resolved "system" once and forgot would be right until sunset and
        // wrong after it - which is the whole difference between following the system and reading it.
        SystemAppearance.PrefersDark = true;

        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Dark));
    }

    [Test]
    public void AVariantChosenBYHAND_IsNotOverriddenWhenTheSystemChanges()
    {
        var theme = CurrentTwoVariantTheme();
        _themes.SetVariant(ThemeVariant.Light);

        SystemAppearance.PrefersDark = true;

        Assert.That(theme.CurrentVariant, Is.EqualTo(ThemeVariant.Light),
            "asking for light explicitly means light, whatever the OS decides afterwards");
    }

    [Test]
    public void FollowingTheSystemOnAThemeWithNoSuchNotion_IsRefusedRatherThanGuessed()
    {
        var hud = new Theme("Game HUD");
        var cyan = new ThemeVariantDefinition(ThemeVariant.Named("Cyan"));
        cyan.Colors["Signal"] = Color.FromRgba(0, 229, 255, 255);
        hud.AddVariant(cyan);
        hud.ApplyVariant(ThemeVariant.Named("Cyan"));
        _themes.AddTheme(hud.Name, hud);
        _themes.SetTheme(hud);

        Assert.That(_themes.SetVariant(ThemeVariant.System), Is.False,
            "a HUD theme has no light or dark; guessing one of its signal colours is 'light' would be a lie");
    }
}
