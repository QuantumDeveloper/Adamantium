using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The two commands that carry a menu: <see cref="RibbonDropDownButton"/>, which only drops one, and
/// <see cref="RibbonSplitButton"/>, which also does something. What is tested here is the seam between the button and
/// the flyout - who opens it, who closes it, and which half of a split button a press belongs to.</summary>
[TestFixture]
public class RibbonMenuButtonTests
{
    [TearDown]
    public void TearDown() => Mouse.PrimaryDevice.UpdateButtonStates(InputModifiers.None);

    private static ContextMenu MenuWith(params string[] headers)
    {
        var menu = new ContextMenu();
        foreach (var header in headers) menu.Items.Add(new MenuItem { Header = header });
        return menu;
    }

    // Checked IS open: one state, so the lit command and the menu under it can never disagree.
    [Test]
    public void CheckingTheCommand_DropsItsMenu()
    {
        var button = new RibbonDropDownButton { DropDownMenu = MenuWith("Keep formatting") };

        button.IsChecked = true;
        Assert.That(button.DropDownMenu.IsOpen, Is.True);

        button.IsChecked = false;
        Assert.That(button.DropDownMenu.IsOpen, Is.False);
    }

    // The menu closes itself on an outside press, on Escape, and on a row being picked. Nothing else is listening, so a
    // command that did not hear it would stay lit over a flyout that is gone.
    [Test]
    public void TheMenuClosingItself_UnlightsTheCommand()
    {
        var button = new RibbonDropDownButton { DropDownMenu = MenuWith("Values only") };
        button.IsChecked = true;

        button.DropDownMenu.IsOpen = false;

        Assert.That(button.IsChecked, Is.False);
    }

    // A logical child, like the right-click ContextMenu a control already owns: that is what themes it and hands it the
    // DataContext its rows bind against.
    [Test]
    public void TheMenu_IsALogicalChild()
    {
        var button = new RibbonDropDownButton();
        var menu = MenuWith("Paste special");

        button.DropDownMenu = menu;

        Assert.That(button.LogicalChildren, Does.Contain(menu));
    }

    // Swapping the menu has to let the old one go BOTH ways - it is a logical child and it is subscribed to.
    [Test]
    public void SwappingTheMenu_ReleasesTheOldOne()
    {
        var button = new RibbonDropDownButton();
        var first = MenuWith("a");
        var second = MenuWith("b");

        button.DropDownMenu = first;
        button.DropDownMenu = second;
        button.IsChecked = true;

        Assert.Multiple(() =>
        {
            Assert.That(button.LogicalChildren, Does.Not.Contain(first));
            Assert.That(first.IsOpen, Is.False, "the menu it no longer has must not open");
            Assert.That(second.IsOpen, Is.True);
        });
    }

    // A command with nothing to drop must not throw when it is clicked - the menu is optional until an author fills it.
    [Test]
    public void ACommandWithNoMenu_TogglesQuietly()
    {
        var button = new RibbonDropDownButton();

        Assert.DoesNotThrow(() => button.IsChecked = true);
    }

    // --- The split button: which half a press belongs to -------------------------------------------------------------

    private const double ActionWidth = 60;
    private const double ArrowWidth = 20;

    private static ControlTemplate SplitTemplate() => new(() =>
    {
        // A row, so the two halves sit at known x - the theme's Grid would put them there too, but only because its
        // columns are Auto, and that is not what this test is about.
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var action = new Border { Width = ActionWidth, Height = 30 };
        var arrow = new Border { Width = ArrowWidth, Height = 30 };
        row.Children.Add(action);
        row.Children.Add(arrow);

        var result = new TemplateResult { RootComponent = row };
        result.RegisterName("PART_DropDownArea", arrow);
        return result;
    });

    private sealed class CountingCommand : ICommand
    {
        public int Runs;
        public event System.EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter = null) => true;
        public void Execute(object parameter = null) => Runs++;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
    }

    // GetPosition walks up to a ROOT visual, so the button has to hang under one - a bare Border throws.
    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    private static (RibbonSplitButton Button, CountingCommand Command) ArrangedSplitButton()
    {
        var command = new CountingCommand();
        var button = new RibbonSplitButton { Template = SplitTemplate(), Command = command };
        var root = new TestWindowRoot { Width = 200, Height = 40, ClientWidth = 200, ClientHeight = 40 };
        root.Children.Add(button);
        root.Measure(new Size(200, 40));
        root.Arrange(new Rect(0, 0, 200, 40));
        return (button, command);
    }

    private static void PressAndRelease(RibbonSplitButton button, double x, double y)
    {
        // The position is a SCREEN point converted by the ROOT, so it is the root that has to be named - naming the
        // button subtracts its offset twice and the press lands in the wrong half.
        IUIComponent root = button;
        while (root.VisualParent != null) root = root.VisualParent;
        Mouse.PrimaryDevice.SetExternalPosition((IInputComponent)root, new PixelPoint(x, y));

        // A click only counts while the pointer is over the button, and nothing hit-tests in a pure-CPU test.
        ((IObservableComponent)button).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0)
        { RoutedEvent = Mouse.MouseEnterEvent });

        Mouse.PrimaryDevice.UpdateButtonStates(InputModifiers.LeftMouseButton);
        ((IObservableComponent)button).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, InputModifiers.LeftMouseButton, 0)
        { RoutedEvent = InputUIComponent.MouseLeftButtonDownEvent });

        Mouse.PrimaryDevice.UpdateButtonStates(InputModifiers.None);
        ((IObservableComponent)button).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Released, InputModifiers.None, 0)
        { RoutedEvent = InputUIComponent.MouseLeftButtonUpEvent });
    }

    // The body is the ACTION. It must not drop the menu, or the split button is just a drop-down with extra steps.
    [Test]
    public void PressingTheBody_RunsTheCommandAndLeavesTheMenuShut()
    {
        var (button, command) = ArrangedSplitButton();
        button.DropDownMenu = MenuWith("Values only");

        PressAndRelease(button, ActionWidth / 2, 15);

        Assert.Multiple(() =>
        {
            Assert.That(command.Runs, Is.EqualTo(1));
            Assert.That(button.IsChecked, Is.False);
            Assert.That(button.DropDownMenu.IsOpen, Is.False);
        });
    }

    // The arrow is the MENU. It must not run the action - a split button exists precisely because those are two things.
    [Test]
    public void PressingTheArrow_DropsTheMenuAndLeavesTheCommandAlone()
    {
        var (button, command) = ArrangedSplitButton();
        button.DropDownMenu = MenuWith("Values only");

        PressAndRelease(button, ActionWidth + ArrowWidth / 2, 15);

        Assert.Multiple(() =>
        {
            Assert.That(command.Runs, Is.EqualTo(0));
            Assert.That(button.IsChecked, Is.True);
            Assert.That(button.DropDownMenu.IsOpen, Is.True);
        });
    }

    // The keyboard has no halves - Enter is the action, which is what a person reaching a command by Tab means by it.
    [Test]
    public void Enter_RunsTheAction_EvenAfterTheArrowWasPressed()
    {
        var (button, command) = ArrangedSplitButton();
        button.DropDownMenu = MenuWith("Values only");

        PressAndRelease(button, ActionWidth + ArrowWidth / 2, 15);   // the press that made the arrow the last half hit
        button.IsChecked = false;

        ((IObservableComponent)button).RaiseEvent(new KeyEventArgs(KeyboardDevice.CurrentDevice, Key.Enter, InputModifiers.None, 0)
        { RoutedEvent = Keyboard.KeyDownEvent });

        Assert.Multiple(() =>
        {
            Assert.That(command.Runs, Is.EqualTo(1));
            Assert.That(button.IsChecked, Is.False, "Enter runs the action, it does not drop the menu");
        });
    }

    // Sized by the group like any other command - a menu hanging off it changes nothing about that.
    [Test]
    public void AMenuCommand_IsSizedByItsGroup()
    {
        var button = new RibbonDropDownButton();
        Ribbon.SetMaxSize(button, RibbonSize.Medium);

        var panel = new RibbonGroupPanel();
        panel.Children.Add(button);
        panel.Measure(Size.Infinity);

        Assert.That(Ribbon.GetSize(button), Is.EqualTo(RibbonSize.Medium));
    }
}
