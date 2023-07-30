using System;

namespace Adamantium.Core.DependencyInjection;

public interface IContainerRegistry
{
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
}