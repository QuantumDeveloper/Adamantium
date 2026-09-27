using System;

namespace Adamantium.Core.DependencyInjection;

/// <summary>
/// Marks a class for automatic registration by <see cref="IContainerRegistry.AutoRegister"/>. It is the single source
/// of truth for a type's lifetime: the type is registered with <see cref="Lifetime"/> (default
/// <see cref="ServiceLifetime.Transient"/>), so an intended singleton can't silently degrade to a transient as long
/// as it carries this attribute. The concrete type is registered against itself and aliased to its (non-system)
/// interfaces, so it resolves either way.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ServiceAttribute : Attribute
{
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        Lifetime = lifetime;
    }

    public ServiceLifetime Lifetime { get; }
}
