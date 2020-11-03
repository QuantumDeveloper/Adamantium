using System;
using System.Collections.Generic;

namespace Adamantium.Core.DependencyInjection
{
    public class DependencyContainer : IDependencyContainer
    {
        private Dictionary<Type, DependencyInjectionDetails> mappings;

        public DependencyContainer()
        {
            mappings = new Dictionary<Type, DependencyInjectionDetails>();
        }

        public bool IsRegistered<T>()
        {
            return IsRegistered(typeof(T));
        }

        public bool IsRegistered(Type type)
        {
            return mappings.ContainsKey(type);
        }
        
        public void Register<TService, TImplementation>()
        {
            Register(typeof(TService), typeof(TImplementation));
        }

        public void Register(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Transient, service, implementation);
        }

        public void RegisterSingleton<T>()
        {
            RegisterSingleton(typeof(T), typeof(T));
        }

        public void RegisterSingleton<TService, TImplementation>()
        {
            RegisterSingleton(typeof(TService), typeof(TImplementation));
        }

        public void RegisterSingleton(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Singleton, service, implementation);
        }

        public void RegisterInstance<TService>(object instance, string name = "")
        {
            RegisterInstance(typeof(TService), instance, name);
        }

        public void RegisterInstance(Type source, object instance, string name = "")
        {
            Register(LifeTimeVariant.Singleton, source, instance.GetType(), instance, name);
        }

        public T Resolve<T>(string name = "")
        {
            return (T)Resolve(typeof(T), name);
        }

        public object Resolve(Type type, string name = "")
        {
            if (mappings.TryGetValue(type, out var info))
            {
                return info.GetDependency(this, type, name);
            }

            throw new ArgumentException($"{type.Name} does not registered");
        }
        

        private void Register(LifeTimeVariant lifeTime, Type service, Type implementation, object instance = null,
            string name = "")
        {
            if (mappings.TryGetValue(service, out var details))
            {
                details.AddDependency(implementation, instance, name);
            }
            else
            {
                details = new DependencyInjectionDetails(lifeTime);
                details.AddDependency(implementation, instance, name);
                mappings[service] = details;
            }
        }
    }
}