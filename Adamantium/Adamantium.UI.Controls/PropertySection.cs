using System.Collections.Specialized;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>A group of properties under one folding header - one ECS component, one category of a class.
/// <para>It IS an <see cref="Expander"/>, not something that looks like one: a section wants a real header with its own
/// chrome, state and context menu, and a header ROW inside a flat list would only be impersonating that.</para></summary>
public class PropertySection : Expander
{
    public static readonly AdamantiumProperty CategoryProperty = AdamantiumProperty.Register(nameof(Category),
        typeof(String), typeof(PropertySection), new PropertyMetadata(null));

    public PropertySection()
    {
        // The properties JOIN the section's logical tree - that is what gives a definition a DataContext, and with it
        // {Binding}, {Ancestor} and every resource lookup on its own properties. Without this step a definition is an
        // object floating beside the tree, and everything written on it in markup would resolve against nothing.
        Properties.CollectionChanged += OnPropertiesChanged;
    }

    /// <summary>The properties in this section, in display order. [Content] - and it deliberately shadows the
    /// <see cref="Expander"/>'s own content property: what one writes inside a section is its properties, while the
    /// content an expander shows is the rows the grid builds from them.</summary>
    [Content]
    public PropertyDefinitions Properties { get; } = new();

    /// <summary>The object these properties read and write. Set by the grid from its own
    /// <see cref="PropertyGrid.SelectedObject"/> unless the section names its own - a section CAN inspect something
    /// else (a component of the selection).</summary>
    public object Target { get; set; }

    /// <summary>What the section is called in the model - the component's type name, the [Category] attribute. Free
    /// text; the grid does not interpret it.</summary>
    public String Category
    {
        get => GetValue<String>(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    private void OnPropertiesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var gone in e.OldItems?.OfType<PropertyDefinition>() ?? Enumerable.Empty<PropertyDefinition>())
            RemoveLogicalChild(gone);

        foreach (var added in e.NewItems?.OfType<PropertyDefinition>() ?? Enumerable.Empty<PropertyDefinition>())
            AddLogicalChild(added);
    }
}
