using System;
using System.Collections.Generic;
using System.ComponentModel;
using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Input;
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

    // A ROW IS A SHAPE, NOT A VALUE. Pointed at a different object, the same line is the same row re-aimed - so a
    // rebuild that finds the same lines must keep the rows it already has.
    //
    // Measured before this was so: one click on the plane rebuilt the inspector, and that rebuild made about two
    // hundred and fifty controls and built two hundred and thirty templates, with a property write per part of each and
    // thousands of layout invalidations behind them. That was the whole of what a slow click was.
    [Test]
    public void RebuildingWithTheSameLinesKeepsTheRowsItHas()
    {
        var first = new Target { Name = "one" };
        var second = new Target { Name = "two" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        var section = Section(first, name, scale);
        var grid = Built(section);

        var was = RowOf(grid, name);
        Assert.That(was, Is.Not.Null);

        // The same lines, a different object: what selecting something else on a plane does.
        section.Target = second;
        grid.Rebuild();

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, name), Is.SameAs(was), "the row was thrown away and built again");
            Assert.That(was.Targets[0], Is.SameAs(second), "...and it was not re-aimed at the new object");
        });
    }

    // ...and it still gets rid of what is no longer wanted, or a panel would only ever grow.
    [Test]
    public void RebuildingWithFewerLinesDropsTheExtraRows()
    {
        var target = new Target();
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        var section = Section(target, name, scale);
        var grid = Built(section);

        scale.IsVisible = false;
        grid.Rebuild();

        Assert.Multiple(() =>
        {
            Assert.That(RowOf(grid, scale), Is.Null, "a line nobody asked for is still shown");
            Assert.That(RowOf(grid, name), Is.Not.Null);
        });
    }

    // A path that goes THROUGH a property whose declared type is an interface has to resolve the rest of it on what the
    // object actually IS. An inspector points at an item that carries a control, and the control's own lines can only
    // be reached that way - "Element.Accent" where Element is declared as IUIComponent and happens to be a node.
    [Test]
    public void APathThroughAnInterfaceResolvesOnTheRuntimeType()
    {
        var node = new CanvasNode { Accent = Brushes.Red };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var accent = new SolidColorBrushProperty { Header = "Accent", Binding = new Binding("Element.Accent") };
        var grid = Built(Section(item, accent));

        Assert.That(RowOf(grid, accent)?.Value, Is.SameAs(node.Accent));
    }

    // ...and the swatch has to END UP wearing it. Reading the right brush and showing the wrong colour is the same
    // thing to the person looking at the row.
    [Test]
    public void ASwatchWearsTheBrushItWasPointedAt()
    {
        var node = new CanvasNode { Accent = Brushes.Red };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var accent = new SolidColorBrushProperty { Header = "Accent", Binding = new Binding("Element.Accent") };
        var grid = Built(Section(item, accent));

        TemplateRows(grid);

        var swatch = RowOf(grid, accent)?.Editor as ColorPickerButton;

        Assert.That(swatch, Is.Not.Null, "the row built no editor at all");
        Assert.That(swatch.SelectedColor, Is.EqualTo(Colors.Red));
    }

    // A colour row writes INTO the brush it finds, so that everything painting with that brush follows - right when the
    // object owns it, and wrong when it is the theme's. The theme hands one brush to everything that asks for that
    // colour, so recolouring one node's title strip repainted every accent in the application. An edit there means the
    // object OVERRIDES the theme: a new brush on the object, and the theme's left alone.
    [Test]
    public void EditingAThemeBrushLeavesTheThemesOwnBrushAlone()
    {
        var shared = new SolidColorBrush(Colors.Blue);
        ((Brush)shared).IsShared = true;

        var node = new CanvasNode { Accent = shared };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var accent = new SolidColorBrushProperty { Header = "Accent", Binding = new Binding("Element.Accent") };
        var grid = Built(Section(item, accent));

        grid.Write(RowOf(grid, accent), Colors.Red);

        Assert.Multiple(() =>
        {
            Assert.That(shared.Color, Is.EqualTo(Colors.Blue), "the theme's own brush was repainted");
            Assert.That(node.Accent, Is.Not.SameAs(shared), "the node kept holding the theme's brush");
            Assert.That((node.Accent as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red));
        });
    }

    // ...and a brush the object owns is still written into, which is what lets two shapes deliberately sharing one
    // brush be recoloured together.
    [Test]
    public void EditingAnOwnedBrushStillWritesIntoIt()
    {
        var own = new SolidColorBrush(Colors.Blue);
        var node = new CanvasNode { Accent = own };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var accent = new SolidColorBrushProperty { Header = "Accent", Binding = new Binding("Element.Accent") };
        var grid = Built(Section(item, accent));

        grid.Write(RowOf(grid, accent), Colors.Red);

        Assert.Multiple(() =>
        {
            Assert.That(node.Accent, Is.SameAs(own));
            Assert.That(own.Color, Is.EqualTo(Colors.Red));
        });
    }

    // A list of things that each have properties of their own could only be written out by hand, which means writing
    // out a number of lines nobody knows in advance. One group of lines per item, each pointed at that item.
    [Test]
    public void AnItemsLineRepeatsItsChildrenPerElement()
    {
        var node = new CanvasNode { Inputs = 3, Outputs = 0 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Element.InputPins"),
            ItemHeader = new Binding("Name")
        };
        sockets.Children.Add(name);

        var grid = Built(Section(item, sockets));

        var rows = RowsOf(grid, name);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Count, Is.EqualTo(3), "one line per socket");
            Assert.That(rows[0].Targets[0], Is.SameAs(node.InputPins[0]), "a line has to read the item, not the node");
            Assert.That(rows[2].Targets[0], Is.SameAs(node.InputPins[2]));
            Assert.That(rows[1].Value, Is.EqualTo("In 2"));
        });
    }

    // THE SOCKETS OF THE THING SELECTED, read off the application's own node - which is the shape the inspector
    // actually uses: the line is bound through the item to the node it stands for, not to the control showing it. And
    // a socket ADDED while the line is open is another group, without anything being reselected.
    [Test]
    public void AnItemsLineFollowsTheModelsOwnList()
    {
        var node = new Graph.GraphNode { Kind = "Mix", Title = "Mix" };
        node.Inputs.Add(new Graph.GraphSocket { Name = "A" });
        node.Inputs.Add(new Graph.GraphSocket { Name = "B" });

        var item = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110)) { Model = node };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Model.Inputs"),
            ItemHeader = new Binding("Name")
        };

        sockets.Children.Add(name);

        var grid = Built(Section(item, sockets));

        Assert.That(RowsOf(grid, name).Count, Is.EqualTo(2), "the node's sockets are not shown at all");

        node.Inputs.Add(new Graph.GraphSocket { Name = "Amount" });
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var rows = RowsOf(grid, name);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Count, Is.EqualTo(3), "a socket added while the list was open never showed up");
            Assert.That(rows[2].Value, Is.EqualTo("Amount"));
        });
    }

    // Each group is named by the ITEM - a socket's own name over its own lines. Numbered when the items have nothing to
    // be called.
    [Test]
    public void EachGroupIsNamedByItsItem()
    {
        var node = new CanvasNode { Inputs = 2, Outputs = 0 };
        node.InputPins[0].Name = "Base";
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Element.InputPins"),
            ItemHeader = new Binding("Name")
        };
        sockets.Children.Add(new StringProperty { Header = "Name", Binding = new Binding("Name") });

        var grid = Built(Section(item, sockets));

        Assert.That(Headers(grid), Does.Contain("Base"));
    }

    // Writing through one of those lines reaches the ITEM and nothing else - renaming the second socket leaves the
    // first alone, which is the whole point of a line per item.
    [Test]
    public void WritingThroughAnItemsLineReachesThatItem()
    {
        var node = new CanvasNode { Inputs = 2, Outputs = 0 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Element.InputPins"),
            ItemHeader = new Binding("Name")
        };
        sockets.Children.Add(name);

        var grid = Built(Section(item, sockets));

        grid.Write(RowsOf(grid, name)[1], "Bias");

        Assert.Multiple(() =>
        {
            Assert.That(node.InputPins[1].Name, Is.EqualTo("Bias"));
            Assert.That(node.InputPins[0].Name, Is.EqualTo("In 1"));
        });
    }

    // A socket added or dropped is a LINE appearing or going, which no amount of re-reading values can do. The grid
    // follows the collection it is showing and builds again.
    [Test]
    public void AddingToTheCollectionAddsItsLines()
    {
        var node = new CanvasNode { Inputs = 2, Outputs = 0 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Element.InputPins"),
            ItemHeader = new Binding("Name")
        };
        sockets.Children.Add(name);

        var grid = Built(Section(item, sockets));

        node.Inputs = 4;

        Assert.That(RowsOf(grid, name).Count, Is.EqualTo(4));
    }

    // Three dots mean "there is more", and a button that silently drops something is not that. A list says what its
    // per-item button does, and the lines it makes wear it - there is nowhere else to say it, because those lines are
    // made as the grid builds.
    [Test]
    public void AnItemsLineCarriesWhatItsButtonDoes()
    {
        var node = new CanvasNode { Inputs = 1, Outputs = 0 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));
        var sockets = new ItemsProperty
        {
            Header = "In",
            IsExpanded = true,
            Binding = new Binding("Element.InputPins"),
            ItemHeader = new Binding("Name"),
            ItemAction = new Doing(),
            ItemActionIcon = "BinIcon",
            ItemActionTip = "Remove this socket"
        };
        sockets.Children.Add(new StringProperty { Header = "Name", Binding = new Binding("Name") });

        var grid = Built(Section(item, sockets));
        var header = RowsWhoseHeaderIs(grid, "In 1");

        Assert.That(header, Is.Not.Null, "no line for the socket at all");
        Assert.Multiple(() =>
        {
            Assert.That(header.Definition.ShowActionButton, Is.True);
            Assert.That(header.Definition.ActionIcon, Is.EqualTo("BinIcon"), "it would still be three dots");
            Assert.That(header.Definition.ActionTip, Is.EqualTo("Remove this socket"));
        });
    }

    // A COLOUR NOBODY HAS SET is still a line you can press. The node's accent is empty until somebody picks one - the
    // theme's is what it wears meanwhile - and a blank cell there is a colour that can never be chosen.
    [Test]
    public void AColourLineWithNoValueStillOffersItsSwatch()
    {
        var node = new Graph.GraphNode { Kind = "Add", Title = "Add" };
        var item = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110)) { Model = node };

        var accent = new ColorProperty { Header = "Accent", Binding = new Binding("Model.Accent") };
        var grid = Built(Section(item, accent));
        TemplateRows(grid);

        var row = RowOf(grid, accent);

        Assert.That(row.Value, Is.Null, "the node has a colour already, so this proves nothing");
        Assert.That(row.Editor, Is.Not.Null, "the line is blank, so there is nothing to pick a colour with");
    }

    // SHOWING a line must not set what it shows. The inspector's "Folded" box appeared the first time nodes were
    // selected, and the nodes folded - a selection doing something only a person clicking that box should do.
    [Test]
    public void ShowingABooleanLineDoesNotSetIt()
    {
        var node = new Graph.GraphNode { Kind = "Add", Title = "Add" };
        var item = new ElementItem(new CanvasNode(), new Rect(0, 0, 190, 110)) { Model = node };

        var folded = new BooleanProperty { Header = "Folded", Binding = new Binding("Model.IsCollapsed") };
        var grid = Built(Section(item, folded));

        // The EDITOR built, which is what showing the line actually does - a row with no parts has nowhere to build one
        // and would prove nothing.
        TemplateRows(grid);

        Assert.That(node.IsCollapsed, Is.False, "the line folded the node by being shown");
    }

    // ...AND NOT WHEN IT IS SHOWN FOR SEVERAL THINGS AT ONCE. Click one node, then select them all: the line is built
    // again, now for many targets, and building it must still be a reading. It was not - every node folded the moment
    // the selection grew, which is the one thing a selection may not do.
    [Test]
    public void ShowingABooleanLineForSeveralThingsDoesNotSetThem()
    {
        var first = new Graph.GraphNode { Kind = "Add", Title = "Add" };
        var second = new Graph.GraphNode { Kind = "Mix", Title = "Mix" };
        var third = new Graph.GraphNode { Kind = "Out", Title = "Out", IsCollapsed = true };

        var folded = new BooleanProperty { Header = "Folded", Binding = new Binding("Model.IsCollapsed") };

        // TWO THAT DISAGREE first: one folded, one not. The line cannot show either, so it goes mixed - which is the
        // state whose rule is "true for all".
        var section = new PropertySection { Header = "Node", IsExpanded = true };
        section.Properties.Add(folded);

        var grid = new PropertyGrid { Template = Chrome() };
        grid.Sections.Add(section);
        grid.SelectedObjects = new object[] { Item(first), Item(third) };

        Settle(grid);
        TemplateRows(grid);

        Assert.That(first.IsCollapsed, Is.False, "showing a line that cannot answer already folded a node");
        Assert.That(RowOf(grid, folded).IsMixed, Is.True, "the line is not mixed, so this proves nothing");

        // ...and NOW a selection they all agree on. The line takes its targets again and puts their answer in the box -
        // a change of what the box holds, raised exactly as a click raises it, while the line still counts as mixed.
        grid.SelectedObjects = new object[] { Item(first), Item(second) };

        Settle(grid);
        TemplateRows(grid);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsCollapsed, Is.False, "selecting them all folded a node");
            Assert.That(second.IsCollapsed, Is.False, "selecting them all folded a node");
            Assert.That(third.IsCollapsed, Is.True, "the one that was folded came open by being shown");
        });
    }

    // A LINE IS ABOUT THE OBJECTS THAT HAVE WHAT IT ASKS FOR. A band takes wires along with the nodes, and a wire has
    // no "folded" - it is not an object that disagrees, it is one the line is not about. Counted as a disagreement, the
    // line goes mixed, and the rule for mixed is "make them all agree" - which folded every node in the graph.
    [Test]
    public void ALineIgnoresWhatCannotAnswerIt()
    {
        var first = new Graph.GraphNode { Kind = "Add", Title = "Add" };
        var second = new Graph.GraphNode { Kind = "Mix", Title = "Mix" };

        var folded = new BooleanProperty { Header = "Folded", Binding = new Binding("Model.IsCollapsed") };
        var section = new PropertySection { Header = "Node", IsExpanded = true };
        section.Properties.Add(folded);

        var grid = new PropertyGrid { Template = Chrome() };
        grid.Sections.Add(section);

        // A wire is not an ElementItem at all, so "Model.IsCollapsed" finds nothing on it - which is exactly what a
        // selection of a whole graph hands the line.
        grid.SelectedObjects = new object[] { Item(first), new StrokeItem(Vector2.Zero, Brushes.White, 2), Item(second) };

        Settle(grid);
        TemplateRows(grid);

        var row = RowOf(grid, folded);

        Assert.Multiple(() =>
        {
            Assert.That(row.IsMixed, Is.False, "something that cannot answer was read as an object that disagrees");
            Assert.That(row.Value, Is.False, "the line does not show what the nodes actually say");
            Assert.That(first.IsCollapsed, Is.False, "showing the line folded a node");
            Assert.That(second.IsCollapsed, Is.False, "showing the line folded a node");
        });
    }

    private static ElementItem Item(ICanvasNode model) =>
        new(new CanvasNode(), new Rect(0, 0, 190, 110)) { Model = model };

    private static void Settle(PropertyGrid grid)
    {
        grid.Measure(new Size(400, 400), force: true);
        grid.Arrange(new Rect(0, 0, 400, 400));
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
    }

    // Rows are REUSED as the grid rebuilds, and a line that reads nothing - an item's name - has to show nothing. One
    // that kept what the row held before it showed the count of the list that used to stand in that place.
    [Test]
    public void ALineThatReadsNothingShowsNothing()
    {
        var node = new CanvasNode { Inputs = 1, Outputs = 1 };
        var item = new ElementItem(node, new Rect(0, 0, 190, 110));

        var grid = Built(Section(item,
            Sockets("In", "Element.InputPins"),
            Sockets("Out", "Element.OutputPins")));

        // One more input, so the line that stood where the OUT list's name stood is now an input's name.
        node.Inputs = 2;

        Assert.That(RowsWhoseHeaderIs(grid, "In 2")?.Value, Is.Null, "the line is wearing the value of what it was");
    }

    private static ItemsProperty Sockets(string header, string path)
    {
        var sockets = new ItemsProperty
        {
            Header = header,
            IsExpanded = true,
            Binding = new Binding(path),
            ItemHeader = new Binding("Name")
        };

        sockets.Children.Add(new StringProperty { Header = "Name", Binding = new Binding("Name") });

        return sockets;
    }

    private class Doing : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static PropertyRow RowsWhoseHeaderIs(PropertyGrid grid, string header)
    {
        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow row && Equals(row.Definition?.Header, header)) return row;
            }
        }

        return null;
    }

    private static List<PropertyRow> RowsOf(PropertyGrid grid, PropertyDefinition definition)
    {
        var found = new List<PropertyRow>();

        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow row && ReferenceEquals(row.Definition, definition)) found.Add(row);
            }
        }

        return found;
    }

    private static List<string> Headers(PropertyGrid grid)
    {
        var found = new List<string>();

        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow row) found.Add(row.Definition?.Header as string);
            }
        }

        return found;
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

    // ...AND THE OBJECT IT IS POINTED AT is reachable too, through Inspected. Two different questions on one line - the
    // panel's own view-model and the thing being edited - so they are two different ways in, and neither takes the
    // other's slot.
    [Test]
    public void ADefinitionCanBeWrittenToDependOnWhatItIsPointedAt()
    {
        var target = new Target { IsEnabled = true };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        new Self { Path = "Inspected.IsEnabled" }.Apply(scale, nameof(PropertyDefinition.IsVisible));

        var grid = Built(Section(target, scale));

        Assert.That(RowsOf(grid, scale), Has.Count.EqualTo(1), "the line was written for an object that admits it");

        // ...and the same line, pointed at something that says no, is not built at all.
        target.IsEnabled = false;
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        grid.Rebuild();

        Assert.That(RowsOf(grid, scale), Is.Empty, "the line stayed after the object it is about said it should not");
    }

    // NOTHING WHILE SEVERAL ARE SELECTED: the line then stands for all of them, and no single one of them is what it is
    // about - so a condition read off "the object" has no honest answer and the line is left out.
    [Test]
    public void WhatALineIsPointedAtIsNothingForAMultipleSelection()
    {
        var one = new Target { IsEnabled = true };
        var two = new Target { IsEnabled = true };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        new Self { Path = "Inspected.IsEnabled", FallbackValue = false }
            .Apply(scale, nameof(PropertyDefinition.IsVisible));

        var section = new PropertySection { Header = "General", IsExpanded = true };

        section.Properties.Add(scale);

        var grid = new PropertyGrid { Template = Chrome(), SelectedObjects = new[] { one, two } };

        grid.Sections.Add(section);
        grid.Measure(new Size(400, 400), force: true);
        grid.Arrange(new Rect(0, 0, 400, 400));

        Assert.That(scale.Inspected, Is.Null, "one of several selected objects was taken as what the line is about");
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

    // ...and it comes BACK. An inspector whose lines belong to what is selected turns them on and off while it is open,
    // so the grid has to hear a definition change its mind - reading IsVisible once at build time made it a switch that
    // only ever worked before anybody could see it.
    [Test]
    public void ALineTurnedVisibleGetsItsRow()
    {
        var target = new Target();
        var hidden = new NumericProperty { Header = "Scale", Binding = new Binding("Scale"), IsVisible = false };
        var grid = Built(Section(target, hidden));

        Assert.That(RowOf(grid, hidden), Is.Null);

        hidden.IsVisible = true;

        Assert.That(RowOf(grid, hidden), Is.Not.Null, "the grid has to rebuild when a line says it is shown again");
    }

    // A rebuild asked for FROM INSIDE a rebuild - a definition whose IsVisible binding settles while the rows are being
    // built - must not re-enter: the inner pass fills the host and the outer one, still holding its place, adds the rest
    // of the sections a second time. A panel holding the same child twice makes the paint order's "next sibling" chain
    // point at itself, and the frame recorder walks that chain for ever: the whole application freezes, on the first
    // change that records a structural frame, with nothing in the stack to blame it on.
    // Reading its one property asks a definition to become visible - which is what a binding settling mid-pass does,
    // and the only way to ask for a rebuild from INSIDE one.
    private sealed class Tripwire : INotifyPropertyChanged
    {
        private bool _tripped;

        public PropertyDefinition Flip;

        public string Name
        {
            get
            {
                if (!_tripped && Flip != null)
                {
                    _tripped = true;
                    Flip.IsVisible = true;
                }

                return "tripwire";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    [Test]
    public void ARebuildAskedForFromInsideOneDoesNotDoubleTheSections()
    {
        var target = new Tripwire();
        var shown = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var late = new StringProperty { Header = "Late", Binding = new Binding("Name"), IsVisible = false };
        target.Flip = late;

        var grid = Built(Section(target, shown, late));
        var section = grid.Sections[0];

        Assert.Multiple(() =>
        {
            Assert.That(Count(grid, section), Is.EqualTo(1),
                "the section is in the host ONCE, however many rebuilds were asked for while one was running");
            Assert.That(RowOf(grid, late), Is.Not.Null, "and the line the inner pass asked for is there");
        });
    }

    private static int Count(IUIComponent root, IUIComponent wanted)
    {
        var found = 0;
        var stack = new Stack<IUIComponent>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (ReferenceEquals(node, wanted)) found++;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return found;
    }

    // The WHOLE LINE opens a composite, not only the fourteen pixels of chevron beside it. Raised on the LABEL inside
    // the name presenter, which is where a press actually lands - and the point of the test: MouseLeftButtonDown is
    // DIRECT, so a handler on the presenter heard nothing at all, and only a bubbling event reaches the row from
    // whatever the template put under the pointer.
    [Test]
    public void PressingTheNameOpensAComposite()
    {
        var target = new Target();
        var x = new NumericProperty { Header = "X", Binding = new Binding("Scale") };
        // The header IS the label, so the press can be raised on the very element the name presenter holds - which is
        // where a real press lands, and the whole point of the test.
        var label = new TextBlock { Text = "Position" };
        var composite = new CompositeProperty { Header = label };
        composite.Children.Add(x);

        var grid = Built(Section(target, composite));
        var row = RowOf(grid, composite);

        row.Template = new Adamantium.UI.Core.Templates.ControlTemplate(() =>
        {
            var name = new ContentPresenter();
            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = name };
            result.RegisterName("PART_Name", name);
            return result;
        });

        row.Measure(new Size(400, 40), force: true);
        row.Arrange(new Rect(0, 0, 400, 40));

        ((IObservableComponent)label).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0) { RoutedEvent = Mouse.MouseDownEvent });

        Assert.Multiple(() =>
        {
            Assert.That(composite.IsExpanded, Is.True);
            Assert.That(RowOf(grid, x), Is.Not.Null);
        });
    }

    // ...and anywhere ELSE on the line does too - the value half of a composite row holds no editor, so there is nothing
    // there for a press to mean instead.
    [Test]
    public void PressingTheValueHalfOpensACompositeToo()
    {
        var target = new Target();
        var x = new NumericProperty { Header = "X", Binding = new Binding("Scale") };
        var composite = new CompositeProperty { Header = "Position" };
        composite.Children.Add(x);

        var grid = Built(Section(target, composite));
        var row = RowOf(grid, composite);
        var value = new TextBlock { Text = "-" };

        row.Template = new Adamantium.UI.Core.Templates.ControlTemplate(() =>
        {
            var host = new ContentPresenter { Content = value };
            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = host };
            result.RegisterName("PART_Layout", host);
            return result;
        });

        row.Measure(new Size(400, 40), force: true);
        row.Arrange(new Rect(0, 0, 400, 40));

        ((IObservableComponent)value).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0) { RoutedEvent = Mouse.MouseDownEvent });

        Assert.That(composite.IsExpanded, Is.True);
    }

    // ...but NOT through a button on that line. One press cannot both press the button and fold away the thing it was
    // pressed on.
    [Test]
    public void PressingAButtonOnACompositeDoesNotOpenIt()
    {
        var target = new Target();
        var x = new NumericProperty { Header = "X", Binding = new Binding("Scale") };
        var composite = new CompositeProperty { Header = "Position" };
        composite.Children.Add(x);

        var grid = Built(Section(target, composite));
        var row = RowOf(grid, composite);
        var button = new Adamantium.UI.Controls.Buttons.Button { Content = "+" };

        row.Template = new Adamantium.UI.Core.Templates.ControlTemplate(() =>
        {
            var host = new ContentPresenter { Content = button };
            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = host };
            result.RegisterName("PART_Layout", host);
            return result;
        });

        row.Measure(new Size(400, 40), force: true);
        row.Arrange(new Rect(0, 0, 400, 40));

        ((IObservableComponent)button).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0) { RoutedEvent = Mouse.MouseDownEvent });

        Assert.That(composite.IsExpanded, Is.False, "the press went through the button and folded the row");
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

    private static List<string> HeadersOf(PropertyGrid grid)
    {
        var headers = new List<string>();
        foreach (var section in grid.Sections)
        {
            if (section.Content is not IUIComponent host) continue;
            foreach (var child in host.VisualChildren)
            {
                if (child is PropertyRow row) headers.Add(row.Definition.Header as string);
            }
        }

        return headers;
    }

    private static PropertyGrid Searchable()
    {
        var target = new Target();

        var general = new PropertySection { Header = "General", Target = target, IsExpanded = true };
        general.Properties.Add(new StringProperty { Header = "Name", Binding = new Binding("Name") });
        general.Properties.Add(new BooleanProperty { Header = "Enabled", Binding = new Binding("IsEnabled") });

        var transform = new PropertySection { Header = "Transform", Target = target, IsExpanded = true };
        var position = new CompositeProperty { Header = "Position", IsExpanded = true };
        position.Children.Add(new NumericProperty { Header = "Scale X", Binding = new Binding("Scale") });
        position.Children.Add(new NumericProperty { Header = "Offset", Binding = new Binding("Scale") });
        transform.Properties.Add(position);

        return Built(general, transform);
    }

    // An inspector of a real object runs to dozens of rows, and scrolling for the one being looked for is the thing a
    // search replaces.
    [Test]
    public void SearchKeepsOnlyTheRowsThatCarryIt()
    {
        var grid = Searchable();
        Assert.That(HeadersOf(grid), Does.Contain("Enabled"));

        grid.SearchText = "name";
        Assert.That(HeadersOf(grid), Is.EqualTo(new[] { "Name" }), "and case is not what a search is about");

        grid.SearchText = null;
        Assert.That(HeadersOf(grid), Does.Contain("Enabled"), "cleared, the inspector is whole again");
    }

    // A composite whose CHILD is the answer has to stand: hiding the parent would hide what was found.
    [Test]
    public void SearchKeepsTheParentOfWhatItFound()
    {
        var grid = Searchable();

        grid.SearchText = "offset";
        Assert.That(HeadersOf(grid), Is.EqualTo(new[] { "Position", "Offset" }));
    }

    // Asking for a section by name means the whole of it, not the one row that happens to repeat the word.
    [Test]
    public void SearchingForASectionKeepsAllOfIt()
    {
        var grid = Searchable();

        grid.SearchText = "transform";
        Assert.That(HeadersOf(grid), Is.EqualTo(new[] { "Position", "Scale X", "Offset" }));
    }

    // A section with no answer is not shown empty: a column of bare headers reads as a result.
    [Test]
    public void ASectionWithNoAnswerIsNotShown()
    {
        var grid = Searchable();

        Assert.That(((IUIComponent)grid.Sections[0]).VisualParent, Is.Not.Null, "General is on screen to begin with");

        grid.SearchText = "offset";
        Assert.That(((IUIComponent)grid.Sections[0]).VisualParent, Is.Null, "and gone - it has nothing to answer with");
    }

    // A search that hides its own results behind something folded is worse than no search - and what the user folded
    // himself comes back once it is cleared.
    [Test]
    public void SearchOpensWhatItNeedsAndGivesItBack()
    {
        var grid = Searchable();
        grid.Sections[1].IsExpanded = false;

        grid.SearchText = "offset";
        Assert.That(grid.Sections[1].IsExpanded, Is.True, "opened, or the answer is behind a fold");

        grid.SearchText = null;
        Assert.That(grid.Sections[1].IsExpanded, Is.False, "and folded again, the way the user left it");
    }

    // One way out of a search, reached two ways - and the row it was narrowed down to may well have the focus by then,
    // which is why Escape is the inspector's and not only the field's.
    [Test]
    public void ClearSearchDropsIt()
    {
        var grid = Searchable();
        grid.SearchText = "offset";

        Assert.That(grid.HasSearchText, Is.True, "the theme reads this to show the cross");

        grid.ClearSearch();
        Assert.Multiple(() =>
        {
            Assert.That(grid.SearchText, Is.Null);
            Assert.That(grid.HasSearchText, Is.False);
            Assert.That(HeadersOf(grid), Does.Contain("Enabled"), "and every property is back");
        });
    }

    [Test]
    public void EscapeDropsTheSearch()
    {
        var grid = Searchable();
        grid.SearchText = "offset";

        var pressed = new KeyEventArgs(KeyboardDevice.CurrentDevice, Key.Escape, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        grid.RaiseEvent(pressed);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SearchText, Is.Null);
            Assert.That(pressed.Handled, Is.True, "and the key is spent - it undid something");
        });
    }

    // Escape with nothing to undo belongs to whatever else wants it - a dialog to close, an edit to abandon.
    [Test]
    public void EscapeWithNoSearchIsLeftAlone()
    {
        var grid = Searchable();

        var pressed = new KeyEventArgs(KeyboardDevice.CurrentDevice, Key.Escape, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        grid.RaiseEvent(pressed);

        Assert.That(pressed.Handled, Is.False);
    }

    // The mark is the point as much as the button: an inspector of forty rows has to say which of them were touched.
    [Test]
    public void ARowMarksItselfWhenItHoldsSomethingOtherThanTheDefault()
    {
        var target = new Target { Name = "entity" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name"), DefaultValue = "entity" };
        var grid = Built(Section(target, name));

        var row = RowOf(grid, name);
        Assert.That(row.IsModified, Is.False, "it is at its default");

        grid.Write(row, "changed");
        Assert.That(row.IsModified, Is.True);

        Assert.That(row.ResetToDefault(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(target.Name, Is.EqualTo("entity"));
            Assert.That(row.IsModified, Is.False, "and the mark goes with it");
        });
    }

    // Without a default there is nothing to go back to, and the row must not pretend otherwise.
    [Test]
    public void ARowWithNoDefaultOffersNoReset()
    {
        var target = new Target { Name = "anything" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name") };
        var grid = Built(Section(target, name));

        var row = RowOf(grid, name);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsModified, Is.False);
            Assert.That(row.ResetToDefault(), Is.False);
        });
    }

    // NULL is a perfectly good default for a reference, which is why having one is a flag and not a null check.
    [Test]
    public void ADefaultOfNothingIsStillADefault()
    {
        var target = new Target { Name = "something" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name"), DefaultValue = null };
        var grid = Built(Section(target, name));

        var row = RowOf(grid, name);
        Assert.That(row.IsModified, Is.True, "it holds a name, and the default is none");

        Assert.That(row.ResetToDefault(), Is.True);
        Assert.That(target.Name, Is.Null);
    }

    // A read-only row cannot take a write, so a mark there would promise a button that does nothing.
    [Test]
    public void AReadOnlyRowIsNeverMarkedModified()
    {
        var target = new Target();
        var locked = new StringProperty
        {
            Header = "Locked",
            Binding = new Binding("Locked"),
            IsReadOnly = true,
            DefaultValue = 0
        };
        var grid = Built(Section(target, locked));

        var row = RowOf(grid, locked);
        Assert.Multiple(() =>
        {
            Assert.That(row.IsModified, Is.False);
            Assert.That(row.ResetToDefault(), Is.False, "and the reset is refused like any other write");
            Assert.That(target.Locked, Is.EqualTo(7));
        });
    }

    // Several objects that disagree cannot all be at the default - at most one of them is - and one reset is what makes
    // them agree again.
    [Test]
    public void ResettingSeveralObjectsBringsThemAllBack()
    {
        var first = new Target { Name = "one" };
        var second = new Target { Name = "two" };
        var name = new StringProperty { Header = "Name", Binding = new Binding("Name"), DefaultValue = "entity" };
        var grid = MultiGrid(name, first, second);

        var row = RowOf(grid, name);
        Assert.That(row.IsModified, Is.True, "they disagree, so they are not all at the default");

        Assert.That(row.ResetToDefault(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(first.Name, Is.EqualTo("entity"));
            Assert.That(second.Name, Is.EqualTo("entity"));
            Assert.That(row.IsMixed, Is.False);
            Assert.That(row.IsModified, Is.False);
        });
    }

    // The generated inspector gets defaults with no markup at all: they are what a FRESH instance of the type holds.
    [Test]
    public void TheBuilderReadsDefaultsOffTheType()
    {
        var sections = new PropertyDefinitionBuilder().BuildSections(typeof(Target));

        PropertyDefinition name = null;
        foreach (var section in sections)
        {
            foreach (var property in section.Properties)
            {
                if (property.Header as string == "Name") name = property;
            }
        }

        Assert.That(name, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(name.HasDefault, Is.True);
            Assert.That(name.DefaultValue, Is.EqualTo("entity"), "what a new Target is called");
        });
    }

    // The field is the only way back to the whole inspector. Taking it away while a search is running would leave the
    // panel narrowed down with nothing on it to widen it again - and looking like it had lost most of its properties.
    [Test]
    public void HidingTheSearchDropsIt()
    {
        var grid = Searchable();
        grid.SearchText = "offset";
        Assert.That(HeadersOf(grid), Does.Not.Contain("Name"));

        grid.ShowSearch = false;
        Assert.Multiple(() =>
        {
            Assert.That(grid.SearchText, Is.Null);
            Assert.That(HeadersOf(grid), Does.Contain("Name"), "every property is back");
        });
    }

    [Test]
    public void TheSearchIsThereUnlessItIsTurnedOff()
    {
        Assert.That(new PropertyGrid().ShowSearch, Is.True);
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

    // A WRITE IS ANNOUNCED TWICE - before and after - and that is ALL an inspector owes anybody. What becomes of it is
    // not its business: it knows nothing of planes, scenes or histories, and must not, or every inspector in the
    // application would carry a canvas's vocabulary. Whoever cares listens - see InfiniteCanvas.Inspector.
    [Test]
    public void AWriteIsAnnouncedBeforeAndAfter()
    {
        var target = new Target { Scale = 3 };
        var scale = new NumericProperty { Header = "Scale", Binding = new Binding("Scale") };

        var grid = Built(Section(target, scale));

        var said = new List<string>();

        grid.ValueChanging += (_, about) => said.Add($"before {grid.ValueOf(target, about.Property)}");
        grid.ValueChanged += (_, about) => said.Add($"after {grid.ValueOf(target, about.Property)}");

        grid.Write(RowOf(grid, scale), 10.0);

        Assert.That(said, Is.EqualTo(new[] { "before 3", "after 10" }),
            "the value before the write exists only between the two, and that is what undo is made of");
    }
}
