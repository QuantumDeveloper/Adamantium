using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.ComponentsBasics;
using IComponent = Adamantium.EntityFramework.ComponentsBasics.IComponent;

namespace Adamantium.EntityFramework
{
    /// <summary>
    /// Class containing information about entity and its components
    /// </summary>
    public sealed class Entity :
       PropertyChangedBase,
       IIdentifiable,
       IName,
       IEnable,
       IEntityOwner,
       IControllableComponent,
       IDisposable,
       IEntitySearch
    {
        public Entity(Entity owner = null, String name = "")
        {
            dependencies = new TrackingCollection<Entity>();
            dependenciesReadOnly = new ReadOnlyTrackingCollection<Entity>(dependencies);
            uid = UidGenerator.Generate();
            Owner = owner;
            IsEnabled = true;
            Visible = true;
            Name = name;
            Transform = new Transform();
            componentCollection = new EntityComponentCollection(this);
            componentCollection.Add(Transform);
            readOnlyComponents = new ReadOnlyTrackingCollection<IComponent>(componentCollection);
        }

        private Entity owner;
        private readonly Int64 uid;
        private bool isEnabled;
        private bool isSelected;
        private string name;
        private readonly TrackingCollection<Entity> dependencies;
        private readonly ReadOnlyTrackingCollection<Entity> dependenciesReadOnly;
        private EntityComponentCollection componentCollection;
        private ReadOnlyTrackingCollection<IComponent> readOnlyComponents;
        private List<IInitializable> pendingComponents = new List<IInitializable>();

        public Transform Transform { get; internal set; }

        public long Uid => uid;

        public bool IgnoreInCollisionDetection { get; set; }

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        public bool Visible { get; set; }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (SetProperty(ref isEnabled, value))
                {
                    EnabledChanged?.Invoke(this, new StateEventArgs(value));
                }
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        public Entity Owner
        {
            get => owner;
            set
            {
                if (owner != value)
                {
                    OnOwnerChanged(owner, value);
                    owner = value;
                }
            }
        }

        private void OnOwnerChanged(Entity oldOwner, Entity newOwner)
        {
            //Remove this entity from dependeciew of old parent entity
            oldOwner?.RemoveDependency(this);
            //Add current entity as dependency for its parent
            newOwner?.AddDependency(this);

            OwnerChanged?.Invoke(this, new OwnerChangedEventArgs(oldOwner, newOwner));
        }

        public event EventHandler<StateEventArgs> EnabledChanged;

        public event EventHandler<OwnerChangedEventArgs> OwnerChanged;

        public ReadOnlyTrackingCollection<Entity> Dependencies => dependenciesReadOnly;

        public EntityComponentCollection Components => componentCollection;

        public Entity GetRoot()
        {
            if (Owner == null)
            {
                return this;
            }

            Entity root = Owner;
            bool rootFound = false;
            while (!rootFound)
            {
                if (root.Owner != null)
                {
                    root = root.Owner;
                }
                else
                {
                    rootFound = true;
                }
            }

            return root;
        }

        public Entity Get(string name)
        {
            Queue<Entity> queue = new Queue<Entity>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return current;
                }

                foreach (var entity in current.Dependencies)
                {
                    queue.Enqueue(entity);
                }
            }
            return null;
        }

        public T GetOrCreateComponent<T>() where T : class, IComponent, new()
        {
            T component = componentCollection.Get<T>();
            if (component == null)
            {
                component = new T();
                componentCollection.Add(component);
            }

            return component;
        }

        public void AddComponent(IComponent component)
        {
            lock (componentCollection)
            {
                if (component is IEntityOwner owned)
                {
                    owned.Owner = this;
                }

                if (ContainsComponent(component.GetType()) || componentCollection.Contains(component))
                    return;

                if (component is IInitializable initializable)
                {
                    pendingComponents.Add(initializable);
                }
                else
                {
                    componentCollection.Add(component);
                }

                var customAttribute = component.GetType().GetCustomAttributes(typeof(RequiredComponetsAttribute), true);
                foreach (RequiredComponetsAttribute attribute in customAttribute)
                {
                    var types = attribute.Components;
                    foreach (var type in types)
                    {
                        if (!ContainsComponent(type))
                        {
                            var required = (IComponent)Activator.CreateInstance(type);
                            if (required is IEntityOwner entityOwner)
                            {
                                entityOwner.Owner = this;
                            }

                            if (required is IInitializable init)
                            {
                                pendingComponents.Add(init);
                            }
                        }
                    }
                }

                foreach (var pendingComponent in pendingComponents)
                {
                    pendingComponent.Initialize();
                    if (pendingComponent is IComponent pending)
                    {
                        componentCollection.Add(pending);
                    }
                }
                pendingComponents.Clear();
            }
        }

        public void RemoveComponent<T>() where T : class, IComponent
        {
            componentCollection.Remove<T>();
        }

        public void RemoveComponent(IComponent component)
        {
            componentCollection.Remove(component);
        }

        public T GetComponent<T>() where T : class, IComponent
        {
            try
            {
                return componentCollection.Get<T>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public T[] GetComponents<T>() where T : class, IComponent
        {
            try
            {
                return componentCollection.GetAll<T>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                //throw;
            }
            return new T[0];
        }

        public bool ContainsComponent<T>() where T : class, IComponent
        {
            return componentCollection.Contains<T>();
        }

        public bool ContainsComponent(Type type)
        {
            return componentCollection.Contains(type);
        }

        public T GetComponentInParents<T>() where T : class, IComponent
        {
            return componentCollection.GetInParents<T>();
        }

        public T GetComponentInChildren<T>() where T : class, IComponent
        {
            return componentCollection.GetInChildren<T>();
        }

        public T[] GetComponentsInParents<T>() where T : class, IComponent
        {
            return componentCollection.GetAllInParents<T>();
        }

        public T[] GetComponentsInChildren<T>() where T : class, IComponent
        {
            return componentCollection.GetAllInChildren<T>();
        }

        public void AddDependency(Entity entity)
        {
            dependencies.Add(entity);
        }

        public void RemoveDependency(Entity entity)
        {
            dependencies.Remove(entity);
        }

        internal void OnComponentChanged(IComponent oldComponent, IComponent newComponent, ComponentChangedAction action)
        {
            ComponentsChanged?.Invoke(this, new EntityComponentEventArgs(this, oldComponent, newComponent, action));
        }

        public event EventHandler<EntityComponentEventArgs> ComponentsChanged;


        public void TraverseInDepth(Action<Entity> action, bool ignoreDisabled = false)
        {
            var stack = new Stack<Entity>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                Entity current = stack.Pop();
                if (ignoreDisabled || current.IsEnabled)
                {
                    action(current);

                    for (int i = 0; i < current.Dependencies.Count; i++)
                    {
                        stack.Push(current.Dependencies[i]);
                    }
                }
            }
        }

        public void TraverseByLayer(Action<Entity> action, bool ignoreDisabled = false)
        {
            var queue = new Queue<Entity>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                Entity current = queue.Dequeue();
                if (ignoreDisabled || current.IsEnabled)
                {
                    action(current);

                    for (int i = 0; i < current.Dependencies.Count; i++)
                    {
                        queue.Enqueue(current.Dependencies[i]);
                    }
                }
            }
        }

        public Entity Duplicate()
        {
            Entity root = new Entity(null, $"{this.Name} (1)");
            System.Collections.Generic.Dictionary<long, Entity> entities = new System.Collections.Generic.Dictionary<long, Entity>();
            entities.Add(Uid, root);
            CloneComponents(root, this);
            TraverseByLayer(current =>
            {
                if (current.Uid == Uid)
                {
                    return;
                }

                if (entities.ContainsKey(current.Owner.Uid))
                {
                    var currentOwner = entities[current.Owner.Uid];
                    var entity = new Entity(currentOwner, current.Name);
                    entities.Add(current.Uid, entity);
                    CloneComponents(entity, current);
                }
            });
            entities.Clear();
            return root;
        }

        private void CloneComponents(Entity clonedEntity, Entity originalEntity)
        {
            foreach (var component in originalEntity.Components)
            {
                if (component is ICloneableComponent cloneable)
                {
                    var cloned = cloneable.Clone();
                    cloneable.CloneValues(cloned);
                    clonedEntity.AddComponent(cloned);
                }
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString() => $"{Name}: {Uid} ";

        public void Dispose()
        {
            componentCollection.Clear();
        }
    }
}
