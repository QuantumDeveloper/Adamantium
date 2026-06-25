using Adamantium.Mathematics;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class InputHitTestTests
{
    // Hit-testing must descend THROUGH a non-input container (a Border / any Decorator) to reach the interactive
    // content it wraps. The hit-test used to filter to IInputComponent children, so anything inside a Border was dead
    // to the mouse - e.g. a ScrollBar whose template root is a Border had an unclickable thumb.
    [Test]
    public void HitTest_DescendsThroughBorder_ToReachInteractiveContent()
    {
        var inner = new Button { Width = 100, Height = 50 };
        var border = new Border { Child = inner };
        var root = new Grid();
        root.Children.Add(border);

        root.Measure(new Size(100, 50));
        root.Arrange(new Rect(0, 0, 100, 50));

        var hit = ((IInputComponent)root).HitTest(new Vector2(50, 25));

        Assert.That(hit, Is.SameAs(inner), "the button inside the Border must be the hit target, not the Grid");
    }
}
