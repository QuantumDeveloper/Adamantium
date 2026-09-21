using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A PATH THAT ENDS INSIDE A STRUCT: <c>Offset.X</c>, <c>Bounds.Width</c>. The walk reaches a copy, so
/// without the chain being re-walked a row shows the number the path was resolved at forever, and an edit lands in the
/// copy and is thrown away - both without a word.</summary>
[TestFixture]
public class StructPathBindingTests
{
    private sealed class Camera : AdamantiumComponent
    {
        public static readonly AdamantiumProperty WhereProperty = AdamantiumProperty.Register(nameof(Where),
            typeof(Vector2), typeof(Camera), new PropertyMetadata(Vector2.Zero));

        public Vector2 Where
        {
            get => GetValue<Vector2>(WhereProperty);
            set => SetValue(WhereProperty, value);
        }
    }

    private static Border Bound(object source, string path, BindingMode mode = BindingMode.TwoWay)
    {
        var border = new Border { DataContext = source };

        BindingExpression.CreateBindingExpression(border, MeasurableUIComponent.WidthProperty,
            new Binding(path) { Mode = mode });

        return border;
    }

    // A FIELD, which is what the maths types are made of - a path that cannot see one cannot see a position.
    [Test]
    public void AFieldInsideAStructIsRead()
    {
        var camera = new Camera { Where = new Vector2(120, 40) };

        Assert.That(Bound(camera, "Where.X").Width, Is.EqualTo(120));
    }

    [Test]
    public void MovingTheWholeStructMovesTheRow()
    {
        var camera = new Camera { Where = new Vector2(120, 40) };
        var border = Bound(camera, "Where.X");

        camera.Where = new Vector2(360, 40);
        BindingUpdateQueue.Flush();

        Assert.That(border.Width, Is.EqualTo(360), "the row is reading a copy taken when it was bound");
    }

    [Test]
    public void WritingOneNumberWritesItIntoTheWholeStruct()
    {
        var camera = new Camera { Where = new Vector2(120, 40) };
        var border = Bound(camera, "Where.X");

        border.Width = 500;
        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(camera.Where.X, Is.EqualTo(500), "the edit landed in a copy");
            Assert.That(camera.Where.Y, Is.EqualTo(40), "the rest of the value was lost on the way back");
        });
    }

    // A PROPERTY inside a struct, and one written through a plain object rather than a component.
    private sealed class Item
    {
        public Rect Box { get; set; } = new(10, 20, 30, 40);
    }

    [Test]
    public void APropertyInsideAStructIsReadAndWritten()
    {
        var item = new Item();
        var border = Bound(item, "Box.Width");

        Assert.That(border.Width, Is.EqualTo(30));

        border.Width = 90;
        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(item.Box.Width, Is.EqualTo(90));
            Assert.That(item.Box.X, Is.EqualTo(10), "the rest of the box was lost on the way back");
        });
    }

    [Test]
    public void APathThroughAStructToNothingSaysNothing()
    {
        var camera = new Camera { Where = new Vector2(120, 40) };
        var border = Bound(camera, "Where.Nowhere");

        Assert.That(border.Width, Is.EqualTo(new Border().Width), "a path to nothing wrote something anyway");
    }
}
