using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// The GESTURE end of tab dragging - what the pointer has to be doing for a drag to begin at all. The reorder maths it
// leads to is covered by TabStripScrollerTests; here the question is only whether the strip picks a tab up, and along
// which axis it then moves.
[TestFixture]
public class TabDragGestureTests
{
    // The button state is app-global on the mouse device: a test that left it pressed would make every test after it
    // think a drag is in progress.
    [TearDown]
    public void TearDown() => ReleaseButton();

    private static void HoldButton() => Mouse.PrimaryDevice.UpdateButtonStates(InputModifiers.LeftMouseButton);

    private static void ReleaseButton() => Mouse.PrimaryDevice.UpdateButtonStates(InputModifiers.None);

    private static void PressAt(TabItem tab, double x, double y)
    {
        Mouse.PrimaryDevice.SetExternalPosition(tab, new PixelPoint(x, y));
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left, MouseButtonState.Pressed,
            InputModifiers.LeftMouseButton, 0) { RoutedEvent = InputUIComponent.MouseLeftButtonDownEvent };
        ((IObservableComponent)tab).RaiseEvent(args);
    }

    private static void MoveTo(TabItem tab, double x, double y)
    {
        Mouse.PrimaryDevice.SetExternalPosition(tab, new PixelPoint(x, y));
        var args = new MouseEventArgs(Mouse.PrimaryDevice, InputModifiers.None, 0)
        {
            RoutedEvent = Mouse.MouseMoveEvent
        };
        ((IObservableComponent)tab).RaiseEvent(args);
    }

    private static bool PickedUp(TabItem tab) => tab.ZIndex != 0;

    // Pressing a FOLDED strip expands the panel on the preview of the press, which re-templates the tree under the
    // cursor - so the button-up lands in the new tree and never reaches this tab. If the tab took its own latch as the
    // authority on "the button is down", that latch stayed true for good, and afterwards a plain HOVER walked straight
    // into the drag path and slid the tabs out of the strip. The device knows the button is up; ask it.
    [Test]
    public void MoveWithNoButtonHeld_DoesNotPickTheTabUp()
    {
        var (tc, tabs) = ArrangedStrip(Orientation.Horizontal, 40, 100, 60);

        PressAt(tabs[0], 10, 10);   // the press the tab did see
        ReleaseButton();            // ...whose release went somewhere else - the device is the only thing that knows
        MoveTo(tabs[0], 60, 10);
        MoveTo(tabs[0], 120, 10);

        Assert.That(PickedUp(tabs[0]), Is.False, "nothing is held down - a hover must not drag the tab");
        Assert.That(tc.Items.IndexOf(tabs[0]), Is.EqualTo(0), "and nothing was reordered");
    }

    // The other half of the same guard: with the button genuinely held, a move past the threshold MUST still pick the
    // tab up. A fix for a phantom drag that also kills the real one is no fix.
    [Test]
    public void MoveWithTheButtonHeld_PicksTheTabUp()
    {
        var (_, tabs) = ArrangedStrip(Orientation.Horizontal, 40, 100, 60);

        HoldButton();
        PressAt(tabs[0], 10, 10);
        MoveTo(tabs[0], 60, 10);

        Assert.That(PickedUp(tabs[0]), Is.True, "a real drag still begins");
    }

    // A strip that forbids dragging leaves its tabs selectable and nothing more - which is what a tool panel folded
    // against an edge shows: buttons that bring the panel back.
    [Test]
    public void AllowTabDragFalse_LeavesTabsWhereTheyAre()
    {
        var (_, tabs) = ArrangedStrip(Orientation.Vertical, 40, 100, 60);
        var tc = tabs[0].GetVisualAncestors().OfType<TabControl>().First();
        tc.AllowTabDrag = false;

        HoldButton();
        PressAt(tabs[0], 10, 10);
        MoveTo(tabs[0], 10, 60);

        Assert.That(PickedUp(tabs[0]), Is.False, "the strip forbids picking tabs up");
    }

    // The drag axis must come from the panel that lays the tabs out, not from TabStripPlacement: a tool group folded
    // against a side edge keeps its tabs' placement (Bottom) and turns its PANEL vertical, and a drag that trusted the
    // placement moved them along X - sideways out of the narrow column, off the screen.
    [Test]
    public void VerticalPanel_WithBottomPlacement_DragsAlongTheColumn()
    {
        var (tc, tabs) = ArrangedStrip(Orientation.Vertical, 24, 24, 24);
        tc.TabStripPlacement = TabStripPlacement.Bottom;

        tc.BeginDrag(tabs[0], 5.0);    // 5px down into tab 0
        tc.UpdateDrag(tabs[0], 30.0);  // ...dragged 25px DOWN the column

        var transform = tabs[0].RenderTransform as Transform;
        Assert.Multiple(() =>
        {
            Assert.That(transform?.TranslateY ?? 0, Is.EqualTo(25).Within(0.5), "the tab follows the pointer DOWN the column");
            Assert.That(transform?.TranslateX ?? 0, Is.EqualTo(0).Within(0.001), "and never sideways out of it");
        });
    }

    /// <summary>A strip is laid out as TWO rows - pinned and ordinary - and a drag belongs to exactly one of them.
    /// Reordering walked the single item list and slid every tab whose index fell between the start and the target, so
    /// tabs in the OTHER row moved in lockstep with tabs they have nothing to do with. The rows were never linked; they
    /// were sharing one index range.</summary>
    [Test]
    public void DraggingATab_LeavesTheOtherRowWhereItIs()
    {
        var (tc, tabs) = ArrangedStrip(Orientation.Horizontal, 40, 40, 40, 40);
        tabs[0].IsPinned = true;   // the pinned row
        tabs[1].IsPinned = true;
                                   // tabs 2 and 3 are the ordinary row

        tc.BeginDrag(tabs[3], 125.0);   // grabbed 5px into the last ordinary tab (it starts at 120)
        tc.UpdateDrag(tabs[3], 80.0);   // ...dragged back past tab 2's centre, so the gap opens at index 2

        Assert.Multiple(() =>
        {
            Assert.That(tabs[2].RenderTransform, Is.Not.Null, "the tab it passed IN ITS OWN ROW slides aside");
            Assert.That(tabs[0].RenderTransform, Is.Null, "and the pinned row is not touched at all");
            Assert.That(tabs[1].RenderTransform, Is.Null);
        });
    }

    /// <summary>A visual root with a client viewport, mirroring a window's role - the press path focuses the tab, and
    /// focus needs a root to walk up to.</summary>
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

    // A TabControl whose strip is a content-sized TabPanel, realized + arranged so every tab has real Bounds. Extents
    // are the sizes ALONG the strip axis, so the same numbers describe either orientation.
    private static (TabControl tc, TabItem[] tabs) ArrangedStrip(Orientation orientation, params double[] extents)
    {
        var vertical = orientation == Orientation.Vertical;
        var tc = new TabControl();
        var tabs = extents
            .Select(e => new TabItem { Width = vertical ? 24 : e, Height = vertical ? e : 24 })
            .ToArray();
        foreach (var t in tabs) tc.Items.Add(t);

        tc.ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
        {
            RootComponent = new TabPanel { Orientation = orientation }
        });
        tc.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });

        var root = new TestWindowRoot { Width = 1000, Height = 1000, ClientWidth = 1000, ClientHeight = 1000 };
        root.Children.Add(tc);
        root.Measure(new Size(1000, 1000));
        root.Arrange(new Rect(0, 0, 1000, 1000));
        return (tc, tabs);
    }
}
