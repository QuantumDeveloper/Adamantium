using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Templates;

/// <summary>
/// A <see cref="DataTemplate"/> for TREE-shaped data. On top of rendering an item (the template body draws its HEADER) it
/// declares where that item's CHILDREN live via <see cref="ItemsSource"/>. An <see cref="ItemsControl"/> whose containers
/// are headered (MenuItem / TreeViewItem) uses it so each generated container gets its header drawn by this template AND
/// its own items bound through <see cref="ItemsSource"/>, re-applying the SAME template to those children - which is what
/// unrolls a tree of arbitrary depth from one template. See ItemsControl.PrepareContainer for the wiring.
/// </summary>
public class HierarchicalDataTemplate : DataTemplate
{
    public HierarchicalDataTemplate()
    {
    }

    public HierarchicalDataTemplate(Func<TemplateResult> templateBuilder) : base(templateBuilder)
    {
    }

    /// <summary>Binding that yields a node's child collection (e.g. <c>{Binding Children}</c>). Held as the binding itself,
    /// not a value: it is cloned into each generated container and re-resolves against that container's item.</summary>
    public BindingBase ItemsSource { get; set; }

    /// <summary>Optional style applied to each generated child container (e.g. to bind a MenuItem's Command / IsSeparator
    /// from the node). Propagates down every level so the whole tree is styled uniformly.</summary>
    public Style ItemContainerStyle { get; set; }

    /// <summary>Optional template for the CHILDREN. When null the children reuse THIS template (the recursive default).</summary>
    public DataTemplate ItemTemplate { get; set; }
}
