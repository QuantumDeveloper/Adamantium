using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The inspector under each theme: the parts the control drives by name, and the one invariant that makes it read as a
/// table - every row's name half is the SAME width, whatever the theme and whatever the nesting.
/// </summary>
[TestFixture]
public class PropertyGridThemeTests
{
    private FakeApp _app;
    private ThemeManager _themes;

    private sealed class Target
    {
        public string Name { get; set; } = "entity";
        public double Scale { get; set; } = 1.5;
        public bool IsEnabled { get; set; } = true;
    }

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use(Theme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static Theme MacOs() => new Adamantium.UI.Themes.MacOsTheme.MacOs();

    private static Theme Fluent() => new Adamantium.UI.Themes.FluentTheme.Fluent();

    private static Theme EditorPro() => new Adamantium.UI.Themes.EditorProTheme.EditorPro();

    private static PropertyGrid Built(bool withEmptyChoice = false)
    {
        var target = new Target();
        var section = new PropertySection { Header = "Transform", Target = target, IsExpanded = true };
        section.Properties.Add(new StringProperty { Header = "Name", Binding = Bind("Name") });
        section.Properties.Add(new NumericProperty { Header = "Scale", Binding = Bind("Scale") });
        section.Properties.Add(new BooleanProperty { Header = "Enabled", Binding = Bind("IsEnabled") });

        // A list nobody filled: it must stand as tall as everything else. Collapsing to nothing when it happens to be
        // empty is what makes an inspector look broken.
        if (withEmptyChoice) section.Properties.Add(new ChoiceProperty { Header = "Material", Binding = Bind("Name") });

        var grid = new PropertyGrid();
        grid.Sections.Add(section);

        grid.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(grid);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        grid.Measure(new Size(360, 400));
        grid.Arrange(new Rect(0, 0, 360, 400));
        return grid;
    }

    private static Adamantium.UI.Core.Data.Binding Bind(string path) => new(path);

    private static PropertyRow FirstRow(PropertyGrid grid)
    {
        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow view) return view;
            }
        }

        return null;
    }

    [Test]
    public void ItKeepsItsPartsUnderMacOs() => Parts(MacOs());

    [Test]
    public void ItKeepsItsPartsUnderFluent() => Parts(Fluent());

    [Test]
    public void ItKeepsItsPartsUnderEditorPro() => Parts(EditorPro());

    private void Parts(Theme theme)
    {
        Use(theme);
        var grid = Built();

        Assert.That(grid.Template, Is.Not.Null, "a template that threw leaves the control with none at all");
        Assert.That(grid.GetTemplateChild("PART_Sections"), Is.Not.Null, "the host the sections are put in");

        var row = FirstRow(grid);
        Assert.That(row, Is.Not.Null, "the section built its rows");

        Assert.Multiple(() =>
        {
            Assert.That(row.Template, Is.Not.Null);
            Assert.That(row.GetTemplateChild("PART_Layout"), Is.Not.Null, "the three columns the control sizes");
            Assert.That(row.GetTemplateChild("PART_Name"), Is.Not.Null);
            Assert.That(row.GetTemplateChild("PART_Grip"), Is.Not.Null, "the grip between the halves");
            Assert.That(row.GetTemplateChild("PART_Value"), Is.Not.Null);
            Assert.That(row.GetTemplateChild("PART_Expander"), Is.Not.Null, "the strip that opens a composite row");
        });
    }

    [Test]
    public void EveryRowIsLiveUnderMacOs() => LiveEditors(MacOs());

    [Test]
    public void EveryRowIsLiveUnderFluent() => LiveEditors(Fluent());

    [Test]
    public void EveryRowIsLiveUnderEditorPro() => LiveEditors(EditorPro());

    // An inspector is a FORM: every editable row carries its editor from the first frame. Nothing here should have to
    // be double-clicked to become editable - that would charge a gesture for every value on the page.
    private void LiveEditors(Theme theme)
    {
        Use(theme);
        var grid = Built();

        var editors = new List<string>();
        foreach (var section in grid.Sections)
        {
            foreach (var child in ((IUIComponent)section.Content).VisualChildren)
            {
                if (child is not PropertyRow view) continue;

                Assert.That(view.Editor, Is.Not.Null, $"row '{view.Definition.Header}' came up with a live editor");
                editors.Add(view.Editor.GetType().Name);

                // Grab the number and pull - the fastest way there is to nudge a position or a mass, and what every
                // 3D tool's inspector does. The control offers it; here is where it belongs ON.
                if (view.Editor is NumericUpDown numeric)
                    Assert.That(numeric.IsDragScrubEnabled, Is.True, "a number can be dragged");
            }
        }

        Assert.That(editors, Is.EquivalentTo(new[] { "TextBox", "NumericUpDown", "CheckBox" }),
            "and each row got the editor its TYPE asks for");
    }

    [Test]
    public void EveryEditorIsTheSameHeightUnderMacOs() => OneHeight(MacOs());

    [Test]
    public void EveryEditorIsTheSameHeightUnderFluent() => OneHeight(Fluent());

    [Test]
    public void EveryEditorIsTheSameHeightUnderEditorPro() => OneHeight(EditorPro());

    // A form has ONE row height. Every editor that FILLS its half - a field, a spinner, a list - stands at that height,
    // including a list nobody filled: a control that shrank because it happens to be empty is what makes an inspector
    // look broken. A check box is the exception and stays a small square, because that is what a check box IS.
    private void OneHeight(Theme theme)
    {
        Use(theme);
        var grid = Built(withEmptyChoice: true);

        double? height = null;
        var boxes = 0;

        foreach (var section in grid.Sections)
        {
            foreach (var child in ((IUIComponent)section.Content).VisualChildren)
            {
                if (child is not PropertyRow view || view.Editor is not IUIComponent editor) continue;

                var measured = editor.RenderSize.Height;

                if (editor is CheckBox)
                {
                    boxes++;
                    Assert.That(measured, Is.LessThanOrEqualTo(view.RenderSize.Height + 0.5),
                        "a check box stays inside its row rather than stretching it");
                    continue;
                }

                Assert.That(measured, Is.GreaterThan(12), $"row '{view.Definition.Header}': an editor has to be legible");

                height ??= measured;
                Assert.That(measured, Is.EqualTo(height.Value).Within(1.5),
                    $"row '{view.Definition.Header}' stands at {measured}, the rest at {height.Value}");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(height, Is.Not.Null, "there were editors to compare");
            Assert.That(boxes, Is.EqualTo(1), "and the check box was among them");
        });
    }

    [Test]
    public void EveryRowUsesTheOneWidthUnderMacOs() => OneWidth(MacOs());

    [Test]
    public void EveryRowUsesTheOneWidthUnderFluent() => OneWidth(Fluent());

    [Test]
    public void EveryRowUsesTheOneWidthUnderEditorPro() => OneWidth(EditorPro());

    // Drag the grip on ONE row and every row follows: a per-row width would turn the inspector into a staircase.
    // Asked of the ARRANGED name half, not of the number that was written into the column - a width nothing re-measured
    // moves nothing on screen, which is exactly how the resize came to be broken.
    private void OneWidth(Theme theme)
    {
        Use(theme);
        var grid = Built();

        grid.NameColumnWidth = 200;
        grid.Measure(new Size(360, 400), force: true);
        grid.Arrange(new Rect(0, 0, 360, 400));

        foreach (var section in grid.Sections)
        {
            foreach (var child in ((IUIComponent)section.Content).VisualChildren)
            {
                if (child is not PropertyRow view) continue;

                var layout = (Adamantium.UI.Controls.Panels.Grid)view.GetTemplateChild("PART_Layout");
                var name = (IUIComponent)view.GetTemplateChild("PART_Name");
                var grip = (IUIComponent)view.GetTemplateChild("PART_Grip");

                Assert.Multiple(() =>
                {
                    Assert.That(layout.ColumnDefinitions[0].Width.Value, Is.EqualTo(200).Within(0.5),
                        $"row '{view.Definition.Header}' took the grid's width");
                    Assert.That(grip.Bounds.X, Is.EqualTo(200).Within(1),
                        $"row '{view.Definition.Header}': the grip is drawn where the width says");
                    Assert.That(grip.RenderSize.Width, Is.GreaterThanOrEqualTo(4),
                        "and it is wide enough to grab - a one-pixel target is one nobody hits");
                    Assert.That(name.RenderSize.Width, Is.GreaterThan(0));
                });
            }
        }
    }
}
