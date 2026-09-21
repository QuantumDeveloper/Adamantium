using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Correctness contract for the recycling-list rebind optimisation, held for BOTH the recycling case (a virtualized list
// rebinding a container to another same-template item) and the GENERAL ContentPresenter case (a Button/header whose
// content changes to a different-sized element). Both must stay green.
[TestFixture]
public class ContentPresenterRebindTests
{
    // Invariant #2 (rebind = data-only): rebinding to a DIFFERENT item of the SAME DataTemplate must NOT re-measure the
    // reused template subtree. Without it, every scrolled row re-measured its whole tile subtree (the Layout-tab cost).
    [Test]
    public void DataRebind_SameTemplate_DoesNotReMeasureSubtree()
    {
        var template = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Child = new Border() } });
        var cp = new ContentPresenter { ContentTemplate = template, Content = new object() };
        cp.Measure(new Size(24, 24));
        cp.Arrange(new Rect(0, 0, 24, 24));

        var m0 = MeasurableUIComponent.TotalMeasureCalls;
        // Exactly what ItemsControl.PrepareContainer does on a recycled rebind (same template):
        cp.DataContext = new object();
        cp.Content = new object();
        cp.Measure(new Size(24, 24));   // the panel re-measures the container at the (unchanged) cell

        var measures = MeasurableUIComponent.TotalMeasureCalls - m0;
        Assert.That(measures, Is.LessThanOrEqualTo(1),
            $"a same-template data rebind must not re-measure the reused subtree (got {measures} measure calls)");
    }

    // General correctness (NOT recycling): replacing content with a DIFFERENT-sized element must re-measure + resize the
    // presenter. The recycling optimisation must not break this.
    [Test]
    public void ContentReplace_DifferentSizedElement_ResizesPresenter()
    {
        var cp = new ContentPresenter { Content = new Border { Width = 10, Height = 10 } };
        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(10, 10)));

        cp.Content = new Border { Width = 50, Height = 40 };
        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(50, 40)),
            "replacing content with a bigger element must re-measure and resize the presenter");
    }

    // The visual is (re)built inside MeasureOverride, so a template swap that does not invalidate measure is never
    // picked up: the property holds the new template while the screen keeps the old visual forever.
    [Test]
    public void ContentTemplateSwap_RebuildsTheVisual()
    {
        var flat = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Width = 60, Height = 20 } });
        var turned = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Width = 20, Height = 60 } });

        var cp = new ContentPresenter { ContentTemplate = flat, Content = new object() };
        cp.Measure(Size.Infinity);
        cp.Arrange(new Rect(0, 0, 60, 20));
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(60, 20)));

        cp.ContentTemplate = turned;
        Assert.That(cp.IsMeasureValid, Is.False, "swapping ContentTemplate must invalidate measure");

        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(20, 60)),
            "the presenter must show the NEW template, not keep the visual built from the old one");
    }

    // The same swap through a SELECTOR, which is the other way a header/body template arrives.
    [Test]
    public void ContentTemplateSelectorSwap_RebuildsTheVisual()
    {
        var cp = new ContentPresenter { Content = new object() };
        cp.Measure(Size.Infinity);
        cp.Arrange(new Rect(0, 0, 10, 10));

        cp.ContentTemplateSelector = new FixedTemplateSelector(
            new DataTemplate(() => new TemplateResult { RootComponent = new Border { Width = 20, Height = 60 } }));
        Assert.That(cp.IsMeasureValid, Is.False, "swapping ContentTemplateSelector must invalidate measure");

        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(20, 60)));
    }

    // A template root that binds its Content to its own DataContext (`Content="{Binding}"` - the item itself, no path)
    // writes back the very object the presenter already sits on. Re-assigning DataContext to what it already holds runs
    // the whole DataContext cascade again - refresh bindings, write Content, assign DataContext... - and that closes a
    // cycle with no exit: the app died of a stack overflow the moment such a template was built.
    [Test]
    public void ContentBoundToItsOwnDataContext_DoesNotReassignDataContext()
    {
        var item = new object();
        var cp = new ContentPresenter { DataContext = item };

        var changes = 0;
        cp.DataContextChanged += (_, _) => changes++;
        cp.Content = item;   // exactly what a pathless {Binding} pushes back into Content

        Assert.That(changes, Is.Zero, "the presenter already sits on that object - re-assigning it re-enters the cascade");
    }

    // What a docking panel folded against a side edge is made of: the tab label's template is swapped for one whose root
    // is turned a quarter turn, so the strip must become as wide as the label is TALL.
    [Test]
    public void HeaderTemplateSwappedForATurnedOne_TurnsTheFootprint()
    {
        var flat = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Width = 80, Height = 24 } });
        var turned = new DataTemplate(() => new TemplateResult
        {
            RootComponent = new Border
            {
                Width = 80,
                Height = 24,
                LayoutTransform = new Transform { RotationAngle = -90 }
            }
        });

        var cp = new ContentPresenter { ContentTemplate = flat, Content = new object() };
        cp.Measure(Size.Infinity);
        cp.Arrange(new Rect(0, 0, 80, 24));

        cp.ContentTemplate = turned;
        cp.Measure(Size.Infinity);

        Assert.Multiple(() =>
        {
            Assert.That(cp.DesiredSize.Width, Is.EqualTo(24).Within(0.001), "turned label: the strip is as wide as the text is tall");
            Assert.That(cp.DesiredSize.Height, Is.EqualTo(80).Within(0.001));
        });
    }
}

internal class FixedTemplateSelector : DataTemplateSelector
{
    private readonly DataTemplate _template;

    public FixedTemplateSelector(DataTemplate template)
    {
        _template = template;
    }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
    {
        return _template;
    }
}

// The recycling fast-path that keeps a virtualized list from churning GPU buffers on every scroll frame: rebinding a
// ContentPresenter to new content of the SAME shape reuses the existing visual instead of tearing it down and rebuilding
// it (which exhausted device memory under fast scroll for plain-string items).
public class ContentPresenterTests
{
    [Test]
    public void StringRebind_ReusesAutoTextBlock()
    {
        var presenter = new ContentPresenter { Content = "a" };
        presenter.Measure(new Size(120, 30));
        var first = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();
        Assert.That(first, Is.Not.Null, "string content is hosted in an auto-generated TextBlock");

        presenter.Content = "b";                 // a recycled container rebinding to another string item
        presenter.Measure(new Size(120, 30), force: true);
        var second = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first), "the TextBlock is reused (not recreated -> no per-rebind GPU buffer churn)");
            Assert.That(second.Text, Is.EqualTo("b"), "and its text is updated to the new item");
        });
    }

    // DataContext adoption for DATA content rendered via a template - the crux that binds a data-bound TabControl body
    // (PART_SelectedContentHost) to its own selected item view-model, not the TabControl's DataContext. The context goes
    // on the BUILT VISUAL, not on the presenter: the presenter's own properties may themselves be bound.
    private sealed class ItemVm { public string Name { get; init; } public object Payload { get; init; } }

    private sealed class NameTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, AdamantiumComponent container) =>
            new(() =>
            {
                var tb = new TextBlock();
                var r = new TemplateResult { RootComponent = tb };
                r.AddBinding(tb, "Text", new Binding("Name"));   // resolves against the presenter's DataContext
                return r;
            });
    }

    [Test]
    public void DataContent_BecomesTheBuiltVisualsDataContext_SoTemplateBindsToIt()
    {
        var vm = new ItemVm { Name = "Zed" };
        var ambient = new ItemVm { Name = "Ambient" };
        var cp = new ContentPresenter
        {
            // Ambient context is deliberately something ELSE (mirrors PART_SelectedContentHost inheriting a TabControl's).
            DataContext = ambient,
            ContentTemplateSelector = new NameTemplateSelector(),
            Content = vm
        };
        cp.Measure(new Size(200, 100));
        cp.Arrange(new Rect(0, 0, 200, 100));

        Assert.Multiple(() =>
        {
            var tb = cp.VisualChildren.OfType<TextBlock>().First();
            Assert.That(tb.Text, Is.EqualTo("Zed"), "the built template's {Binding Name} resolves against the content view-model");
            Assert.That(tb.DataContext, Is.SameAs(vm), "the content is the context of the visual built for it");
            Assert.That(cp.DataContext, Is.SameAs(ambient),
                "the presenter keeps its own context - its Content/ContentTemplate may be bound against it");
        });
    }

    /// <summary>The presenter's own Content is BOUND (the ordinary case inside an item template), and the row is recycled
    /// onto another item. Stamping the content onto the presenter's own DataContext broke exactly this: the value the
    /// binding produced became the context the binding read from, and being a local write it masked inheritance for good,
    /// so a virtualized list repeated its first screenful forever.</summary>
    [Test]
    public void BoundContent_FollowsTheContainerWhenItIsRecycled()
    {
        var row = new ContentPresenter { ContentTemplateSelector = new NameTemplateSelector() };
        row.SetBinding(nameof(ContentPresenter.Content), new Binding("Payload"));
        var host = new Adamantium.UI.Controls.Panels.StackPanel
            { DataContext = new ItemVm { Payload = new ItemVm { Name = "first" } } };
        host.Children.Add(row);
        host.Measure(new Size(200, 100));
        host.Arrange(new Rect(0, 0, 200, 100));
        var before = row.VisualChildren.OfType<TextBlock>().First().Text;

        host.DataContext = new ItemVm { Payload = new ItemVm { Name = "second" } };   // the container is rebound
        host.Measure(new Size(200, 100), force: true);
        host.Arrange(new Rect(0, 0, 200, 100));

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo("first"));
            Assert.That(row.VisualChildren.OfType<TextBlock>().First().Text, Is.EqualTo("second"),
                "the rebound container's presenter re-resolves its Content and the kept visual follows the new item");
        });
    }

    // DIAGNOSTIC: mirrors exactly what the AUML CODE GENERATOR emits for a {Binding} inside a DataTemplate - it calls
    // element.SetBinding(...) in the builder, NOT result.AddBinding(...). If this fails while the AddBinding test above
    // passes, the generator must switch to result.AddBinding inside template builders (empty tab headers repro).
    [Test]
    public void ContentTemplate_SetBindingInBuilder_ResolvesAgainstDataContext()
    {
        var vm = new ItemVm { Name = "Zed" };
        var template = new DataTemplate(() =>
        {
            var tb = new TextBlock();
            var r = new TemplateResult { RootComponent = tb };
            tb.SetBinding("Text", new Binding("Name"));   // <-- the codegen path (element.SetBinding, not result.AddBinding)
            return r;
        });
        var cp = new ContentPresenter { DataContext = new ItemVm { Name = "Ambient" }, ContentTemplate = template, Content = vm };
        cp.Measure(new Size(200, 100));
        cp.Arrange(new Rect(0, 0, 200, 100));

        var tb = cp.VisualChildren.OfType<TextBlock>().First();
        Assert.That(tb.Text, Is.EqualTo("Zed"), "a {Binding} set via element.SetBinding inside a template must still resolve");
    }

    /// <summary>
    /// An ELEMENT content handed to another presenter belongs to that one. The presenter it left is told afterwards -
    /// its Content goes null - and it must not detach the element out of its new home on the way out.
    /// <para>Measured on docking: merging two floating windows moved a tab's body to the surviving window's presenter,
    /// the emptied one was notified a moment later and pulled the same element back out, and the tab was blank with its
    /// content parented nowhere - and it never came back.</para>
    /// </summary>
    [Test]
    public void ElementContentTakenByAnotherPresenter_IsNotDetachedByTheOldOne()
    {
        var body = new TextBlock { Text = "body" };
        var slot = new Size(100, 100);

        var first = new ContentPresenter { Content = body };
        first.Measure(slot);
        Assert.That(body.VisualParent, Is.SameAs(first), "the presenter it was given to hosts it");

        var second = new ContentPresenter { Content = body };
        second.Measure(slot);
        Assert.That(body.VisualParent, Is.SameAs(second), "handing it on moves it");

        // ...and only now does the first one hear that its content changed.
        first.Content = null;
        first.Measure(slot);

        Assert.That(body.VisualParent, Is.SameAs(second), "the presenter that no longer owns it must leave it alone");
    }

    /// <summary>
    /// TEMPLATED content moved to another presenter: the view model is shown by a template, so each presenter builds its
    /// OWN visual from it. The presenter that lost the content tears its copy down - and that teardown must not leave the
    /// new one empty.
    /// <para>Measured on docking: merging two floating windows blanked exactly the tabs whose body is a view model shown
    /// through the region's view locator, while tabs holding a plain element survived.</para>
    /// </summary>
    [Test]
    public void TemplatedContentMovedToAnotherPresenter_LeavesTheNewOneShowingIt()
    {
        var built = 0;
        var template = new DataTemplate(() =>
        {
            built++;
            return new TemplateResult { RootComponent = new Border { Child = new TextBlock { Text = "view" } } };
        });

        var vm = new ItemVm { Name = "page" };
        var slot = new Size(100, 100);

        var first = new ContentPresenter { ContentTemplate = template, Content = vm };
        first.Measure(slot);
        Assert.That(first.VisualChildren, Is.Not.Empty, "the first presenter shows it");

        // The same view model handed to another presenter - what a merge does.
        var second = new ContentPresenter { ContentTemplate = template, Content = vm };
        second.Measure(slot);

        // ...and only then is the first told its content is gone.
        first.Content = null;
        first.Measure(slot);
        second.Measure(slot);

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.EqualTo(2), "each presenter builds its own visual from the template");
            Assert.That(second.VisualChildren, Is.Not.Empty, "and the surviving one still shows its own");
        });
    }

    /// <summary>
    /// A presenter restyles only the text IT generated from a string. An AUTHORED TextBlock given as content keeps its
    /// own colour: writing into it would be an explicit value, and an explicit value outranks inheritance for good.
    /// <para>Measured on docking: merging two floating windows let the emptied presenter - holding the Transparent
    /// default by then - stamp that onto the tab's body, so the text stayed invisible in its new home no matter what
    /// the live presenter's colour was.</para>
    /// </summary>
    [Test]
    public void AnAuthoredTextContent_TakesItsColourFromWhicheverPresenterHoldsIt()
    {
        var authored = new TextBlock { Text = "body" };
        var slot = new Size(100, 100);

        // The presenter it starts in, holding the colour a torn-down one ends up with.
        var dim = new ContentPresenter { Foreground = Brushes.Transparent, Content = authored };
        dim.Measure(slot);

        // Handed to a live presenter with a real colour - what a merge does.
        var lit = new ContentPresenter { Foreground = Brushes.White, Content = authored };
        lit.Measure(slot);

        Assert.That(authored.Foreground, Is.SameAs(Brushes.White),
            "the colour must follow the presenter that holds it now - a value stamped in by the old one would be permanent");
    }

    [Test]
    public void UIElementContent_DoesNotOverrideDataContext()
    {
        var ambient = new ItemVm { Name = "Ambient" };
        var cp = new ContentPresenter { DataContext = ambient };

        cp.Content = new Border();   // a UI element brings its own bindings; must keep inheriting the ambient context

        Assert.That(cp.DataContext, Is.SameAs(ambient), "a UI-element content must not hijack the presenter's DataContext");
    }
}
