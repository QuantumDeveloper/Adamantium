using System;
using System.Reflection;

namespace Adamantium.Core.DependencyInjection
{
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
}