using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Maps a host-control type to its <see cref="IRegionAdapter"/>. Lookup walks up the type hierarchy, so
/// registering an adapter for <c>Selector</c> covers <c>TabControl</c> (which derives from it). The extensibility point
/// for supporting a new host control: <c>Register&lt;DockingControl&gt;(new DockingRegionAdapter(...))</c>.</summary>
public sealed class RegionAdapterMappings
{
    private readonly Dictionary<Type, IRegionAdapter> _map = new();

    public void Register(Type controlType, IRegionAdapter adapter) => _map[controlType] = adapter;
    public void Register<TControl>(IRegionAdapter adapter) => _map[typeof(TControl)] = adapter;

    public IRegionAdapter GetAdapter(Type controlType)
    {
        for (var type = controlType; type != null; type = type.BaseType)
            if (_map.TryGetValue(type, out var adapter))
                return adapter;
        return null;
    }
}
