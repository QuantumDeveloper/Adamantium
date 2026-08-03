using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Phase 0 of keyboard navigation: Tab and Shift+Tab, with the route ASKED of the panels rather than computed by a
/// tree walker. No window and no GPU - the navigator only needs a visual tree and the focus manager.
/// </summary>
[TestFixture]
public class KeyboardNavigationTests
{
    [TearDown]
    public void TearDown() => FocusManager.ResetFocus();

    private static Button NewButton(string name) => new() { Name = name, Width = 40, Height = 20 };

    // A root that is NOT a panel, so the walk has to leave the panel it starts in - as it does in a real window.
    private static Border Root(Panel content) => new() { Width = 200, Height = 100, Child = content };

    [Test]
    public void TabWalksTheChildrenInOrder()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        stack.Children.Add(c);
        Root(stack);

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Next), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(b));
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Next), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(c));
        });
    }

    [Test]
    public void ShiftTabWalksBack()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        Root(stack);

        FocusManager.Focus(b);
        KeyboardNavigation.Move(FocusNavigationDirection.Previous);

        Assert.That(FocusManager.Focused, Is.SameAs(a));
    }

    /// <summary>Off the end, Tab comes round again - otherwise the focus reaches the last control and the keyboard is
    /// stuck there.</summary>
    [Test]
    public void TabWrapsAtTheEnd()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        Root(stack);

        FocusManager.Focus(b);
        KeyboardNavigation.Move(FocusNavigationDirection.Next);

        Assert.That(FocusManager.Focused, Is.SameAs(a));
    }

    [Test]
    public void ShiftTabWrapsToTheLast()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        Root(stack);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Previous);

        Assert.That(FocusManager.Focused, Is.SameAs(b), "and enters the container from its END");
    }

    /// <summary>The whole point of the panel answering: running out of one panel carries on into the next, with neither
    /// panel knowing the other exists.</summary>
    [Test]
    public void TabLeavesOnePanelAndEntersTheNext()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var left = new StackPanel();
        left.Children.Add(a);
        left.Children.Add(b);

        var c = NewButton("c");
        var right = new StackPanel();
        right.Children.Add(c);

        var outer = new StackPanel { Orientation = Orientation.Horizontal };
        outer.Children.Add(left);
        outer.Children.Add(right);
        Root(outer);

        FocusManager.Focus(b);
        KeyboardNavigation.Move(FocusNavigationDirection.Next);

        Assert.That(FocusManager.Focused, Is.SameAs(c));
    }

    [Test]
    public void ANonTabStopIsSkipped()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        KeyboardNavigation.SetIsTabStop(b, false);

        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        stack.Children.Add(c);
        Root(stack);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Next);

        Assert.That(FocusManager.Focused, Is.SameAs(c));
    }

    [Test]
    public void ADisabledControlIsSkipped()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        b.IsEnabled = false;

        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(b);
        stack.Children.Add(c);
        Root(stack);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Next);

        Assert.That(FocusManager.Focused, Is.SameAs(c));
    }

    /// <summary>A container with nothing focusable inside is a gap on screen, not a dead end for the keyboard.</summary>
    [Test]
    public void AnEmptyContainerIsSteppedOver()
    {
        var a = NewButton("a");
        var c = NewButton("c");
        var empty = new StackPanel();
        empty.Children.Add(new TextBlock { Text = "just a label" });

        var stack = new StackPanel();
        stack.Children.Add(a);
        stack.Children.Add(empty);
        stack.Children.Add(c);
        Root(stack);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Next);

        Assert.That(FocusManager.Focused, Is.SameAs(c));
    }

    // --- Arrows: each panel answers from its own layout ---------------------------------------------------------

    [Test]
    public void AVerticalStackAnswersUpAndDown()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(a);
        stack.Children.Add(b);
        Root(stack);

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(b));
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Up), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(a));
        });
    }

    /// <summary>ACROSS the stacking axis there is nothing beside a child, so the stack says nothing and the focus stays -
    /// until some panel above it has an answer.</summary>
    [Test]
    public void AVerticalStackSaysNothingSideways()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(a);
        stack.Children.Add(b);
        Root(stack);

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.False);
            Assert.That(FocusManager.Focused, Is.SameAs(a));
        });
    }

    /// <summary>...and this is what that null is FOR: sideways out of a column is answered by the row that holds it.</summary>
    [Test]
    public void SidewaysOutOfAColumnIsAnsweredByThePanelAbove()
    {
        var a = NewButton("a");
        var left = new StackPanel { Orientation = Orientation.Vertical };
        left.Children.Add(a);

        var b = NewButton("b");
        var right = new StackPanel { Orientation = Orientation.Vertical };
        right.Children.Add(b);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(left);
        row.Children.Add(right);
        Root(row);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Right);

        Assert.That(FocusManager.Focused, Is.SameAs(b));
    }

    [Test]
    public void AGridNavigatesByRowAndColumn()
    {
        var topLeft = NewButton("topLeft");
        var topRight = NewButton("topRight");
        var bottomLeft = NewButton("bottomLeft");
        Grid.SetRow(topLeft, 0); Grid.SetColumn(topLeft, 0);
        Grid.SetRow(topRight, 0); Grid.SetColumn(topRight, 1);
        Grid.SetRow(bottomLeft, 1); Grid.SetColumn(bottomLeft, 0);

        var grid = new Grid();
        grid.Children.Add(topLeft);
        grid.Children.Add(topRight);
        grid.Children.Add(bottomLeft);
        Root(grid);

        FocusManager.Focus(topLeft);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(topRight), "same row, next column");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.False, "nothing below the top-right cell");
            Assert.That(FocusManager.Focused, Is.SameAs(topRight));
        });
    }

    /// <summary>The order the children were ADDED in is not the order they sit in - navigation follows the cells.</summary>
    [Test]
    public void AGridFollowsCellsNotChildOrder()
    {
        var first = NewButton("first");
        var second = NewButton("second");
        Grid.SetRow(first, 0); Grid.SetColumn(first, 1);    // added first, but sits on the RIGHT
        Grid.SetRow(second, 0); Grid.SetColumn(second, 0);

        var grid = new Grid();
        grid.Children.Add(first);
        grid.Children.Add(second);
        Root(grid);

        FocusManager.Focus(first);
        KeyboardNavigation.Move(FocusNavigationDirection.Left);

        Assert.That(FocusManager.Focused, Is.SameAs(second));
    }

    [Test]
    public void AUniformGridNavigatesByItsDerivedCells()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        var d = NewButton("d");
        var grid = new UniformGrid { Columns = 2, Rows = 2 };
        grid.Children.Add(a);   // [a][b]
        grid.Children.Add(b);   // [c][d]
        grid.Children.Add(c);
        grid.Children.Add(d);
        Root(grid);

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(c), "one row down is one whole line of cells");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(d));
        });
    }

    /// <summary>An items host attaches its realized containers straight to the VISUAL tree (AddVisualChild), so they
    /// never appear in Children - and navigation that ordered by Children found nothing there at all. A list could be
    /// entered and then not walked, with either the arrows or Tab. This panel does the same thing by hand, because that
    /// is the whole of the difference: children the layout put in the visual tree without the Children collection.</summary>
    private sealed class ItemsHostLikePanel : StackPanel
    {
        public void Realize(IUIComponent child) => AddVisualChild(child);
    }

    [Test]
    public void ChildrenAttachedToTheVisualTreeAloneAreStillWalked()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var host = new ItemsHostLikePanel { Orientation = Orientation.Vertical };
        host.Realize(a);
        host.Realize(b);
        Root(host);

        Assert.That(host.Children, Is.Empty, "sanity: this is the case Children knows nothing about");

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(b));
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Previous), Is.True, "and Tab was just as blind");
            Assert.That(FocusManager.Focused, Is.SameAs(a));
        });
    }

    /// <summary>A plain wrap panel has children of different sizes and lines of different lengths, so there is no
    /// items-per-line number to step by - it has to answer from the layout it actually produced. Four 40px tiles in a
    /// 100px panel wrap two to a line.</summary>
    [Test]
    public void AWrapPanelNavigatesByTheLinesItLaidOut()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        var d = NewButton("d");
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        wrap.Children.Add(a);   // [a][b]
        wrap.Children.Add(b);   // [c][d]
        wrap.Children.Add(c);
        wrap.Children.Add(d);

        var root = new Border { Width = 100, Height = 100, Child = wrap };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
        Assert.That(b.Bounds.Y, Is.EqualTo(a.Bounds.Y), "sanity: a and b share a line");
        Assert.That(c.Bounds.Y, Is.GreaterThan(a.Bounds.Y), "sanity: c wrapped onto the next one");

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(b), "along the line");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(d), "onto the next line, keeping the column");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Left), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(c));
        });
    }

    /// <summary>At the end of a LINE the flow carries on at the start of the next one, the way reading does - and
    /// backwards, at the end of the previous line. The line ends; the panel does not.</summary>
    [Test]
    public void AWrapPanelCarriesOnAtTheStartOfTheNextLine()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        wrap.Children.Add(a);   // [a][b]
        wrap.Children.Add(b);   // [c]
        wrap.Children.Add(c);

        var root = new Border { Width = 100, Height = 100, Child = wrap };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);

        FocusManager.Focus(b);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.True, "b ends its line");
            Assert.That(FocusManager.Focused, Is.SameAs(c), "...so the next one starts the line below");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Left), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(b), "and back the same way");
        });
    }

    /// <summary>A virtualizing panel PARKS an off-screen child - hidden, but still a child, still carrying the bounds it
    /// had when it was last on screen. Offering one as a neighbour put the focus nowhere and left the search carrying on
    /// from a position that no longer exists: a wall in the middle of a visible row, forwards only.</summary>
    [Test]
    public void AParkedChildIsNotOfferedAsANeighbour()
    {
        var a = NewButton("a");
        var parked = NewButton("parked");
        var c = NewButton("c");
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        wrap.Children.Add(a);
        wrap.Children.Add(parked);
        wrap.Children.Add(c);

        var root = new Border { Width = 300, Height = 100, Child = wrap };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);

        // Hidden AFTER it was laid out - so it keeps real, stale bounds, exactly like a parked container.
        parked.Visibility = Visibility.Collapsed;

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Right);

        Assert.That(FocusManager.Focused, Is.SameAs(c), "stepped over what is not on screen");
    }

    /// <summary>An arrow does not LEAVE a field of tiles: at its edge the key does nothing, and the focus stays where
    /// it was. One arrow too many should not cost you your place in the grid - Tab is how you leave, deliberately.</summary>
    [Test]
    public void ArrowsDoNotEscapeAWrapPanel()
    {
        var outside = NewButton("outside");
        var a = NewButton("a");
        var b = NewButton("b");
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        wrap.Children.Add(a);
        wrap.Children.Add(b);

        var outer = new StackPanel { Orientation = Orientation.Vertical };
        outer.Children.Add(wrap);
        outer.Children.Add(outside);
        var root = new Border { Width = 100, Height = 200, Child = outer };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);

        FocusManager.Focus(a);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Down), Is.False,
                "nothing below inside the panel - and the button under the panel is not the arrow's business");
            Assert.That(FocusManager.Focused, Is.SameAs(a));
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Next), Is.True, "Tab still leaves");
        });
    }

    // --- A list owns its own arrows -----------------------------------------------------------------------------

    // A list with a real items HOST: the arrows are answered by the host panel, so a list without one has nothing to
    // ask. Non-virtualizing, so every container is realized without needing a viewport.
    private static ListBox NewList(Orientation flow, params string[] items)
    {
        var presenter = new ItemsPresenter();
        var list = new ListBox
        {
            ItemsSource = items,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new StackPanel { Orientation = flow, IsVirtualizing = false }
            }),
            Template = new ControlTemplate(() =>
            {
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        var root = new Border { Width = 200, Height = 300, Child = list };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
        return list;
    }

    private static ListBox NewList(params string[] items) => NewList(Orientation.Vertical, items);

    private static void Press(ListBox list, Key key) =>
        list.RaiseEvent(new KeyEventArgs(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });

    /// <summary>The arrows move the SELECTION, which is what a list is for - the focus and the scroll follow it. The
    /// navigator never sees these keys, because a control that claims a key keeps it.</summary>
    [Test]
    public void ArrowsMoveTheListSelection()
    {
        var list = NewList("one", "two", "three");
        list.SelectedIndex = 0;

        Press(list, Key.DownArrow);
        Assert.That(list.SelectedIndex, Is.EqualTo(1));

        Press(list, Key.UpArrow);
        Assert.That(list.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void TheFirstArrowSelectsFromNothing()
    {
        var list = NewList("one", "two");

        Press(list, Key.DownArrow);

        Assert.That(list.SelectedIndex, Is.EqualTo(0), "the first press takes the end the key came from");
    }

    /// <summary>At either end the list KEEPS the key. Letting it through would hand the focus to whatever sits beside
    /// the list, which is not what an arrow at the bottom of a list means.</summary>
    [Test]
    public void AnArrowAtTheEndOfTheListGoesNoFurther()
    {
        var list = NewList("one", "two");
        list.SelectedIndex = 1;

        Press(list, Key.DownArrow);

        Assert.That(list.SelectedIndex, Is.EqualTo(1));
    }

    /// <summary>A list laid out in WRAPPED LINES: down means the row below, not the next chip along. The list has to ask
    /// its host panel which item comes next - stepping its own index by one made "down" mean "the neighbour", which in
    /// a grid of chips is the one to the RIGHT of it.</summary>
    [Test]
    public void ArrowsInAWrappedListFollowTheGridNotTheItemOrder()
    {
        var presenter = new ItemsPresenter();
        var list = new ListBox
        {
            ItemsSource = new[] { "a", "b", "c", "d", "e", "f" },           // [a][b][c]
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult    // [d][e][f]
            {
                RootComponent = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = 30,
                    ItemHeight = 20,
                    IsVirtualizing = false
                }
            }),
            Template = new ControlTemplate(() =>
            {
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        var root = new Border { Width = 95, Height = 200, Child = list };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
        list.SelectedIndex = 0;

        Assert.Multiple(() =>
        {
            Press(list, Key.RightArrow);
            Assert.That(list.SelectedIndex, Is.EqualTo(1), "right is the next chip along the line");
            Press(list, Key.DownArrow);
            Assert.That(list.SelectedIndex, Is.EqualTo(4), "down is the chip BELOW - not the next one along");
            Press(list, Key.LeftArrow);
            Assert.That(list.SelectedIndex, Is.EqualTo(3));
        });
    }

    /// <summary>A container marked "entered once" is a DOORWAY for Tab: the step into it lands inside, and the next
    /// step leaves the whole thing rather than walking its second child. Tested on a plain panel, since that is where
    /// the rule lives - a ListBox only opts into it (asserted below).</summary>
    [Test]
    public void TabEntersAOnceContainerAndThenLeavesItWhole()
    {
        var before = NewButton("before");
        var firstInside = NewButton("firstInside");
        var secondInside = NewButton("secondInside");
        var after = NewButton("after");

        var inner = new StackPanel();
        inner.Children.Add(firstInside);
        inner.Children.Add(secondInside);
        KeyboardNavigation.SetTabNavigation(inner, KeyboardNavigationMode.Once);

        var outer = new StackPanel();
        outer.Children.Add(before);
        outer.Children.Add(inner);
        outer.Children.Add(after);
        Root(outer);

        FocusManager.Focus(before);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Next), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(firstInside), "Tab steps INTO it");
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Next), Is.True);
            Assert.That(FocusManager.Focused, Is.SameAs(after), "and the next Tab steps over the rest of it");
        });
    }

    /// <summary>...and the arrows still walk INSIDE such a container: entering once is a Tab rule, not a ban on moving
    /// about once you are in there.</summary>
    [Test]
    public void ArrowsStillWalkInsideAOnceContainer()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var inner = new StackPanel { Orientation = Orientation.Vertical };
        inner.Children.Add(a);
        inner.Children.Add(b);
        KeyboardNavigation.SetTabNavigation(inner, KeyboardNavigationMode.Once);
        Root(inner);

        FocusManager.Focus(a);
        KeyboardNavigation.Move(FocusNavigationDirection.Down);

        Assert.That(FocusManager.Focused, Is.SameAs(b));
    }

    /// <summary>The ListBox opts into exactly that: not a stop itself, entered once. Stopping ON the list is what kept
    /// the keyboard out of it - Tab landed on the list and the next Tab left again, without ever reaching a row.</summary>
    [Test]
    public void AListIsADoorwayNotAStop()
    {
        var list = new ListBox();

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.GetIsTabStop(list), Is.False, "the list itself is not where Tab stops");
            Assert.That(KeyboardNavigation.GetTabNavigation(list), Is.EqualTo(KeyboardNavigationMode.Once));
            Assert.That(list.Focusable, Is.True, "it can still be focused - by a click, or to start the arrows");
        });
    }

    /// <summary>A sideways step must not roll off the end of a line into the start of the next one - which is exactly
    /// what plain index arithmetic would do.</summary>
    [Test]
    public void AUniformGridDoesNotWrapAroundALine()
    {
        var a = NewButton("a");
        var b = NewButton("b");
        var c = NewButton("c");
        var grid = new UniformGrid { Columns = 2, Rows = 2 };
        grid.Children.Add(a);   // [a][b]
        grid.Children.Add(b);   // [c]
        grid.Children.Add(c);
        Root(grid);

        FocusManager.Focus(b);

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardNavigation.Move(FocusNavigationDirection.Right), Is.False, "b is at the end of its line");
            Assert.That(FocusManager.Focused, Is.SameAs(b));
        });
    }
}
