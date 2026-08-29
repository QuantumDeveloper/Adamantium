using System.Linq;
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

    /// <summary>The same coverage question one level finer. A CLASS selector ("ToggleButton.RibbonFileTab",
    /// "BusyIndicator.Dots", "ListBox.TabOverflowList") is how this framework gives one type several looks, and the type
    /// check above cannot see those: the theme styles ToggleButton, so ToggleButton counts as covered even when the file
    /// tab's own style is gone - and what a person then sees is a ribbon's File tab wearing the ordinary toggle look,
    /// with nothing anywhere saying a style went missing.
    /// The comparison is on TYPE + CLASSES together, because that pair is what decides which control a style lands on;
    /// property conditions are deliberately left out, since they gate a style's setters rather than its attachment.</summary>
    [Test]
    public void ItCoversEveryCLASSFluentStyles()
    {
        var editorPro = new EditorPro();
        var fluent = new Adamantium.UI.Themes.FluentTheme.Fluent();
        editorPro.Initialize();
        fluent.Initialize();

        static IEnumerable<string> Signatures(IEnumerable<Adamantium.UI.Core.Resources.Style> styles)
        {
            foreach (var style in styles)
            {
                if (style.Selector.Classes.Count == 0) continue;

                var classes = new List<string>(style.Selector.Classes);
                classes.Sort(System.StringComparer.Ordinal);
                var suffix = "." + string.Join(".", classes);

                if (style.Selector.Types.Count == 0) { yield return suffix; continue; }
                foreach (var type in style.Selector.Types) yield return type.FullName + suffix;
            }
        }

        var covered = new HashSet<string>(Signatures(editorPro.MergedStyles.Styles));
        var missing = new List<string>();
        foreach (var signature in Signatures(fluent.MergedStyles.Styles))
            if (!covered.Contains(signature))
                missing.Add(signature);

        Assert.That(missing, Is.Empty, "class looks left to the default of their type: " + string.Join(", ", missing));
    }

    /// <summary>Coverage means a TEMPLATE, not merely a style. The two checks above ask whether a control is addressed
    /// by some style at all - and a style that sets only a Background answers them while leaving the control with no
    /// template, which is to say drawing NOTHING. That is exactly how three controls (ListBox, ListBoxItem,
    /// DropDownItem) went dark: their Fluent sets were dropped on the reasoning that Editor Pro named the same
    /// selectors, when what it actually had was a one-line ground override that had been RIDING on Fluent's template.
    /// So the question this asks is the one that matters: for every control Fluent hands a template, does this theme
    /// hand one too?</summary>
    [Test]
    public void ItGivesATemplateToEveryControlFluentTemplates()
    {
        var editorPro = new EditorPro();
        var fluent = new Adamantium.UI.Themes.FluentTheme.Fluent();
        editorPro.Initialize();
        fluent.Initialize();

        // TYPE + CLASSES, not the type alone. A class-scoped template ("ListBox.TabOverflowList") is a template for
        // THAT class of list and no other, so counting it as the type's would have said ListBox was covered while every
        // ordinary list in the application drew nothing - which is precisely what happened.
        static HashSet<string> Templated(ITheme theme)
        {
            var signatures = new HashSet<string>();
            foreach (var style in theme.MergedStyles.Styles)
            {
                if (!style.Setters.Any(s => s.Property == "Template")) continue;

                var classes = new List<string>(style.Selector.Classes);
                classes.Sort(System.StringComparer.Ordinal);
                var suffix = classes.Count == 0 ? "" : "." + string.Join(".", classes);

                foreach (var type in style.Selector.Types) signatures.Add(type.FullName + suffix);
            }
            return signatures;
        }

        var covered = Templated(editorPro);
        var missing = Templated(fluent).Where(t => !covered.Contains(t)).OrderBy(t => t).ToList();

        Assert.That(missing, Is.Empty, "controls left with NO TEMPLATE - they draw nothing: " + string.Join(", ", missing));
    }

    /// <summary>And the third thing a theme owns, after the style and the template: the STATES.
    /// <para>A control's triggers are not decoration - they are most of what it does. A tab with a template but no
    /// triggers has no hover, no close button, no icon slot; a row has no selection. And because the template is there,
    /// it does not look broken, it looks DESIGNED that way, which is why this went unnoticed until somebody asked
    /// whether the tabs were meant to be like that.</para>
    /// <para>It is the same mistake as the missing templates, one level down: a set that keeps another theme's part
    /// names is leaning on that theme's triggers, and dropping the include takes the behaviour with it while leaving
    /// the shape behind. So the question is per property WATCHED: for every control state Fluent reacts to, does this
    /// theme react to it too?</para></summary>
    [Test]
    public void ItReactsToEveryStateFluentReactsTo()
    {
        var editorPro = new EditorPro();
        var fluent = new Adamantium.UI.Themes.FluentTheme.Fluent();
        editorPro.Initialize();
        fluent.Initialize();

        // "TabItem watches IsMouseOver" - the type, its classes, and one property the theme reacts to. A MultiTrigger
        // contributes each of its conditions: reacting to ShowCloseButton alone is not reacting to
        // ShowCloseButton+IsMouseOver, but a theme that never mentions the property at all is the case worth catching.
        static HashSet<string> WatchedStates(ITheme theme)
        {
            var watched = new HashSet<string>();

            foreach (var style in theme.MergedStyles.Styles)
            {
                var classes = new List<string>(style.Selector.Classes);
                classes.Sort(System.StringComparer.Ordinal);
                var suffix = classes.Count == 0 ? "" : "." + string.Join(".", classes);

                foreach (var trigger in style.Triggers)
                {
                    var properties = trigger switch
                    {
                        Adamantium.UI.Core.Resources.Triggers.PropertyTrigger p => new[] { p.Property },
                        Adamantium.UI.Core.Resources.Triggers.MultiTrigger m => m.Conditions.Select(c => c.Property).ToArray(),
                        _ => System.Array.Empty<string>()
                    };

                    foreach (var property in properties)
                        foreach (var type in style.Selector.Types)
                            watched.Add(type.FullName + suffix + " -> " + property);
                }
            }

            return watched;
        }

        // DELIBERATE divergences. Each one is a decision this theme made, written down here so that it stays a decision:
        // an entry has to be added on purpose, which is exactly what distinguishes it from the accidents this test is
        // for. Keep the reason with the entry.
        var byDesign = new HashSet<string>
        {
            // Fluent fades its scrollbars in on hover. Editor Pro's are ALWAYS visible - in a dense editor a bar that
            // appears only once you are already pointing at it cannot be used to see where you are in a long file - so
            // there is no hover state to have.
            "Adamantium.UI.Controls.ScrollViewer -> IsMouseOver",
        };

        var covered = WatchedStates(editorPro);
        var missing = WatchedStates(fluent)
            .Where(s => !covered.Contains(s) && !byDesign.Contains(s))
            .OrderBy(s => s)
            .ToList();

        Assert.That(missing, Is.Empty,
            "states this theme never reacts to - the control will look static: " + string.Join(", ", missing));
    }
}
