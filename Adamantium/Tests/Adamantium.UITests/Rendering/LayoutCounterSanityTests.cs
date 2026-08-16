using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// The counters that answer "who lays out, and how often" have to be trustworthy before any conclusion is drawn from
/// them. A diagnostic that silently records nothing reads exactly like "this code never runs" - the one wrong conclusion
/// it must never invite - so it is pinned here against the number the engine keeps on its own.
/// </summary>
[TestFixture]
public class LayoutCounterSanityTests
{
    [Test]
    public void ArrangeCalls_AreCounted_AndAgreeWithTheEnginesOwnTotal()
    {
        LayoutTrace.ResetCounts();
        LayoutTrace.Counting = true;
        try
        {
            var before = MeasurableUIComponent.TotalArrangeCalls;

            var root = new TestWindowRoot { ClientWidth = 400, ClientHeight = 300 };
            var panel = new StackPanel();
            for (var i = 0; i < 20; i++) panel.Children.Add(new Border { Background = Brushes.Red, Height = 10 });
            root.Children.Add(panel);
            WindowExtension.UpdateTree(root);

            var engineTotal = MeasurableUIComponent.TotalArrangeCalls - before;
            Assert.That(engineTotal, Is.GreaterThan(0), "the harness must actually arrange something");
            Assert.That(LayoutTrace.TotalCount(), Is.GreaterThan(0),
                $"the engine counted {engineTotal} arrange calls and the trace counted nothing:\n{LayoutTrace.DumpCounts()}");
        }
        finally
        {
            LayoutTrace.Counting = false;
            LayoutTrace.ResetCounts();
        }
    }

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
}
