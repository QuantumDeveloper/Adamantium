using System;

namespace Adamantium.UI.Core.Resources;

public interface IResourceManager
{
    // Raised (coalesced, once per layout pass via FlushResourceChanges) when the resource set changed - a theme swap, or
    // a dictionary loaded/unloaded. A live {ObservableResource} subscribes to re-resolve its key.
    event EventHandler ResourcesChanged;

    // Fire a pending ResourcesChanged if any AddSource/RemoveSources happened since the last flush. Called once at the
    // end of a layout pass, so a theme swap's remove-old + add-new collapse into ONE notification, AFTER the new theme's
    // dictionaries are loaded (no transient miss), and startup's many AddSource calls don't storm.
    void FlushResourceChanges();

    T FindResourceInScope<T>(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResourceInScope(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResource(string name);

    // Requester-aware: Local resources are visible only within the subtree of the element that declared them; walks up
    // from the requester. Context-less FindResource cannot see Local at all.
    object FindResource(IFundamentalUIComponent requester, string name);

    void AddSource(IAdamantiumComponent component, Type source, ResourceScope scope = ResourceScope.Local);

    // Register an inline (per-element) dictionary INSTANCE - ResourceContext.Resources authored on an element.
    void AddSource(IAdamantiumComponent component, ResourceDictionary instance, ResourceScope scope = ResourceScope.Local);

    // A resource VALUE changed in place (an inline dictionary was mutated): re-resolve live {ObservableResource}s.
    void NotifyResourcesChanged();

    void RemoveSources(IAdamantiumComponent component);
    
    Object this[string name] { get; }
}