using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// <c>ThemeContext</c>: which theme is in force at a place in the tree. The question these tests are really asking is
/// whether "the theme here" is expressible at all - before this it was not, there was one theme and elements merely
/// remembered what they had resolved from it, which is why anything out of the tree during a swap ended up in a state
/// nobody could name.
/// </summary>
[TestFixture]
public class ThemeContextScopeTests
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
        SystemAppearance.PrefersDark = false;
    }

    private static readonly Color LightBg = Color.FromRgba(243, 243, 243, 255);
    private static readonly Color DarkBg = Color.FromRgba(32, 32, 32, 255);

    private static Theme TwoVariantTheme(string name = "Fluent")
    {
        var theme = new Theme(name);

        var light = new ThemeVariantDefinition(ThemeVariant.Light);
        light.Colors["Background"] = LightBg;

        var dark = new ThemeVariantDefinition(ThemeVariant.Dark);
        dark.Colors["Background"] = DarkBg;

        theme.AddVariant(light);
        theme.AddVariant(dark);
        theme.SystemLightVariant = ThemeVariant.Light;
        theme.SystemDarkVariant = ThemeVariant.Dark;
        theme.ApplyVariant(ThemeVariant.Light);
        return theme;
    }

    [Test]
    public void AThemeSetOnAnElement_ReachesEverythingUnderIt()
    {
        var theme = TwoVariantTheme();
        var inner = new Border();
        var outer = new Border { Child = inner };

        ThemeContext.SetTheme(outer, theme);

        Assert.That(ThemeContext.For(inner), Is.SameAs(theme),
            "the scope has to cascade, or it would have to be repeated on every element in the subtree");
    }

    [Test]
    public void ADeeperScopeWins()
    {
        var outerTheme = TwoVariantTheme("Outer");
        var innerTheme = TwoVariantTheme("Inner");

        var inner = new Border();
        var outer = new Border { Child = inner };

        ThemeContext.SetTheme(outer, outerTheme);
        ThemeContext.SetTheme(inner, innerTheme);

        Assert.That(ThemeContext.For(inner), Is.SameAs(innerTheme));
    }

    [Test]
    public void AVariantTheThemeIsNotShowing_ResolvesToASiblingThatIs()
    {
        var theme = TwoVariantTheme();          // showing Light
        var element = new Border();
        ThemeContext.SetTheme(element, theme);
        ThemeContext.SetVariant(element, ThemeVariant.Dark);

        var resolved = ThemeContext.For(element) as Theme;

        Assert.That(resolved, Is.Not.SameAs(theme), "one palette cannot hold two variants at once");
        Assert.That(resolved!.CurrentVariant, Is.EqualTo(ThemeVariant.Dark));
        Assert.That((resolved.GetResource("Background") as SolidColorBrush)!.Color, Is.EqualTo(DarkBg));
    }

    [Test]
    public void TwoSubtreesShowDifferentVariantsAtTheSameTime()
    {
        // THE case the whole scope mechanism exists for: a preview pane beside the thing it previews. Before variants
        // resolved to siblings this was impossible - the palette is one set of brushes, so whichever subtree switched
        // last would have dragged the other one with it.
        var theme = TwoVariantTheme();

        var left = new Border();
        var right = new Border();
        var root = new StackPanel();
        root.Children.Add(left);
        root.Children.Add(right);

        ThemeContext.SetTheme(root, theme);
        ThemeContext.SetVariant(left, ThemeVariant.Light);
        ThemeContext.SetVariant(right, ThemeVariant.Dark);

        var leftBrush = (ThemeContext.For(left) as Theme)!.GetResource("Background") as SolidColorBrush;
        var rightBrush = (ThemeContext.For(right) as Theme)!.GetResource("Background") as SolidColorBrush;

        Assert.That(leftBrush!.Color, Is.EqualTo(LightBg));
        Assert.That(rightBrush!.Color, Is.EqualTo(DarkBg));
        Assert.That(leftBrush, Is.Not.SameAs(rightBrush), "two variants shown at once need two brushes");
    }

    [Test]
    public void ASiblingSharesTheStyles_SoOnlyThePaletteIsDuplicated()
    {
        var theme = TwoVariantTheme();
        var style = new Style();
        style.Selector.Types.Add(typeof(Border));
        theme.MergedStyles.Add(style);

        var sibling = theme.SiblingForVariant(ThemeVariant.Dark);

        Assert.That(sibling, Is.Not.SameAs(theme));
        Assert.That(sibling.MergedStyles.Styles, Does.Contain(style),
            "the sibling is a different PALETTE, not a different theme - duplicating the styles would make a variant "
            + "switch a template rebuild, which is the cost this whole design removes");
    }

    [Test]
    public void TheSameVariantIsAskedForTwice_AndTheSameSiblingComesBack()
    {
        var theme = TwoVariantTheme();

        Assert.That(theme.SiblingForVariant(ThemeVariant.Dark),
            Is.SameAs(theme.SiblingForVariant(ThemeVariant.Dark)),
            "a fresh sibling per request would leak one theme per resolution");
    }

    [Test]
    public void ANamedVariantGetsItsOwnSibling_EVENWhenTheThemeAlreadyShowsIt()
    {
        var theme = TwoVariantTheme();   // showing Light

        var pinned = theme.SiblingForVariant(ThemeVariant.Light);

        // Handing back the theme itself here looks free and is a bug: the subtree would then hold the APPLICATION's
        // brushes, so the moment the application switched variant the pinned subtree would switch with it - a pane
        // labelled "Dark" going light because something elsewhere changed. Naming a variant has to mean nobody else
        // can change it, and that is only true of brushes nobody else holds.
        Assert.That(pinned, Is.Not.SameAs(theme));

        theme.ApplyVariant(ThemeVariant.Dark);
        Assert.That(pinned.CurrentVariant, Is.EqualTo(ThemeVariant.Light),
            "the application moved on; the pinned subtree did not");
    }

    [Test]
    public void FollowingTheSystemResolvesAgainstWhatTheOsSays()
    {
        var theme = TwoVariantTheme();   // showing Light
        var element = new Border();
        ThemeContext.SetTheme(element, theme);
        ThemeContext.SetVariant(element, ThemeVariant.System);

        SystemAppearance.PrefersDark = true;
        Assert.That((ThemeContext.For(element) as Theme)!.CurrentVariant, Is.EqualTo(ThemeVariant.Dark));

        SystemAppearance.PrefersDark = false;
        Assert.That((ThemeContext.For(element) as Theme)!.CurrentVariant, Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void SystemStopsInheritance_ItDoesNotDeferToTheAncestorsVariant()
    {
        // The distinction that made System a value rather than "unset": a preview pane that tracks the OS, inside a
        // window pinned to dark. If following the system were expressed by leaving the property unset, the pane would
        // simply inherit dark and could never track anything.
        var theme = TwoVariantTheme();
        var inner = new Border();
        var outer = new Border { Child = inner };

        ThemeContext.SetTheme(outer, theme);
        ThemeContext.SetVariant(outer, ThemeVariant.Dark);
        ThemeContext.SetVariant(inner, ThemeVariant.System);

        SystemAppearance.PrefersDark = false;

        Assert.That((ThemeContext.For(inner) as Theme)!.CurrentVariant, Is.EqualTo(ThemeVariant.Light),
            "the inner subtree follows the OS, not the ancestor that pinned dark");
        Assert.That((ThemeContext.For(outer) as Theme)!.CurrentVariant, Is.EqualTo(ThemeVariant.Dark),
            "...and the ancestor is unaffected by its child following the OS");
    }

    [Test]
    public void ResourceLookupFromAnElement_AnswersFromTheThemeInForceThere()
    {
        // {ResourceReference} resolves through the ResourceManager, NOT through ITheme.GetResource - so a palette the
        // theme object alone knew about would answer for code and never for the markup themes are written in. This is
        // the step that makes a scope reach the screen.
        var theme = TwoVariantTheme();
        var element = new Border();
        ThemeContext.SetTheme(element, theme);
        ThemeContext.SetVariant(element, ThemeVariant.Dark);

        var resolved = _app.ResourceManager.FindResource(element, "Background") as SolidColorBrush;

        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Color, Is.EqualTo(DarkBg));
    }

    [Test]
    public void TwoElementsInDifferentScopes_ResolveTheSameKeyDifferently()
    {
        var theme = TwoVariantTheme();
        var left = new Border();
        var right = new Border();
        var root = new StackPanel();
        root.Children.Add(left);
        root.Children.Add(right);

        ThemeContext.SetTheme(root, theme);
        ThemeContext.SetVariant(left, ThemeVariant.Light);
        ThemeContext.SetVariant(right, ThemeVariant.Dark);

        var leftValue = _app.ResourceManager.FindResource(left, "Background") as SolidColorBrush;
        var rightValue = _app.ResourceManager.FindResource(right, "Background") as SolidColorBrush;

        Assert.That(leftValue!.Color, Is.EqualTo(LightBg));
        Assert.That(rightValue!.Color, Is.EqualTo(DarkBg));
    }

    [Test]
    public void StylesInsideAScopeComeFromTheScopesTheme_NotTheApplications()
    {
        // Resources were the visible half; styles are the half that decides what a control IS - its template, its
        // metrics. A scope that re-coloured a subtree but still templated it from the application's theme would be a
        // half-scope, and the half that was missing would be the expensive one to discover later.
        var appTheme = TwoVariantTheme("App");
        var appStyle = new Style();
        appStyle.Selector.Types.Add(typeof(Border));
        appStyle.Setters.Add(new Setter(nameof(Border.Width), 10.0));
        appTheme.MergedStyles.Add(appStyle);

        var scopeTheme = TwoVariantTheme("Scoped");
        var scopeStyle = new Style();
        scopeStyle.Selector.Types.Add(typeof(Border));
        scopeStyle.Setters.Add(new Setter(nameof(Border.Width), 99.0));
        scopeTheme.MergedStyles.Add(scopeStyle);

        var scoped = new Border();
        ThemeContext.SetTheme(scoped, scopeTheme);

        Assert.That(ThemeContext.For(scoped).FindStylesForComponent(scoped), Does.Contain(scopeStyle));
        Assert.That(ThemeContext.For(scoped).FindStylesForComponent(scoped), Does.Not.Contain(appStyle));
    }

    [Test]
    public void SwitchingAScopesVariant_UpdatesReferencesWrittenStraightOntoAttributes()
    {
        // A scope that can be SET before anything is shown but not SWITCHED afterwards would not be the feature asked
        // for - "a subtree that can change its own theme whenever it likes" is the whole scenario. Styles are re-applied
        // by the ordinary re-theme, but {ResourceReference} on an attribute is not a style: it resolved once, on attach,
        // and nothing would ever ask again.
        var theme = TwoVariantTheme();
        var pane = new Border();
        ThemeContext.SetTheme(pane, theme);
        ResourceResolver.SetDeferred(pane, nameof(Border.Background), "Background");

        ThemeContext.SetVariant(pane, ThemeVariant.Dark);

        Assert.That((pane.Background as SolidColorBrush)?.Color, Is.EqualTo(DarkBg));

        ThemeContext.SetVariant(pane, ThemeVariant.Light);
        Assert.That((pane.Background as SolidColorBrush)?.Color, Is.EqualTo(LightBg),
            "...and back again, as many times as the subtree likes");
    }

    [Test]
    public void AVariantTheThemeDoesNotDeclare_LeavesTheThemeAsItIs()
    {
        var theme = TwoVariantTheme();
        var element = new Border();
        ThemeContext.SetTheme(element, theme);
        ThemeContext.SetVariant(element, ThemeVariant.Named("HighContrast"));

        Assert.That(ThemeContext.For(element), Is.SameAs(theme),
            "an undeclared variant must not conjure a sibling showing something nobody asked for");
    }
}
