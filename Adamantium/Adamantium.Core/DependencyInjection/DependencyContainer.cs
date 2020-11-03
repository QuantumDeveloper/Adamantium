using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

    internal class DependencyInjectionDetails
    {
        public DependencyInjectionDetails(LifeTimeVariant lifeTimeVariant)
        {
            LifeTimeVariant = lifeTimeVariant;
            DependencyItems = new List<DependencyItem>();
        }
        
        public LifeTimeVariant LifeTimeVariant { get; set; }
        
        public List<DependencyItem> DependencyItems { get; set; }

        public bool AlreadyExists(Type implementation, object instance, string name = "")
        {
            var contains = DependencyItems
                .FirstOrDefault(x => x.ImplementationType == implementation && x.Instance == instance && x.RegistryName == name);

            return contains != null;
        }

        public void AddDependency(Type implementation, object instance, string name = "")
        {
            if (AlreadyExists(implementation, instance, name)) throw new ArgumentException($"{implementation.Name} already registered");

            var item = new DependencyItem(implementation, instance, name);
            //If there are no instance, we should find constructor, which we will use to create an instance
            if (instance == null)
            {
                var constructors = implementation.GetConstructors().ToList();

                if (constructors.Count > 1)
                {
                    var ctor = constructors.FirstOrDefault(x => x.GetCustomAttribute<DependencyConstructorAttribute>() != null);
                    if (ctor != null)
                    {
                        item.Ctor = ctor;
                    }
                    else
                    {
                        throw new ArgumentException($"you should mark corresponding constructor for {implementation.Name}");                        
                    }
                }
            }
            DependencyItems.Add(item);
        }

        public object GetDependency(DependencyContainer container, Type type, string name = "")
        {
            var service = type;
            DependencyItem item = DependencyItems.FirstOrDefault(x => x.RegistryName == name);
            
            if (item == null) throw new ArgumentException($"Cannot resolve {service.Name} because its not registered");

            if (LifeTimeVariant == LifeTimeVariant.Transient || LifeTimeVariant == LifeTimeVariant.Singleton)
            {
                var parameters = new List<object>();
                if (item.Ctor != null)
                {
                    foreach (var parameter in item.Ctor.GetParameters())
                    {
                        if (!container.IsRegistered(parameter.ParameterType))
                        {
                            throw new ArgumentException(
                                $"{parameter.ParameterType.Name} is not registered in {item.ImplementationType.Name}");
                        }

                        parameters.Add(container.Resolve(parameter.ParameterType));
                    }
                }

                object instance = null;
                if (LifeTimeVariant == LifeTimeVariant.Transient || (LifeTimeVariant == LifeTimeVariant.Singleton && item.Instance == null))
                {
                    instance = Activator.CreateInstance(item.ImplementationType, parameters.ToArray());
                    if (LifeTimeVariant == LifeTimeVariant.Singleton)
                    {
                        item.Instance = instance;
                    }
                }
                else if (LifeTimeVariant == LifeTimeVariant.Singleton && item.Instance != null)
                {
                    instance = item.Instance;
                }

                return instance;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
        
    }
    

    internal class DependencyItem
    {
        public DependencyItem(Type implementation, object instance, string name = "")
        {
            ImplementationType = implementation;
            Instance = instance;
            RegistryName = name;
        }
        
        public Type ImplementationType { get; set; }
        
        public ConstructorInfo Ctor { get; set; }

        public object Instance { get; set; }
        
        public string RegistryName { get; set; }
    }

    internal enum LifeTimeVariant
    {
        Transient,
        Scoped,
        Singleton
    }
    
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

    [AttributeUsage(AttributeTargets.Constructor)]
    public class DependencyConstructorAttribute : Attribute
    {
    }
}