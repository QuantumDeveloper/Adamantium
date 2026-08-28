using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.EditorProTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The SECOND theme. Its job is not only to dress the editor: a theme that changes the metrics of every control is what
/// proves the first theme was a theme and not the framework's own look, so what is asserted here is mostly that it is a
/// complete, self-consistent theme rather than a palette.
/// </summary>
[TestFixture]
public class EditorProThemeTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer());
        UIAppContext.Initialize(_app, null);
    }

    [Test]
    public void ItDeclaresBothVariants()
    {
        var theme = new EditorPro();

        Assert.That(theme.VariantsByKey.Keys, Is.EquivalentTo(new[] { ThemeVariant.Dark, ThemeVariant.Light }));
    }

    /// <summary>A key one variant answers and another does not would leave the palette holding whatever the previous
    /// variant put there, so the appearance would depend on which variant it was switched FROM.</summary>
    [Test]
    public void BothVariantsAnswerTheSameKeys()
    {
        var theme = new EditorPro();

        Assert.That(theme.ValidateVariants(), Is.Empty, string.Join(" | ", theme.ValidateVariants()));
    }

    /// <summary>It opens DARK - order is what says so, and an editor is usually set to dark.</summary>
    [Test]
    public void ItOpensOnTheDarkVariant()
    {
        var theme = new EditorPro();

        Assert.That(theme.DefaultVariant, Is.EqualTo(ThemeVariant.Dark));
    }

    /// <summary>Both halves of "follow the system" are declared, so a subtree resolving to System has an answer either
    /// way the OS goes.</summary>
    [Test]
    public void ItSaysWhichOfItsVariantsIsLightAndWhichIsDark()
    {
        var theme = new EditorPro();

        Assert.Multiple(() =>
        {
            Assert.That(theme.ResolveSystemVariant(true), Is.EqualTo(ThemeVariant.Dark));
            Assert.That(theme.ResolveSystemVariant(false), Is.EqualTo(ThemeVariant.Light));
        });
    }

    /// <summary>A theme answers for EVERY control: styles come only from the current theme, and a control with no styled
    /// ancestor gets no template and draws nothing at all. This is the assertion that catches a set dropped from the
    /// include list - a mistake whose symptom on screen is a blank area, which says nothing about its cause.</summary>
    [Test]
    public void ItCoversEveryControlFluentCovers()
    {
        var editorPro = new EditorPro();
        var fluent = new Adamantium.UI.Themes.FluentTheme.Fluent();
        editorPro.Initialize();
        fluent.Initialize();

        var covered = new HashSet<string>();
        foreach (var style in editorPro.MergedStyles.Styles)
            foreach (var type in style.Selector.Types)
                covered.Add(type.FullName);

        var missing = new List<string>();
        foreach (var style in fluent.MergedStyles.Styles)
            foreach (var type in style.Selector.Types)
                if (!covered.Contains(type.FullName))
                    missing.Add(type.FullName);

        Assert.That(missing, Is.Empty, "controls left with no style at all: " + string.Join(", ", missing));
    }
}
