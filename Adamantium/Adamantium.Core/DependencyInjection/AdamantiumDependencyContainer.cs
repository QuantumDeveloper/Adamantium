using System;
using System.Collections.Generic;
using Adamantium.Core.Events;

namespace Adamantium.Core.DependencyInjection
{
    public class AdamantiumDependencyContainer : IDependencyContainer
    {
        private Dictionary<Type, DependencyInjectionDetails> _registrations;
        
        public static AdamantiumDependencyContainer Current { get; }

        static AdamantiumDependencyContainer()
        {
            Current = new AdamantiumDependencyContainer();
        }

        public AdamantiumDependencyContainer()
        {
            _registrations = new Dictionary<Type, DependencyInjectionDetails>();
            ((IContainerRegistry)this).RegisterSingleton<IEventAggregator, EventAggregator>();
        }

        public bool IsRegistered<T>()
        {
            return IsRegistered(typeof(T));
        }

        public bool IsRegistered(Type type)
        {
            return _registrations.ContainsKey(type);
        }

        IContainerRegistry IContainerRegistry.Register<TService, TImplementation>()
        {
            ((IContainerRegistry)this).Register(typeof(TService), typeof(TImplementation));
            return this;
        }

        IContainerRegistry IContainerRegistry.Register(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Transient, service, implementation);
            return this;
        }

        IContainerRegistry IContainerRegistry.RegisterSingleton<T>()
        {
            ((IContainerRegistry)this).RegisterSingleton(typeof(T), typeof(T));
            return this;
        }

        IContainerRegistry IContainerRegistry.RegisterSingleton<TService, TImplementation>()
        {
            ((IContainerRegistry)this).RegisterSingleton(typeof(TService), typeof(TImplementation));
            return this;
        }

        public IContainerRegistry RegisterSingleton<TService>(object implementation)
        {
            Register(LifeTimeVariant.Singleton, typeof(TService), implementation.GetType(), implementation);
            return this;
        }

        IContainerRegistry IContainerRegistry.RegisterSingleton(Type service, Type implementation)
        {
            Register(LifeTimeVariant.Singleton, service, implementation);
            return this;
        }

        IContainerRegistry IContainerRegistry.RegisterInstance<TService>(object instance, string name = "")
        {
            ((IContainerRegistry)this).RegisterInstance(typeof(TService), instance, name);
            return this;
        }

        IContainerRegistry IContainerRegistry.RegisterInstance(Type source, object instance, string name = "")
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
            if (_registrations.TryGetValue(type, out var info))
            {
                return info.GetDependency(this, type, name);
            }

            throw new ArgumentException($"{type.Name} does not registered");
        }
        

        private void Register(LifeTimeVariant lifeTime, Type service, Type implementation, object instance = null,
            string name = "")
        {
            if (_registrations.TryGetValue(service, out var details))
            {
                details.AddDependency(implementation, instance, name);
            }
            else
            {
                details = new DependencyInjectionDetails(lifeTime);
                details.AddDependency(implementation, instance, name);
                _registrations[service] = details;
            }
        }
    }
}