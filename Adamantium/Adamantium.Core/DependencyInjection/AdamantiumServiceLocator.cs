using System;
using System.Collections.Generic;
using Adamantium.Core.Events;

namespace Adamantium.Core.DependencyInjection
{
    public class AdamantiumServiceLocator : IDependencyResolver
    {
        private Dictionary<Type, DependencyInjectionDetails> mappings;
        
        public static AdamantiumServiceLocator Current { get; }

        static AdamantiumServiceLocator()
        {
            Current = new AdamantiumServiceLocator();
        }

        public AdamantiumServiceLocator()
        {
            mappings = new Dictionary<Type, DependencyInjectionDetails>();
            RegisterSingleton<IEventAggregator, EventAggregator>();
        }

        public bool IsRegistered<T>()
        {
            return IsRegistered(typeof(T));
        }

        public bool IsRegistered(Type type)
        {
            return mappings.ContainsKey(type);
        }
        
        public IDependencyResolver Register<TService, TImplementation>()
        {
            Register(typeof(TService), typeof(TImplementation));
            return this;
        }

        public IDependencyResolver Register(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Transient, service, implementation);
            return this;
        }

        public IDependencyResolver RegisterSingleton<T>()
        {
            RegisterSingleton(typeof(T), typeof(T));
            return this;
        }

        public IDependencyResolver RegisterSingleton<TService, TImplementation>()
        {
            RegisterSingleton(typeof(TService), typeof(TImplementation));
            return this;
        }

        public IDependencyResolver RegisterSingleton(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Singleton, service, implementation);
            return this;
        }

        public IDependencyResolver RegisterInstance<TService>(object instance, string name = "")
        {
            RegisterInstance(typeof(TService), instance, name);
            return this;
        }

        public IDependencyResolver RegisterInstance(Type source, object instance, string name = "")
        {
            Register(LifeTimeVariant.Singleton, source, instance.GetType(), instance, name);
            return this;
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