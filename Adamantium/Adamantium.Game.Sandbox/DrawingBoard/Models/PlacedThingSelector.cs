using Adamantium.Game.Sandbox.DrawingBoard.ViewModels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>WHICH TEMPLATE draws a thing the page put on the plane, by the type of that thing - the drawing side's
/// answer to <see cref="NodeBodySelector"/>.
/// <para>The page says WHAT is there and the canvas builds the control, so no view-model here ever holds one. A shape
/// needs nothing of this: the canvas knows how to draw a shape from its description, and only what is NOT a shape
/// comes past here.</para></summary>
public sealed class PlacedThingSelector : DataTemplateSelector
{
    /// <summary>Something to press.</summary>
    public DataTemplate Button { get; set; }

    /// <summary>Something to tick, which holds a state of its own.</summary>
    public DataTemplate Switch { get; set; }

    /// <summary>Something to type in.</summary>
    public DataTemplate Field { get; set; }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container) => item switch
    {
        SampleButton => Button,
        SampleSwitch => Switch,
        SampleField => Field,
        _ => null
    };
}
