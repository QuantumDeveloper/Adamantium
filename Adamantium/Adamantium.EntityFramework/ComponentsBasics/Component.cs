using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.Components;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public abstract class Component : DisposableObject, IComponent, IControllableComponent, IInitializable, IEntityOwner, ICloneableComponent
    {
        protected Component()
        {
            uid = UidGenerator.Generate();
        }

        private Entity owner;
        private readonly long uid;
        private bool initialized;

        public Entity Owner
        {
            get => owner;
            set
            {
                if (owner != value)
                {
                    var old = owner;
                    SetProperty(ref owner, value);
                    OnOwnerChanged(old, owner);
                }
            }
        }

        protected virtual void OnOwnerChanged(Entity oldOwner, Entity newOwner)
        {
            OwnerChanged?.Invoke(this, new OwnerChangedEventArgs(oldOwner, newOwner));
        }

        public event EventHandler<OwnerChangedEventArgs> OwnerChanged;


        public virtual void Initialize()
        {
            initialized = true;
        }

        public long Uid => uid;

        protected void Traverse(Action<Entity> action)
        {
            var stack = new Stack<Entity>();
            stack.Push(Owner);
            while (stack.Count > 0)
            {
                Entity current = stack.Pop();
                if (current.IsEnabled)
                {
                    action(current);

                    for (int i = 0; i < current.Dependencies.Count; i++)
                    {
                        stack.Push(current.Dependencies[i]);
                    }
                }
            }
        }

        public T GetOrCreateComponent<T>() where T : class, IComponent, new()
        {
            return owner?.GetOrCreateComponent<T>();
        }

        public void AddComponent(IComponent component)
        {
            owner?.AddComponent(component);
        }

        public void RemoveComponent<T>() where T : class, IComponent
        {
            owner?.RemoveComponent<T>();
        }

        public T GetComponent<T>() where T : class, IComponent
        {
            return owner?.GetComponent<T>();
        }

        public T GetComponentInParents<T>() where T : class, IComponent
        {
            return owner?.GetComponentInParents<T>();
        }

        public T GetComponentInChildren<T>() where T : class, IComponent
        {
            return owner?.GetComponentInChildren<T>();
        }

        public T[] GetComponents<T>() where T : class, IComponent
        {
            return owner?.GetComponents<T>();
        }

        public T[] GetComponentsInParents<T>() where T : class, IComponent
        {
            return owner?.GetComponentsInParents<T>();
        }

        public T[] GetComponentsInChildren<T>() where T : class, IComponent
        {
            return owner?.GetComponentsInChildren<T>();
        }

        public bool ContainsComponent<T>() where T : class, IComponent
        {
            if (owner == null)
            {
                return false;
            }
            return owner.ContainsComponent<T>();
        }

        public bool Initialized => initialized;


        public virtual IComponent Clone()
        {
            return (IComponent)Activator.CreateInstance(GetType());
        }

        public virtual void CloneValues(IComponent clone)
        {
            var originalProperties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var clonedProperties = clone.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(x => x.Name);
            foreach (var propertyInfo in originalProperties)
            {
                if (!propertyInfo.CanWrite)
                {
                    continue;
                }

                var attr = propertyInfo.GetCustomAttribute<DoNotCloneAttribute>();
                if (attr != null)
                {
                    continue;
                }

                var property = clonedProperties[propertyInfo.Name];
                var value = propertyInfo.GetValue(this);
                property.SetValue(clone, value);
            }
        }
    }
}
