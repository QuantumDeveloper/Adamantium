using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Adamantium.Core.DependencyInjection
{
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
                else
                {
                    item.Ctor = constructors.FirstOrDefault();
                }
            }
            DependencyItems.Add(item);
        }

        public object GetDependency(AdamantiumServiceLocator container, Type type, string name = "")
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
}