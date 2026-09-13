using System;
using System.Collections.Generic;
using System.ComponentModel;
using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The inspector's own logic: what a row reads, what it writes, what refuses, and how the two halves share one
/// width. Everything here is asked of the CONTROL, not of a theme.
/// <para>Every value in it travels through a REAL binding - there is no member-name shortcut to test, because there is
/// none in the control.</para></summary>
[TestFixture]
public class PropertyGridTests
{
    private sealed class Target : INotifyPropertyChanged
    {
        private string _name = "entity";
        private double _scale = 1.5;
        private bool _isEnabled;

        public string Name
        {
            get => _name;
            set { _name = value; Raise(nameof(Name)); }
        }

        public double Scale
        {
            get => _scale;
            set { _scale = value; Raise(nameof(Scale)); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; Raise(nameof(IsEnabled)); }
        }

        public Visibility Visibility { get; set; } = Visibility.Visible;

        public int Locked { get; set; } = 7;

        private Color _tint = Colors.CornflowerBlue;

        public Color Tint
        {
            get => _tint;
            set { _tint = value; Raise(nameof(Tint)); }
        }

        /// <summary>A brush the line paints INTO. Get-only on purpose: the property is never written, which is the
        /// whole claim - and a setter would let a test pass that had quietly replaced it.</summary>
        public Brush Fill { get; } = new SolidColorBrush(Colors.Tomato);

        public Brush Frozen { get; set; } = Snapshot(new SolidColorBrush(Colors.Tomato));

        private static Brush Snapshot(Brush brush)
        {
            brush.ForRendering();
            return brush.Snapshot;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string property) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    public sealed class Model
    {
        public string[] Materials { get; init; }
    }

    private sealed class Doubling : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is double number ? number * 2 : value;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is double number ? number / 2 : value;
    }

    // The grid is templated by a theme in the application; here it gets the one part it drives by name, so the logic can
    // be asked about without dragging a theme in.
    private static PropertyGrid Built(params PropertySection[] sections)
    {
        var grid = new PropertyGrid { Template = Chrome() };
        foreach (var section in sections) grid.Sections.Add(section);

        grid.Measure(new Size(400, 400), force: true);
        grid.Arrange(new Rect(0, 0, 400, 400));
        return grid;
    }

    private static Adamantium.UI.Core.Templates.ControlTemplate Chrome() =>
        new(() =>
        {
            var host = new Adamantium.UI.Controls.Panels.StackPanel();
            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = host };
            result.RegisterName("PART_Sections", host);
            return result;
        });

    private static PropertySection Section(object target, params PropertyDefinition[] properties)
    {
        var section = new PropertySection { Header = "Transform", Target = target, IsExpanded = true };
        foreach (var property in properties) section.Properties.Add(property);
        return section;
    }

    // Rows are templated by a theme in the application. A test that asks about the EDITOR has to give them one, because
    // the editor is built by the value presenter and a row with no parts has nowhere to build it. Only the one part the
    // question needs - the rest of the row is chrome.
    private static void TemplateRows(PropertyGrid grid)
    {
        var chrome = new Adamantium.UI.Core.Templates.ControlTemplate(() =>
        {
            var host = new ContentPresenter();
            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = host };
            result.RegisterName("PART_Value", host);
            return result;
        });

        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is not PropertyRow row) continue;

                row.Template = chrome;
                // The row's OWN pass, not the grid's: the editor is built by the presenter during measure and picked up
                // in arrange, and a grid that has already settled will not walk down to a child again.
                row.Measure(new Size(400, 40), force: true);
                row.Arrange(new Rect(0, 0, 400, 40));
            }
        }
    }

    private static PropertyRow RowOf(PropertyGrid grid, PropertyDefinition definition)
    {
        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow row && ReferenceEquals(row.Definition, definition)) return row;
            }
        }

        return null;
    }

    [Test]
    public void RowsReadTheirValueThroughTheirBinding()
    {
        var target = new Target();
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };
        var grid = Built(Section(target, name, scale));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, name)?.Value, Is.EqualTo("entity"));
            Assert.That(RowOf(grid, scale)?.Value, Is.EqualTo(1.5));
        });
    }

    // The whole reason the value is a binding and not a member name: everything the binding system can do comes with
    // it. A converter is the plainest proof - a name could never carry one.
    [Test]
    public void AValueBindingCarriesItsConverter()
    {
        var target = new Target { Scale = 3 };
        var scale = new NumericProperty
        {
            Header = "Scale",
            Binding = new Binding("Scale") { Converter = new Doubling() }
        };

        var grid = Built(Section(target, scale));

        Assert.That(RowOf(grid, scale).Value, Is.EqualTo(6.0), "shown through the converter");

        grid.Write(RowOf(grid, scale), 10.0);
        Assert.That(target.Scale, Is.EqualTo(5.0), "and written back through it");
    }

    // A one-way binding is a value to LOOK at. Reporting a write that never happened would leave the inspector showing
    // a number the object never took.
    [Test]
    public void AOneWayValueIsNotWritten()
    {
        var target = new Target();
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") { Mode = BindingMode.OneWay } };
        var grid = Built(Section(target, name));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, name).Value, Is.EqualTo("entity"), "read, like any other row");
            Assert.That(grid.Write(RowOf(grid, name), "changed"), Is.False, "and the write says it did not land");
            Assert.That(target.Name, Is.EqualTo("entity"));
        });
    }

    // The model moved on its own - another view wrote it, a simulation stepped. A row bound properly follows; one that
    // read a member name once would sit there showing a stale number.
    [Test]
    public void ARowFollowsTheObjectWhenSomethingElseChangesIt()
    {
        var target = new Target();
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = Built(Section(target, name));

        target.Name = "moved elsewhere";
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(RowOf(grid, name).Value, Is.EqualTo("moved elsewhere"));
    }

    // Typing into a row writes the PROPERTY, converting on the way: an editor produces text and a double is what the
    // member takes.
    [Test]
    public void WritingConvertsToTheMembersType()
    {
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        Assert.Multiple(() =>
        {
            Assert.That(scale.TryConvert("2.5", typeof(double), out var invariant), Is.True, "as a file writes it");
            Assert.That(invariant, Is.EqualTo(2.5));

            var typed = 2.5.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Assert.That(scale.TryConvert(typed, typeof(double), out var local), Is.True, "as this machine's keyboard types it");
            Assert.That(local, Is.EqualTo(2.5));

            Assert.That(scale.TryConvert("not a number", typeof(double), out _), Is.False,
                "a value the member will not take is a refusal, not a crash");
        });
    }

    [Test]
    public void AFlagFlipsOnOneClick()
    {
        var target = new Target();
        var flag = new BooleanProperty { Header = "Enabled", Binding = new Binding("IsEnabled") };
        var grid = Built(Section(target, flag));

        Assert.That(grid.ToggleRow(RowOf(grid, flag)), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(target.IsEnabled, Is.True, "written through to the object");
            Assert.That(RowOf(grid, flag).Value, Is.True, "and the row shows it");
        });

        grid.ToggleRow(RowOf(grid, flag));
        Assert.That(target.IsEnabled, Is.False, "and back again");
    }

    // A read-only row shows TEXT, not a disabled editor: that is both the honest look and what says it cannot be
    // edited. And every write it is asked for is refused.
    [Test]
    public void AReadOnlyRowShowsTextAndRefusesWrites()
    {
        var target = new Target();
        var locked = new StringProperty { Header = "Locked", Binding = new Binding("Locked"), IsReadOnly = true };
        var open = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = Built(Section(target, locked, open));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, locked).IsReadOnly, Is.True);
            Assert.That(RowOf(grid, locked).Editor, Is.Null, "no editor at all on a row nobody can edit");
            Assert.That(grid.Write(RowOf(grid, locked), "changed"), Is.False, "and the write is refused");
            Assert.That(target.Locked, Is.EqualTo(7));
            Assert.That(RowOf(grid, open).IsReadOnly, Is.False);
        });
    }

    [Test]
    public void AnEnumPropertyFillsItsOwnChoices()
    {
        var choice = new ChoiceProperty
        {
            Header = "Visibility", Binding = new Binding("Visibility"), EnumType = typeof(Visibility)
        };

        Assert.That(choice.Choices(), Is.Not.Null);
        Assert.That(choice.Choices(), Does.Contain(Visibility.Collapsed));
    }

    // A definition is in the inspector's LOGICAL tree, so its own properties bind like any element's: against the
    // DataContext the grid stands in. That is what makes a list of choices owned by a view-model reachable without a
    // provider of any kind - the thing this control used to need one for.
    [Test]
    public void ADefinitionsOwnPropertiesBindToTheGridsDataContext()
    {
        var choices = new[] { "Steel", "Glass" };
        var target = new Target();
        var material = new ChoiceProperty { Header = "Material", Binding = new Binding("Name") };

        var section = new PropertySection { Header = "General", Target = target, IsExpanded = true };
        section.Properties.Add(material);

        var grid = Built(section);
        grid.DataContext = new Model { Materials = choices };
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        material.SetBinding(ChoiceProperty.ItemsSourceProperty, new Binding("Materials"));
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Assert.That(material.ItemsSource, Is.EquivalentTo(choices),
            "the definition reached the view-model through the tree it stands in");
    }

    // A line that says it is not shown is not built at all - which is what an inspector with a "show advanced" switch
    // is made of, and the switch itself is an ordinary binding.
    [Test]
    public void AnInvisibleDefinitionGetsNoRow()
    {
        var target = new Target();
        var shown = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var hidden = new NumericProperty { Header = "Scale", Binding = new Binding("Scale"), IsVisible = false };
        var grid = Built(Section(target, shown, hidden));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, shown), Is.Not.Null);
            Assert.That(RowOf(grid, hidden), Is.Null, "no row at all, not a hidden one");
        });
    }

    // A composite row is a node: folded it shows one line, opened it adds its children, indented one level.
    [Test]
    public void ACompositePropertyOpensIntoItsChildren()
    {
        var target = new Target();
        var x = new NumericProperty { Header = "X", Binding = new Binding("Scale") };
        var composite = new CompositeProperty { Header = "Position" };
        composite.Children.Add(x);

        var grid = Built(Section(target, composite));

        Assert.That(RowOf(grid, x), Is.Null, "folded: the child has no row at all, not a hidden one");

        grid.ToggleComposite(RowOf(grid, composite));

        Assert.Multiple(() =>
        {
            Assert.That(composite.IsExpanded, Is.True);
            Assert.That(RowOf(grid, x), Is.Not.Null);
            Assert.That(RowOf(grid, x).Indent, Is.EqualTo(grid.Indent).Within(0.01), "one level in");
            Assert.That(RowOf(grid, composite).Indent, Is.EqualTo(0).Within(0.01));
        });
    }

    // The width belongs to the GRID, and every row reads that one number - a per-row width would let the halves of two
    // rows drift apart and the inspector would read as a staircase.
    [Test]
    public void TheNameWidthIsOneNumberForEveryRow()
    {
        var target = new Target();
        var first = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var second = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };
        var grid = Built(Section(target, first, second));

        grid.NameColumnWidth = 200;
        Assert.That(grid.NameColumnWidth, Is.EqualTo(200));

        grid.NameColumnWidth = 5;
        Assert.That(grid.NameColumnWidth, Is.EqualTo(grid.MinNameColumnWidth),
            "and it cannot be squeezed past the minimum, however far the grip is dragged");
    }

    // ---- Several objects at once -----------------------------------------------------------------------------------

    private static PropertyGrid MultiGrid(PropertyDefinition definition, params Target[] targets)
    {
        var section = new PropertySection { Header = "General", IsExpanded = true };
        section.Properties.Add(definition);

        var grid = Built(section);
        grid.SelectedObjects = targets;

        // Again, because the selection arrives AFTER the first pass and a row finds its editor once a pass has run.
        grid.Measure(new Size(400, 400), force: true);
        grid.Arrange(new Rect(0, 0, 400, 400));
        return grid;
    }

    // An editor almost always has several things selected. One definition then stands for the same property on all of
    // them: it shows their common value, and SAYS SO when they differ instead of showing a blank that reads as "empty".
    [Test]
    public void SeveralObjectsShowTheirCommonValue()
    {
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = MultiGrid(name, new Target { Name = "same" }, new Target { Name = "same" });

        var row = RowOf(grid, name);
        Assert.Multiple(() =>
        {
            Assert.That(row.Value, Is.EqualTo("same"));
            Assert.That(row.IsMixed, Is.False);
        });
    }

    [Test]
    public void SeveralObjectsThatDisagreeSaySo()
    {
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = MultiGrid(name, new Target { Name = "one" }, new Target { Name = "two" });

        var row = RowOf(grid, name);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsMixed, Is.True, "the row says the objects differ");
            Assert.That(row.Value, Is.Null, "and shows no value, because there is none to show");
        });
    }

    [Test]
    public void WritingReachesEverySelectedObject()
    {
        var flag = new BooleanProperty { Header = "Enabled", Binding = new Binding("IsEnabled") };
        var first = new Target { IsEnabled = false };
        var second = new Target { IsEnabled = true };
        var grid = MultiGrid(flag, first, second);

        Assert.That(RowOf(grid, flag).IsMixed, Is.True, "one on, one off");

        grid.ToggleRow(RowOf(grid, flag));

        Assert.Multiple(() =>
        {
            Assert.That(first.IsEnabled, Is.True);
            Assert.That(second.IsEnabled, Is.True, "both, and to the SAME value - mixed resolves to true");
            Assert.That(RowOf(grid, flag).IsMixed, Is.False, "and they no longer disagree");
        });
    }

    [Test]
    public void ChangingTheObjectRebuildsTheRows()
    {
        var first = new Target { Name = "one" };
        var second = new Target { Name = "two" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };

        var section = new PropertySection { Header = "General", IsExpanded = true };
        section.Properties.Add(name);

        var grid = Built(section);
        grid.SelectedObject = first;
        Assert.That(RowOf(grid, name).Value, Is.EqualTo("one"));

        grid.SelectedObject = second;
        Assert.That(RowOf(grid, name).Value, Is.EqualTo("two"), "the section follows the selection");
    }

    // A colour is a value like any other: the line reads one and writes one back.
    [Test]
    public void AColourLineReadsAndWritesAColour()
    {
        var target = new Target();
        var tint = new ColorProperty { Header = "Tint", Binding = new Binding("Tint") };
        var grid = Built(Section(target, tint));

        Assert.That(RowOf(grid, tint).Value, Is.EqualTo(Colors.CornflowerBlue));

        Assert.That(grid.Write(RowOf(grid, tint), Colors.Goldenrod), Is.True);
        Assert.That(target.Tint, Is.EqualTo(Colors.Goldenrod));
    }

    // The point of the brush line: it does NOT put a new brush on the object, it changes the colour inside the one
    // already there. Everything else painting with that brush follows, and nothing had to be told. Note the binding is
    // one-way and the write still lands - nothing is written back through it.
    [Test]
    public void ABrushLineChangesTheColourInsideTheBrushItWasGiven()
    {
        var target = new Target();
        var before = target.Fill;

        var fill = new SolidColorBrushProperty
        {
            Header = "Fill",
            Binding = new Binding("Fill") { Mode = BindingMode.OneWay }
        };
        var grid = Built(Section(target, fill));

        Assert.That(grid.Write(RowOf(grid, fill), Colors.Goldenrod), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(target.Fill, Is.SameAs(before), "the SAME brush - nobody was handed a new one");
            Assert.That(((SolidColorBrush)target.Fill).Color, Is.EqualTo(Colors.Goldenrod));
        });
    }

    // A frozen brush is shared and cannot be painted into. Then the line falls back to putting a new brush there, which
    // is the only thing left that can work - silently doing nothing would leave the inspector showing a colour the
    // object never took.
    [Test]
    public void AFrozenBrushIsReplacedRatherThanPainted()
    {
        var target = new Target();
        var before = target.Frozen;

        var frozen = new SolidColorBrushProperty { Header = "Frozen", Binding = new Binding("Frozen") };
        var grid = Built(Section(target, frozen));

        Assert.That(grid.Write(RowOf(grid, frozen), Colors.Goldenrod), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(target.Frozen, Is.Not.SameAs(before), "a new brush, because the old one cannot be painted");
            Assert.That(((SolidColorBrush)target.Frozen).Color, Is.EqualTo(Colors.Goldenrod));
            Assert.That(((SolidColorBrush)before).Color, Is.EqualTo(Colors.Tomato), "and the shared one is untouched");
        });
    }

    // The "..." button is the line's, not the inspector's: off everywhere until a line asks for it, so an inspector of
    // lines that offer nothing shows no buttons at all.
    [Test]
    public void TheActionButtonIsOffUnlessTheLineAsksForIt()
    {
        var target = new Target();
        var plain = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var offering = new StringProperty
        {
            Header = "Tag",
            Binding = new Binding("Name"),
            ShowActionButton = true
        };
        var grid = Built(Section(target, plain, offering));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, plain).ShowActionButton, Is.False);
            Assert.That(RowOf(grid, offering).ShowActionButton, Is.True, "the row mirrors what the line asked for");
        });
    }

    // What the button DOES is the application's business. The inspector only hands over what the line is pointed at -
    // one object, or all of them when several are selected.
    [Test]
    public void TheActionButtonRunsTheApplicationsCommand()
    {
        var target = new Target();
        var command = new Recording();
        var tag = new StringProperty
        {
            Header = "Tag",
            Binding = new Binding("Name"),
            ShowActionButton = true,
            ActionCommand = command
        };
        var grid = Built(Section(target, tag));

        Assert.That(RowOf(grid, tag).RunAction(), Is.True);
        Assert.That(command.Ran, Is.SameAs(target), "the one object the line is pointed at");
    }

    [Test]
    public void TheActionButtonHandsOverEveryObjectWhenSeveralAreSelected()
    {
        var first = new Target();
        var second = new Target();
        var command = new Recording();
        var tag = new StringProperty
        {
            Header = "Tag",
            Binding = new Binding("Name"),
            ShowActionButton = true,
            ActionCommand = command
        };
        var grid = MultiGrid(tag, first, second);

        Assert.That(RowOf(grid, tag).RunAction(), Is.True);
        Assert.That(command.Ran, Is.EqualTo(new object[] { first, second }));
    }

    // A command that says it cannot run must not be run - and the row must SAY the press did nothing rather than
    // reporting a press that never reached anything.
    [Test]
    public void AnActionThatCannotRunIsNotRun()
    {
        var target = new Target();
        var command = new Recording { CanRun = false };
        var tag = new StringProperty
        {
            Header = "Tag",
            Binding = new Binding("Name"),
            ShowActionButton = true,
            ActionCommand = command
        };
        var grid = Built(Section(target, tag));

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, tag).RunAction(), Is.False);
            Assert.That(command.Ran, Is.Null);
        });
    }

    // A line offering the button but no command: pressing it is a no-op, not a crash.
    [Test]
    public void AButtonWithNoCommandDoesNothing()
    {
        var target = new Target();
        var tag = new StringProperty { Header = "Tag", Binding = new Binding("Name"), ShowActionButton = true };
        var grid = Built(Section(target, tag));

        Assert.That(RowOf(grid, tag).RunAction(), Is.False);
    }

    // Two objects each holding their OWN brush of the same colour hold, as far as the line is concerned, the same
    // value - a brush has no equality of its own, so comparing them by reference would have the row reporting a
    // difference nobody can see.
    [Test]
    public void TwoBrushesOfOneColourAreNotADifference()
    {
        var fill = new SolidColorBrushProperty { Header = "Fill", Binding = new Binding("Fill") };
        var grid = MultiGrid(fill, new Target(), new Target());

        var row = RowOf(grid, fill);
        Assert.Multiple(() =>
        {
            Assert.That(row.Target, Is.Not.SameAs(row.Targets[1]));
            Assert.That(((Target)row.Targets[0]).Fill, Is.Not.SameAs(((Target)row.Targets[1]).Fill),
                "two brushes, not one - which is what makes this worth asserting");
            Assert.That(row.IsMixed, Is.False, "and the line shows the colour they agree on");
        });
    }

    [Test]
    public void TwoBrushesOfDifferentColoursStillDisagree()
    {
        var second = new Target();
        ((SolidColorBrush)second.Fill).Color = Colors.Goldenrod;

        var fill = new SolidColorBrushProperty { Header = "Fill", Binding = new Binding("Fill") };
        var grid = MultiGrid(fill, new Target(), second);

        Assert.That(RowOf(grid, fill).IsMixed, Is.True);
    }

    // A row of disagreeing objects KEEPS its editor: putting one value on all of them is the point of selecting
    // several, and a row with nothing in it cannot be filled.
    [Test]
    public void ARowTheObjectsDisagreeOnCanStillBeEdited()
    {
        var first = new Target { Name = "one" };
        var second = new Target { Name = "two" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = MultiGrid(name, first, second);

        TemplateRows(grid);

        var row = RowOf(grid, name);
        Assert.That(row.Editor, Is.Not.Null, "the editor is there even with nothing to show");

        Assert.That(grid.Write(row, "agreed"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(first.Name, Is.EqualTo("agreed"));
            Assert.That(second.Name, Is.EqualTo("agreed"));
            Assert.That(row.IsMixed, Is.False, "and they no longer disagree");
        });
    }

    // The conversion has to know the type even when there is no common value to read it off - otherwise what was typed
    // reaches a double-valued property as a string.
    [Test]
    public void ANumberTypedIntoADisagreeingRowIsStillConverted()
    {
        var first = new Target { Scale = 1 };
        var second = new Target { Scale = 2 };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };
        var grid = MultiGrid(scale, first, second);

        var row = RowOf(grid, scale);
        Assert.That(row.IsMixed, Is.True);

        Assert.That(grid.Write(row, "7,5"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(first.Scale, Is.EqualTo(7.5));
            Assert.That(second.Scale, Is.EqualTo(7.5));
        });
    }

    // A colour swatch has no empty state - it is a colour or it is nothing. So that one row stays blank rather than
    // standing there showing a colour neither object holds.
    [Test]
    public void AColourRowTheObjectsDisagreeOnStaysBlank()
    {
        var second = new Target { Tint = Colors.Goldenrod };
        var tint = new ColorProperty { Header = "Tint", Binding = new Binding("Tint") };
        var grid = MultiGrid(tint, new Target(), second);

        TemplateRows(grid);

        var row = RowOf(grid, tint);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsMixed, Is.True);
            Assert.That(row.Editor, Is.Null, "no swatch, because there is no colour it could honestly show");
        });
    }

    // An empty editor says nothing on its own, and "nothing" is not what happened - a double cannot hold nothing at
    // all. So the editor's prompt says the objects hold more than one value, and it goes once they agree.
    [Test]
    public void ADisagreeingEditorSaysSoAndStopsWhenTheyAgree()
    {
        var first = new Target { Scale = 1 };
        var second = new Target { Scale = 2 };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };
        var grid = MultiGrid(scale, first, second);

        TemplateRows(grid);

        var row = RowOf(grid, scale);
        Assert.That(row.Editor, Is.InstanceOf<NumericUpDown>());
        Assert.That(((NumericUpDown)row.Editor).Placeholder, Is.EqualTo(grid.MixedText));

        Assert.That(grid.Write(row, 7.0), Is.True);
        Assert.That(((NumericUpDown)row.Editor).Placeholder, Is.Null, "they agree now - there is nothing to say");
    }

    // The point of the whole thing: the caption appears ONLY where the objects really hold different values. A row they
    // all agree on shows that value, however many of them are selected.
    [Test]
    public void OnlyTheRowsThatReallyDifferSaySo()
    {
        var first = new Target { Name = "Player", Scale = 1 };
        var second = new Target { Name = "Enemy", Scale = 1 };

        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        var section = new PropertySection { Header = "General", IsExpanded = true };
        section.Properties.Add(name);
        section.Properties.Add(scale);

        var grid = Built(section);
        grid.SelectedObjects = new[] { first, second };
        TemplateRows(grid);

        var differing = RowOf(grid, name);
        var agreed = RowOf(grid, scale);

        Assert.Multiple(() =>
        {
            Assert.That(differing.IsMixed, Is.True);
            Assert.That(((TextBox)differing.Editor).Placeholder, Is.EqualTo(grid.MixedText));

            Assert.That(agreed.IsMixed, Is.False, "they hold the same number");
            Assert.That(agreed.Value, Is.EqualTo(1.0), "so the row shows it");
            Assert.That(((NumericUpDown)agreed.Editor).Placeholder, Is.Null, "and says nothing about a difference");
        });
    }

    // A hundred objects that agree are still one value, not a hundred.
    [Test]
    public void ManyObjectsThatAgreeShowTheirValue()
    {
        var targets = new Target[100];
        for (var i = 0; i < targets.Length; i++) targets[i] = new Target { Name = "shared" };

        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = MultiGrid(name, targets);

        TemplateRows(grid);

        var row = RowOf(grid, name);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsMixed, Is.False);
            Assert.That(row.Value, Is.EqualTo("shared"));
            Assert.That(((TextBox)row.Editor).Placeholder, Is.Null);
        });
    }

    // And a hundred that do not agree still say it in one phrase - what each of them holds is not something a row can
    // show, and trying would fill it with a line nobody can read.
    [Test]
    public void ManyObjectsThatDisagreeSaySoInOnePhrase()
    {
        var targets = new Target[100];
        for (var i = 0; i < targets.Length; i++) targets[i] = new Target { Name = $"object {i}" };

        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = MultiGrid(name, targets);

        TemplateRows(grid);

        var row = RowOf(grid, name);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsMixed, Is.True);
            Assert.That(((TextBox)row.Editor).Placeholder, Is.EqualTo(grid.MixedText));
        });
    }

    // An unticked box on a row where half the objects are ticked would be a plain lie. Indeterminate, and one flip
    // resolves them all - to TRUE, because leaving them disagreeing is the one thing nobody asked for.
    [Test]
    public void ABooleanTheObjectsDisagreeOnIsIndeterminate()
    {
        var first = new Target { IsEnabled = true };
        var second = new Target { IsEnabled = false };
        var enabled = new BooleanProperty { Header = "Enabled", Binding = new Binding("IsEnabled") };
        var grid = MultiGrid(enabled, first, second);

        TemplateRows(grid);

        var row = RowOf(grid, enabled);
        Assert.That(row.Editor, Is.InstanceOf<ToggleButton>());
        Assert.That(((ToggleButton)row.Editor).IsChecked, Is.Null, "neither on nor off - they disagree");

        Assert.That(grid.ToggleRow(row), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(first.IsEnabled, Is.True);
            Assert.That(second.IsEnabled, Is.True);
            Assert.That(((ToggleButton)row.Editor).IsChecked, Is.True);
        });
    }

    private sealed class Recording : ICommand
    {
        public object Ran;

        public bool CanRun = true;

        public bool CanExecute(object parameter = null) => CanRun;

        public void Execute(object parameter = null) => Ran = parameter;

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // A MEASUREMENT, not a check - explicit, so it runs when asked. How the inspector scales with the size of the
    // selection, which is the number that decides whether an editor can point it at everything a user just selected.
    // Written to a file because a number printed from a test run is lost.
    [Test, Explicit]
    public void MeasureLargeSelection()
    {
        var report = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "propertygrid-selection.csv");
        System.IO.File.WriteAllText(report, "objects,rows,bind ms,read ms,write ms\n");

        foreach (var count in new[] { 1, 10, 100, 1000, 10000, 50000 })
        {
            var targets = new Target[count];
            for (var i = 0; i < count; i++) targets[i] = new Target { Name = "shared", Scale = 1 };

            var section = new PropertySection { Header = "General", IsExpanded = true };
            var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
            section.Properties.Add(name);
            section.Properties.Add(new StringProperty { Header = "Locked", Binding = new Binding("Locked") });
            section.Properties.Add(new NumericProperty { Header = "Scale", Binding = new Binding("Scale") });
            section.Properties.Add(new BooleanProperty { Header = "Enabled", Binding = new Binding("IsEnabled") });

            var grid = Built(section);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            grid.SelectedObjects = targets;
            var bind = watch.Elapsed.TotalMilliseconds;

            var row = RowOf(grid, name);

            watch.Restart();
            grid.Refresh();
            var read = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            grid.Write(row, "pushed");
            var write = watch.Elapsed.TotalMilliseconds;

            System.IO.File.AppendAllText(report,
                $"{count},{section.Properties.Count},{bind:F1},{read:F1},{write:F1}\n");
        }

        TestContext.Out.WriteLine(System.IO.File.ReadAllText(report));
    }
}
