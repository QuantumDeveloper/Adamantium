using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// An inherited change may STEP OVER an element that has nothing to be told - the value is not written, only staled, and
/// the next read resolves it from the ancestors. That is sound for the value and unsound for everything a WRITE does on
/// the way past: <c>AffectsRender</c> and its family are side effects of writing, so a stepped-over element ends up
/// holding the new value and still painting the old one.
/// <para>Which is exactly how it presented: a tab's label kept the resting colour while every probe - the trigger, the
/// presenter, the TextBlock's own Foreground - reported the selected one. Reading the value proves nothing here; what
/// has to be asserted is that the element was INVALIDATED.</para>
/// </summary>
[TestFixture]
public class InheritedChangeInvalidatesTests
{
    [OneTimeSetUp]
    public void EnsureAppContext() =>
        UIAppContext.Initialize(new FakeApp(new AdamantiumDependencyContainer()), null);

    [Test]
    public void AnInheritedForegroundChange_AsksTheTextToRepaint()
    {
        var text = new TextBlock { Text = "Shapes" };
        var parent = new Border { Child = text };
        parent.Foreground = new SolidColorBrush(Color.FromRgba(220, 40, 40, 255));

        // The signal the render side actually listens to, rather than a flag only a completed Render can clear.
        var toldToRedraw = false;
        void OnInvalidated(IUIComponent c) { if (ReferenceEquals(c, text)) toldToRedraw = true; }
        VisualTreeNotifications.ContentInvalidated += OnInvalidated;
        try
        {
            parent.Foreground = new SolidColorBrush(Color.FromRgba(40, 200, 90, 255));
        }
        finally
        {
            VisualTreeNotifications.ContentInvalidated -= OnInvalidated;
        }

        Assert.That(toldToRedraw, Is.True,
            "the text took the new colour but was never asked to redraw - it keeps painting the old one until " +
            "something unrelated dirties it, which is why this looked intermittent");
    }
}
