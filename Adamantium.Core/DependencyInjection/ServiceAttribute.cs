using System;

namespace Adamantium.Core.DependencyInjection;

/// <summary>Marks a class for <see cref="IContainerRegistry.AutoRegister"/> with its <see cref="Lifetime"/>; the type is
/// registered against itself and its non-system interfaces.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ServiceAttribute : Attribute
{
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        Lifetime = lifetime;
    }

    public ServiceLifetime Lifetime { get; }
}
