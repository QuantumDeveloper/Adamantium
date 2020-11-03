using System;

namespace Adamantium.Core.DependencyInjection
{
    public interface IDependencyContainer
    {
        public bool IsRegistered<T>();

        public bool IsRegistered(Type type);
        
        public void Register<TService, TImplementation>();

        public void Register(Type from, Type to);

        public void RegisterSingleton<T>();

        public void RegisterSingleton<TService, TImplementation>();

        public void RegisterSingleton(Type service, Type implementation);

        public void RegisterInstance<TService>(object instance, string name = "");

        public void RegisterInstance(Type service, object implementation, string name = "");

        public T Resolve<T>(string name = "");

        public object Resolve(Type type, string name = "");

    }
}