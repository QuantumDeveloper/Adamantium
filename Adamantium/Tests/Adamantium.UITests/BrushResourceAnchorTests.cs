using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A brush written in markup is NOT in the tree, so an expression on one of its properties has nothing to
/// resolve against. It has to be anchored to the element that draws with it - the route Transform takes through its
/// Owner. Without it a brush bound to the view model paints nothing and says nothing about why.</summary>
[TestFixture]
public class BrushResourceAnchorTests
{
    public class Palette
    {
        public Color Accent { get; set; }
    }

    [Test]
    public void ExpressionOnABrushProperty_ResolvesAgainstTheElementThatDrawsWithIt()
    {
        var brush = new SolidColorBrush();   // no ctor value: a local one would outrank the binding and mask it
        brush.SetBinding(SolidColorBrush.ColorProperty, new Binding("Accent"));

        var border = new Border { DataContext = new Palette { Accent = Colors.Red } };
        border.Background = brush;

        Assert.That(brush.Color, Is.EqualTo(Colors.Red));
    }

    /// <summary>A theme brush is shared by thousands of elements. The FIRST owner wins, so assigning it again does not
    /// re-establish its expressions - which on a theme load would be thousands of re-establishments for an answer that
    /// does not change.</summary>
    [Test]
    public void ASharedBrush_KeepsItsFirstAnchor()
    {
        var brush = new SolidColorBrush();
        brush.SetBinding(SolidColorBrush.ColorProperty, new Binding("Accent"));
        var first = new Border { DataContext = new Palette { Accent = Colors.Red } };
        var second = new Border();

        first.Background = brush;
        second.Background = brush;

        Assert.That(brush.InheritanceParent, Is.SameAs(first));
    }

    /// <summary>One brush on SEVERAL render properties of one element must subscribe that element ONCE - otherwise every
    /// change of a shared theme brush notifies it twice, multiplied by every element sharing it. And giving up one of
    /// the properties must not take the subscription with it: the other still draws with the brush.</summary>
    [Test]
    public void OneBrushOnTwoPropertiesOfOneElement_SubscribesItOnce()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var border = new Border();

        border.Background = brush;
        border.BorderBrush = brush;

        Assert.That(brush.SubscriberCount, Is.EqualTo(1), "subscribed twice for one element");
        Assert.That(brush.OwnerHoldCount(border), Is.EqualTo(2), "both properties counted");

        border.Background = new SolidColorBrush(Colors.Blue);

        Assert.That(brush.SubscriberCount, Is.EqualTo(1), "BorderBrush still draws with it");
        Assert.That(brush.OwnerHoldCount(border), Is.EqualTo(1));

        border.BorderBrush = new SolidColorBrush(Colors.Blue);

        Assert.That(brush.SubscriberCount, Is.EqualTo(0), "let go by both, still listening");
        Assert.That(brush.OwnerHoldCount(border), Is.EqualTo(0));
    }

    /// <summary>A RESOURCE reference on a brush property - the case this whole thing exists for. It is not a binding and
    /// is kept nowhere near them, which is exactly how a fix tested only with {Binding} could look green and still leave
    /// the brush empty. A resource declared on a VIEW is reachable only by walking the tree, and a brush has no place in
    /// it until an element takes it.</summary>
    [Test]
    public void AResourceReferenceOnABrushProperty_ResolvesOnceAnElementTakesTheBrush()
    {
        var brush = new SolidColorBrush();
        ResourceResolver.SetDeferred(brush, nameof(SolidColorBrush.Color), "TestFill");

        Assert.That(ResourceResolver.HasPending(brush), Is.True, "the ask was not remembered");

        new Border().Background = brush;

        // The anchor is the ONLY thing that can answer a tree-scoped resource for a brush, and a brush carrying only a
        // resource - no binding - must get one. Asking about bindings alone is exactly how this case stayed broken.
        Assert.That(brush.InheritanceParent, Is.Not.Null, "a brush waiting on a resource was left with no tree");
    }

    /// <summary>An anchor is NOT free: it subscribes the brush to the element's property changes and holds the element
    /// for as long as the brush lives. A theme brush shared by thousands of recycled rows would pin whichever one used
    /// it first. A brush with nothing to resolve must therefore never get one.</summary>
    [Test]
    public void APlainBrush_IsNeverAnchored()
    {
        var brush = new SolidColorBrush(Colors.Red);

        new Border().Background = brush;

        Assert.That(brush.InheritanceParent, Is.Null);
    }
}
