using Adamantium.Game.Sandbox.DrawingBoard.ViewModels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>WHICH TEMPLATE draws what a node carries, by the type of the thing it carries.
/// <para>The templates themselves are written in the view, where visuals belong; this only says which one goes with
/// which state. Without it the table of kinds would have to build controls, and a view-model that builds controls is
/// a view-model drawing - and then the value a person set would have to be dug back out of the visual tree to save
/// it.</para>
/// <para>A selector and not one template per node, because the CONTROL is one - a node - and what is in it differs by
/// kind. The engine has no way to pick a template from a data type on its own yet; when it grows one this whole class
/// goes away and the templates stand on their own.</para></summary>
public sealed class NodeBodySelector : DataTemplateSelector
{
    /// <summary>A single number a person sets - an amount, a brightness.</summary>
    public DataTemplate Number { get; set; }

    /// <summary>A color a person picked.</summary>
    public DataTemplate Color { get; set; }

    /// <summary>What reached the end of the graph, shown as itself.</summary>
    public DataTemplate Result { get; set; }

    /// <summary>NOTHING TO SHOW - a mix, a merge: a node that is all sockets. An empty template and not a missing one,
    /// because a presenter given content it has no template for falls back to the object's text, and the text of a
    /// state object is its type name - which is what made those nodes stretch to the width of a namespace.</summary>
    public DataTemplate None { get; set; }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container) => item switch
    {
        NumberSpecialization => Number,
        ColorSpecialization => Color,
        OutputSpecialization => Result,
        NodeSpecialization => None,
        _ => null
    };
}
