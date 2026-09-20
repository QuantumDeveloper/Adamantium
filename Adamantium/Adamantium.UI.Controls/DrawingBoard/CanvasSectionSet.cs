using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>The inspector lines that belong to ONE kind of thing - a rectangle's corners, a picture's source, a pen's
/// thickness.
/// <para>This is what makes the panel's contents CONTENT rather than part of a theme. The lines used to be written into
/// the inspector's control template, where a theme had to carry the list of everything a canvas can hold, three themes
/// carried three copies of it, and an application that put something of its own on the plane had no way to say what it
/// is set by. Here the list is a resource: the default set ships with the themes as a shared dictionary, and anything
/// else is added beside it.</para>
/// <para>WHAT IT IS FOR is said by name rather than by type, so a set can be written in markup that has no reference to
/// the class it describes - which is the usual case for an application's own objects. See <see cref="For"/>.</para>
/// </summary>
public class CanvasSectionSet : AdamantiumComponent
{
    /// <summary>What these lines are about - matched against the thing being inspected.
    /// <para>EMPTY means everything: where it stands, where it is, how big it is. Otherwise it is either the CLASS of
    /// the thing ("ShapeItem", "StrokeItem"), which is where a whole family's lines live, or its narrowest name -
    /// <see cref="ICanvasItem.Sort"/> - which is where one kind's own lines do: "Rectangle", "Image", "Bezier". For the
    /// tool page it is the tool's <see cref="ICanvasTool.Name"/>.</para>
    /// <para>Both are offered because they answer different questions: every shape has a fill, only a rectangle has
    /// corners. A thing gets the lines of every set that matches it, general first.</para></summary>
    public string For { get; set; }

    /// <summary>The sections themselves. [Content], so a set is written as the sections it is.</summary>
    [Content]
    public PropertySections Sections { get; } = new();
}
