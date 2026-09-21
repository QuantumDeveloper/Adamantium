namespace Adamantium.UI.Core;

/// <summary>A control that has applied a template and can hand back the elements named inside it.
/// <para>Exists so that things living in Core - the binding system, above all - can ask about a TEMPLATE'S names
/// without referencing the control library. A template is its own namescope: the names in it belong to the control
/// that applied it, not to the window, which is why they cannot be found by walking the visual tree looking at
/// <see cref="IFundamentalUIComponent.Name"/>.</para></summary>
public interface ITemplateHost
{
    /// <summary>The element named <paramref name="name"/> inside this control's applied template, or null.</summary>
    IAdamantiumComponent GetTemplateChild(string name);
}
