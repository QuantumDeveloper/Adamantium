namespace Adamantium.Core.DependencyInjection;

/// <summary>Lifetime of a service registered via <see cref="ServiceAttribute"/> or auto-registration.</summary>
public enum ServiceLifetime
{
    /// <summary>A new instance on every resolve (the default).</summary>
    Transient,

    /// <summary>A single shared instance for the container's lifetime.</summary>
    Singleton
}
