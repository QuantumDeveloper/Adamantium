using System;

namespace Adamantium.Core.DependencyInjection
{
    public interface IDependencyResolver
    {
        public bool IsRegistered<T>();

        public bool IsRegistered(Type type);
        
        public IDependencyResolver Register<TService, TImplementation>();

        public IDependencyResolver Register(Type from, Type to);

        public IDependencyResolver RegisterSingleton<T>();

        public IDependencyResolver RegisterSingleton<TService, TImplementation>();

        public IDependencyResolver RegisterSingleton(Type service, Type implementation);

        public IDependencyResolver RegisterInstance<TService>(object instance, string name = "");

        public IDependencyResolver RegisterInstance(Type service, object implementation, string name = "");

        public T Resolve<T>(string name = "");

        public object Resolve(Type type, string name = "");

    }
}