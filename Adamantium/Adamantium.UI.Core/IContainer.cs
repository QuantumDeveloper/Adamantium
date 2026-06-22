using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core;

public interface IContainer
{
    void AddOrSetChildComponent(object component);

    void RemoveAllChildComponents();

    // --- incremental child editing (designer hot reload) ----------------------------------------------------------
    // Lets the live previewer reconcile a container's authored children one-by-one (add/remove/replace/reorder) when
    // markup is edited, instead of rebuilding the whole tree. Default fallbacks keep any container that doesn't
    // override these loadable - the previewer just replaces such a container's subtree wholesale instead.

    /// <summary>The authored child components, in order. A panel returns its children; a single-child host returns its
    /// one child (or nothing).</summary>
    IReadOnlyList<object> GetChildComponents() => Array.Empty<object>();

    /// <summary>Inserts a child at <paramref name="index"/>. A single-child host treats index 0 as its one slot.</summary>
    void InsertChildComponent(int index, object component) => AddOrSetChildComponent(component);

    /// <summary>Removes the child at <paramref name="index"/>.</summary>
    void RemoveChildComponentAt(int index) { }
}
