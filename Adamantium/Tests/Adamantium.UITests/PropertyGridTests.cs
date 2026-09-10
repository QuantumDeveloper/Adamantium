using System;
using System.Collections.Generic;
using System.ComponentModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
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
}
