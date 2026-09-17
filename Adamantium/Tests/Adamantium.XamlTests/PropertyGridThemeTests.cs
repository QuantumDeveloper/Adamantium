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

    // A LIST'S WORDS SIT IN THE MIDDLE of it. A drop-down whose text rides high reads as a box a line too tall, and a
    // column of them reads as a form that has slipped.
    [TestCase("MacOs")]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    public void ADropDownPutsItsTextInTheMiddle(string named)
    {
        Use(named switch { "MacOs" => MacOs(), "Fluent" => Fluent(), _ => EditorPro() });

        var drop = new DropDown { Width = 140, Height = 26, MinHeight = 0, MinWidth = 0 };
        drop.Items.Add("Add");
        drop.SelectedItem = "Add";

        var window = new Window { Width = 300, Height = 120, Content = drop };
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var presenter = (IUIComponent)drop.GetTemplateChild("PART_ContentPresenter");

        Assert.That(presenter, Is.Not.Null, "no presenter at all, so this proves nothing");

        var text = Words(presenter);

        Assert.That(text, Is.Not.Null, "the list shows no words, so there is nothing to place");

        // Where the words sit inside the CONTROL, both measured from the window.
        var middle = Middle(text, drop);

        Assert.That(middle, Is.EqualTo(0).Within(1.5),
            $"{named}: the words sit {middle:0.0} pixels off the middle of the list");
    }

    // ...and the same inside an inspector ROW, which is where it was seen: the editor is stretched to the row's height
    // there, and a list that centres its words when it is 26 pixels tall need not when it is told to fill.
    [TestCase("MacOs")]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    public void ADropDownInARowPutsItsTextInTheMiddle(string named)
    {
        Use(named switch { "MacOs" => MacOs(), "Fluent" => Fluent(), _ => EditorPro() });

        var target = new Target();
        var choice = new ChoiceProperty
        {
            Header = "Kind",
            Binding = Bind("Name"),
            ItemsSource = new List<string> { "entity", "Add" }
        };

        var section = new PropertySection { Header = "Node", Target = target, IsExpanded = true };
        section.Properties.Add(choice);

        var grid = new PropertyGrid();
        grid.Sections.Add(section);
        grid.ApplyCurrentTheme();

        var window = new Window { Width = 360, Height = 200, Content = grid };
        for (var i = 0; i < 8; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        var row = FirstRow(grid);

        Assert.That(row?.Editor, Is.Not.Null, "the row has no editor, so this proves nothing");

        var text = Words(row.Editor);

        Assert.That(text, Is.Not.Null, "the list shows no words, so there is nothing to place");

        // ...and the words have ROOM. A row is shorter than a theme's own drop-down, so a padding cut for the taller one
        // leaves less than a line needs and the letters are drawn past the bottom of their box - which is what reads as
        // text sitting low, and what no alignment can correct.
        var loose = new Adamantium.UI.Controls.Text.TextBlock { Text = "entity", FontSize = text.FontSize };
        loose.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity), force: true);

        // Within a pixel and a half of the line it wants: a hairline rule and a rounded row can cost one, and one is not
        // what this is about - the case that started it was thirteen pixels holding nineteen.
        Assert.That(text.RenderSize.Height, Is.GreaterThanOrEqualTo(loose.DesiredSize.Height - 1.5),
            $"{named}: the line has {text.RenderSize.Height} pixels and wants {loose.DesiredSize.Height}");

        var inList = Middle(text, row.Editor);
        var inRow = Middle(row.Editor, row);
        var words = Middle(text, row);

        Assert.Multiple(() =>
        {
            Assert.That(inList, Is.EqualTo(0).Within(1.5),
                $"{named}: the words sit {inList:0.0} pixels off the middle of the list");
            Assert.That(inRow, Is.EqualTo(0).Within(1.5),
                $"{named}: the list sits {inRow:0.0} pixels off the middle of the row");
            Assert.That(words, Is.EqualTo(0).Within(1.5),
                $"{named}: the words sit {words:0.0} pixels off the middle of the row");
        });
    }

    private static Adamantium.UI.Controls.Text.TextBlock Words(IUIComponent from)
    {
        if (from is Adamantium.UI.Controls.Text.TextBlock words) return words;

        foreach (var child in from.VisualChildren)
        {
            if (child is IUIComponent inside && Words(inside) is { } found) return found;
        }

        return null;
    }

    // How far the middle of one is from the middle of the other, in the window's own pixels.
    private static double Middle(IUIComponent text, IUIComponent control)
    {
        var textMiddle = Top(text) + text.RenderSize.Height / 2;
        var controlMiddle = Top(control) + control.RenderSize.Height / 2;

        return textMiddle - controlMiddle;
    }

    private static double Top(IUIComponent component)
    {
        var top = 0.0;
        for (var at = component; at != null; at = at.VisualParent) top += at.Bounds.Y;

        return top;
    }
}
