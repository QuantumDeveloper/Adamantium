using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>TAKING AN EDIT BACK. The row has carried a reset button and a mark for it from the start, and both were
/// shown only where the markup had SAID what untouched means - <c>DefaultValue="0"</c> - so a panel whose lines did not
/// say it offered no way back at all.
/// <para>The property system already knows: a value written into a component sits in its own slot, above the style's
/// and the theme's. So the line asks the object, and resetting DROPS what was written rather than writing a type's
/// default over it - the difference between a button going back to the theme's colour and a button going
/// transparent.</para></summary>
[TestFixture]
public class PropertyGridResetTests
{
    private FakeApp _app;

    private sealed class Plain
    {
        public double Scale { get; set; } = 1.5;
    }

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void UseFluent()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static (PropertyGrid Grid, Button Target) Staged()
    {
        var button = new Button { Content = "Press me" };
        var section = new PropertySection { Header = "Look", Target = button, IsExpanded = true };

        section.Properties.Add(new SolidColorBrushProperty
        {
            Header = "Background",
            Binding = new Adamantium.UI.Core.Data.Binding(nameof(Button.Background))
        });

        var grid = new PropertyGrid();
        grid.Sections.Add(section);

        var window = new Window { Width = 400, Height = 300, Content = grid };

        button.ApplyCurrentTheme();
        Settle(window);

        return (grid, button);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    private static PropertyRow Row(IUIComponent within) =>
        Rows(within).FirstOrDefault(row => row.Definition?.Header == "Background");

    private static System.Collections.Generic.IEnumerable<PropertyRow> Rows(IUIComponent within)
    {
        if (within is PropertyRow row) yield return row;

        foreach (var child in within.VisualChildren)
        {
            if (child is not IUIComponent visual) continue;

            foreach (var deep in Rows(visual)) yield return deep;
        }
    }

    // THE MARK MEANS EDITED, not "holds a value". A button wearing the theme's colour has not been touched, and a reset
    // offered there would take the theme away rather than an edit.
    [Test]
    public void AnUntouchedLineOffersNothingToPutBack()
    {
        var (grid, _) = Staged();
        var row = Row(grid);

        Assert.That(row, Is.Not.Null);
        Assert.That(row.IsModified, Is.False, "a line nobody wrote to offered to put something back");
    }

    // ...AND AN EDITED ONE SAYS SO, with no DefaultValue written anywhere: the object knows a value was put in it.
    [Test]
    public void AnEditedLineIsMarked()
    {
        var (grid, _) = Staged();
        var row = Row(grid);

        grid.Write(row, Colors.Tomato);

        Assert.That(row.IsModified, Is.True, "an edited line offers no way back");
    }

    // PUTS THE THEME BACK, not a type's default. Dropping what was written is what a reset is; writing null over it
    // would be one more edit - and a transparent button.
    [Test]
    public void ResettingDropsTheEditAndTheThemesAnswerComesBack()
    {
        var (grid, button) = Staged();
        var row = Row(grid);
        var wore = button.Background;

        grid.Write(row, Colors.Tomato);

        Assert.That((button.Background as SolidColorBrush)?.Color, Is.EqualTo(Colors.Tomato), "the edit never landed");

        Assert.Multiple(() =>
        {
            Assert.That(row.ResetToDefault(), Is.True, "the line refused to put anything back");
            Assert.That(button.Background, Is.SameAs(wore), "the theme's own brush did not come back");
            Assert.That(row.IsModified, Is.False, "the line still says it was edited");
        });
    }

    // REPORTED like any other edit, or the undo of a panel would have a hole in it exactly where somebody pressed
    // "put it back".
    [Test]
    public void AResetIsReportedTheWayAWriteIs()
    {
        var (grid, _) = Staged();
        var row = Row(grid);

        grid.Write(row, Colors.Tomato);

        var announced = 0;
        var warned = 0;

        grid.ValueChanging += (_, _) => warned++;
        grid.ValueChanged += (_, _) => announced++;

        row.ResetToDefault();

        Assert.Multiple(() =>
        {
            Assert.That(warned, Is.EqualTo(1), "nobody was told before the value went");
            Assert.That(announced, Is.EqualTo(1), "nobody was told the value went");
        });
    }

    // ...AND THE LINE DOES NOT MOVE WHILE IT IS BEING USED. The reset is offered only once a value stops being the
    // default, so it APPEARS mid-edit; collapsed, its column grew at that moment, the editor beside it narrowed by
    // that much, and everything inside the editor slid left - so the second press of a stepper landed on the reset
    // that had just arrived under the hand.
    [Test]
    public void ALineDoesNotChangeShapeBecauseSomebodyTypedInIt()
    {
        var button = new Button { Content = "Press me" };
        var section = new PropertySection { Header = "Look", Target = button, IsExpanded = true };

        section.Properties.Add(new NumericProperty
        {
            Header = "Opacity",
            Binding = new Adamantium.UI.Core.Data.Binding(nameof(Button.Opacity)),
            Minimum = 0,
            Maximum = 1,
            Step = 0.1
        });

        var grid = new PropertyGrid();

        grid.Sections.Add(section);

        var window = new Window { Width = 400, Height = 300, Content = grid };

        button.ApplyCurrentTheme();
        Settle(window);
        grid.Measure(new Size(360, 300));
        grid.Arrange(new Rect(0, 0, 360, 300));

        var row = Rows(grid).FirstOrDefault(r => r.Definition?.Header == "Opacity");

        Assert.That(row, Is.Not.Null);

        var cell = row.GetTemplateChild("PART_Value") as IUIComponent;
        var reset = row.GetTemplateChild("PART_Reset") as IUIComponent;
        var was = cell.Bounds;

        Assert.Multiple(() =>
        {
            Assert.That(reset.Visibility, Is.EqualTo(Visibility.Visible), "the button comes and goes");
            Assert.That(reset.IsEnabled, Is.False, "it offers to put something back before anything was written");
        });

        grid.Write(row, 0.5);

        Settle(window);
        grid.Measure(new Size(360, 300));
        grid.Arrange(new Rect(0, 0, 360, 300));

        Assert.That(row.IsModified, Is.True, "the line was edited and does not say so");

        Assert.Multiple(() =>
        {
            Assert.That(reset.IsEnabled, Is.True, "the button never woke up for the edit it is there to undo");
            Assert.That(cell.Bounds.Width, Is.EqualTo(was.Width).Within(0.01),
                "the editor narrowed when the reset appeared");
            Assert.That(cell.Bounds.X, Is.EqualTo(was.X).Within(0.01), "the editor slid when the reset appeared");
        });
    }

    // THE PANEL'S MANNER, and all three of them. Always is the default and the reason the line stops moving; the other
    // two are for a panel that would rather be tidy, or that does not offer putting values back at all.
    [Test]
    public void ThePanelSaysWhenItsLinesOfferTheButton()
    {
        var (grid, _) = Staged();
        var row = Row(grid);
        var reset = row.GetTemplateChild("PART_Reset") as IUIComponent;

        Assert.That(grid.ResetButton, Is.EqualTo(ResetButtonState.Always), "the steady manner is not the default");
        Assert.That(reset.Visibility, Is.EqualTo(Visibility.Visible));

        grid.ResetButton = ResetButtonState.WhenModified;

        Assert.Multiple(() =>
        {
            Assert.That(row.ResetButton, Is.EqualTo(ResetButtonState.WhenModified),
                "the line standing there was not told");
            Assert.That(reset.Visibility, Is.EqualTo(Visibility.Collapsed),
                "an untouched line kept the button it only offers on an edited one");
        });

        grid.Write(row, Colors.Tomato);

        Assert.That(reset.Visibility, Is.EqualTo(Visibility.Visible), "the edited line does not offer it either");

        grid.ResetButton = ResetButtonState.Never;

        Assert.That(reset.Visibility, Is.EqualTo(Visibility.Collapsed), "a panel that offers none still shows one");
    }

    // HOW MANY DECIMALS A LINE SHOWS. The definition has carried this from the start and nothing read it, so every
    // number printed at whatever precision a double prints at - a width dragged by hand read "182.99999999999997",
    // which is not an answer to "how wide is it".
    [Test]
    public void ANumberLineShowsAsManyDecimalsAsItWasTold()
    {
        var target = new Plain();
        var section = new PropertySection { Header = "Transform", Target = target, IsExpanded = true };

        section.Properties.Add(new NumericProperty
        {
            Header = "Scale",
            Binding = new Adamantium.UI.Core.Data.Binding(nameof(Plain.Scale))
        });

        var grid = new PropertyGrid();

        grid.Sections.Add(section);

        var window = new Window { Width = 400, Height = 300, Content = grid };

        Settle(window);

        var row = Rows(grid).FirstOrDefault(r => r.Definition?.Header == "Scale");
        var editor = Editor(row);

        Assert.That(editor, Is.Not.Null, "the line has no number editor");
        Assert.That(((NumericProperty)row.Definition).Decimals, Is.EqualTo(3), "three is what a panel wants");

        grid.Write(row, 182.99999999999997);
        Settle(window);

        Assert.That(editor.StringFormat, Is.EqualTo("0.###"), "the line never told the editor how many to show");

        // ...and a round number stays round rather than growing three zeros. Asked of the BOX the number is read in,
        // which is the thing a person actually looks at.
        grid.Write(row, 12.0);
        Settle(window);

        var box = editor.GetTemplateChild("PART_TextBox") as Adamantium.UI.Controls.Text.TextBox;

        Assert.That(box?.Text, Is.EqualTo("12"), "a whole number came out padded");
    }

    private static NumericUpDown Editor(IUIComponent within)
    {
        if (within is NumericUpDown editor) return editor;

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual && Editor(visual) is { } deep) return deep;
        }

        return null;
    }

    // A PLAIN OBJECT knows no such thing - nothing stands behind its properties to say what untouched means - so the
    // line offers nothing rather than offering a button that does nothing.
    [Test]
    public void APlainObjectsLineOffersNoResetOfItsOwn()
    {
        var target = new Plain();
        var section = new PropertySection { Header = "Transform", Target = target, IsExpanded = true };

        section.Properties.Add(new NumericProperty
        {
            Header = "Scale",
            Binding = new Adamantium.UI.Core.Data.Binding(nameof(Plain.Scale))
        });

        var grid = new PropertyGrid();
        grid.Sections.Add(section);

        var window = new Window { Width = 400, Height = 300, Content = grid };

        Settle(window);

        var row = Rows(grid).FirstOrDefault(r => r.Definition?.Header == "Scale");

        Assert.That(row, Is.Not.Null);

        grid.Write(row, 3.0);

        Assert.Multiple(() =>
        {
            Assert.That(target.Scale, Is.EqualTo(3.0).Within(1e-9), "the edit never landed");
            Assert.That(row.IsModified, Is.False, "a line with nothing behind it offered to put something back");
            Assert.That(row.ResetToDefault(), Is.False, "it claimed to have put something back");
        });
    }
}
