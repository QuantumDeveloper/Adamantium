using System;
using System.Collections.Generic;
using System.Reflection;

namespace Adamantium.Core.DependencyInjection;

public interface IContainerRegistry
{
    /// <summary>See <see cref="AdamantiumDependencyContainer.StrictResolution"/>.</summary>
    bool StrictResolution { get; set; }

    public bool IsRegistered<T>();

    public bool IsRegistered(Type type);
        
    public IContainerRegistry Register<TService, TImplementation>();

    public IContainerRegistry Register(Type from, Type to);

    public IContainerRegistry RegisterSingleton<T>();

    public IContainerRegistry RegisterSingleton<TService, TImplementation>();
    
    public IContainerRegistry RegisterSingleton<TService>(object implementation);

    public IContainerRegistry RegisterSingleton(Type service, Type implementation);

    public IContainerRegistry RegisterInstance<TService>(object instance, string name = "");

    public IContainerRegistry RegisterInstance(Type service, object implementation, string name = "");

    /// <summary>
    /// Scans the given assemblies and registers every class marked with <see cref="ServiceAttribute"/> (with its
    /// declared lifetime) plus every concrete class assignable to one of <paramref name="conventionBaseTypes"/>
    /// (e.g. a view-model base) as Transient. Each implementation is also aliased to its user-defined interfaces so it
    /// resolves either by concrete type or interface.
    /// </summary>
    IContainerRegistry AutoRegister(IEnumerable<Assembly> assemblies, params Type[] conventionBaseTypes);
}