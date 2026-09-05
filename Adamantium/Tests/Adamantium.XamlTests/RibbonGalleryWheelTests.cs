using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The band shows a WINDOW onto a gallery's rows - two of however many the items make - and until now the arrows beside
/// it were the only thing that moved it. A wheel over a grid of choices is the first thing a person tries, and one that
/// does nothing is indistinguishable from a gallery that has no more rows in it.
/// <para>At the end of the run the wheel must go back UNHANDLED, the same chaining rule ScrollViewer follows: a gallery
/// that has stopped hands the event on so the strip around it still scrolls, instead of dead-ending under the
/// pointer.</para>
/// </summary>
[TestFixture]
public class RibbonGalleryWheelTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void Fresh()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
    }

    private static RibbonGallery WithRows(int items, int columns, int rows)
    {
        var gallery = new RibbonGallery { Columns = columns, CompactColumns = columns, Rows = rows };
        for (var i = 0; i < items; i++) gallery.Items.Add($"item {i}");
        return gallery;
    }

    private static bool Wheel(RibbonGallery gallery, int delta)
    {
        var args = new MouseWheelEventArgs(null, InputModifiers.None, delta, 0)
        {
            RoutedEvent = Mouse.MouseWheelEvent
        };
        gallery.RaiseEvent(args);
        return args.Handled;
    }

    [Test]
    public void AWheelNotchWalksTheRows()
    {
        var gallery = WithRows(30, 5, 2);

        Assert.That(gallery.CanScrollDown, Is.True, "six rows behind a two-row window: there is somewhere to go");

        var handled = Wheel(gallery, -120);

        TestContext.WriteLine($"firstRow={gallery.FirstRow} handled={handled} rowCount={gallery.RowCount}");
        Assert.That(gallery.FirstRow, Is.EqualTo(1), "one notch down moves the window by one row");
        Assert.That(handled, Is.True, "and the gallery took the event");
    }

    [Test]
    public void AWheelNotchWalksBackUp()
    {
        var gallery = WithRows(30, 5, 2);
        gallery.FirstRow = 3;

        Wheel(gallery, 120);

        Assert.That(gallery.FirstRow, Is.EqualTo(2));
    }

    [Test]
    public void AGalleryAtItsEndHandsTheWheelBack()
    {
        var gallery = WithRows(30, 5, 2);

        Assert.That(Wheel(gallery, 120), Is.False,
            "already at the top: the strip around it has to get the wheel instead of it dead-ending here");

        gallery.FirstRow = gallery.RowCount;
        Assert.That(Wheel(gallery, -120), Is.False, "and the same at the bottom");
    }

    [Test]
    public void AGalleryWithNothingToScrollNeverTakesTheWheel()
    {
        var gallery = WithRows(4, 5, 2);

        Assert.That(gallery.RowCount, Is.EqualTo(1));
        Assert.That(Wheel(gallery, -120), Is.False);
        Assert.That(Wheel(gallery, 120), Is.False);
    }

    [Test]
    public void ATiltWheelIsNotThisControlsAxis()
    {
        var gallery = WithRows(30, 5, 2);
        var args = new MouseWheelEventArgs(null, InputModifiers.None, -120, 0, true)
        {
            RoutedEvent = Mouse.MouseWheelEvent
        };

        gallery.RaiseEvent(args);

        Assert.That(gallery.FirstRow, Is.Zero);
        Assert.That(args.Handled, Is.False, "a horizontal wheel belongs to whatever scrolls sideways around it");
    }
}
