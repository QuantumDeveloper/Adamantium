using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.Templates;

namespace Adamantium.EntityFramework
{
    public class EntityWorld
    {
        private object syncObject = new object();

        private List<Entity> rootEntities;
        private readonly Dictionary<Int64, Entity> availableEntities;
        private readonly Dictionary<String, EntityGroup> entitiesByGroup;

        public EntityWorld(IDependencyContainer serviceStorage)
        {
            rootEntities = new List<Entity>();
            availableEntities = new Dictionary<long, Entity>();
            entitiesByGroup = new Dictionary<String, EntityGroup>();

            Services = serviceStorage;
            System = new EntitySystem(this);
            SystemManager = Services.Resolve<SystemManager>();
            SystemManager.AddSystem(System);
        }

        public Entity[] RootEntities => rootEntities.ToArray();

        public IDependencyContainer Services { get; }

        private readonly EntitySystem System;
        private readonly SystemManager SystemManager;

        public Entity CreateEntity(string name, Entity owner = null, bool addToWorld = true, bool createDisabled = false)
        {
            lock (this)
            {
                Entity entity = new Entity(owner, name);
                if (addToWorld)
                {
                    AddEntity(entity);
                }

                if (createDisabled)
                {
                    entity.IsEnabled = false;
                }

                return entity;
            }
        }

        public void AddEntity(Entity entity)
        {
            if (entity == null)
            {
                return;
            }

            lock (syncObject)
            {
                entity.TraverseByLayer(
                    current =>
                    {
                        if (current.Owner == null && !rootEntities.Contains(current))
                        {
                            rootEntities.Add(current);
                        }

                        if (!availableEntities.ContainsKey(current.Uid))
                        {
                            availableEntities.Add(current.Uid, current);
                            if (current.Owner == null)
                            {
                                AddEntityToProcessors(current);
                            }
                            OnEntityAdded(current);
                        }
                    }
                );
            }
        }

        private void AddEntityToProcessors(Entity entity)
        {
            lock (syncObject)
            {
                foreach (var processor in System.Processors)
                {
                    processor.AddEntity(entity);
                }
            }
        }

        public async Task<Entity> CreateEntityFromTemplate(IEntityTemplate template, Entity owner = null, string name = "", bool addToWorld = true, bool createDisabled = false)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var entity = CreateEntity(name, owner, false, createDisabled);
            await template.BuildEntity(entity);
            if (addToWorld)
            {
                AddEntity(entity);
            }
            return entity;
        }

        public void RemoveEntity(Entity entity)
        {
            if (entity == null)
                return;

            if (entity.Owner == null && rootEntities.Contains(entity))
            {
                rootEntities.Remove(entity);
            }

            lock (syncObject)
            {
                if (availableEntities.ContainsKey(entity.Uid))
                {
                    entity.Owner?.RemoveDependency(entity);
                    availableEntities.Remove(entity.Uid);
                    RemoveFromProcessors(entity);
                    OnEntityRemoved(entity);
                }
            }
        }

        public void RemoveEntities(IEnumerable<Entity> entities)
        {
            if (entities == null)
                return;

            foreach (var entity in entities)
            {
                RemoveEntity(entity);
            }
        }

        public T GetProcessor<T>() where T : EntityProcessor
        {
            return System.GetProcessor<T>();
        }

        public T[] GetProcessors<T>() where T : EntityProcessor
        {
            return System.GetProcessors<T>();
        }

        private void OnEntityAdded(Entity entity)
        {
            EntityAdded?.Invoke(this, new EntityEventArgs(entity));
        }

        private void OnEntityRemoved(Entity entity)
        {
            EntityRemoved?.Invoke(this, new EntityEventArgs(entity));
        }

        public event EventHandler<EntityEventArgs> EntityAdded;
        public event EventHandler<EntityEventArgs> EntityRemoved;

        public void RemoveFromProcessors(Entity entity)
        {
            if (entity == null)
                return;

            lock (syncObject)
            {
                foreach (var system in System.Processors)
                {
                    system.RemoveEntity(entity);
                }
            }
        }

        public T CreateProcessor<T>(object[] args) where T : EntityProcessor
        {
            var processor = (T)Activator.CreateInstance(typeof(T), args);
            AddProcessor(processor);
            return processor;
        }

        public void AddProcessor(EntityProcessor processor)
        {
            System.AddProcessor(processor);
        }

        public void RemoveProcessor(EntityProcessor processor)
        {
            System.RemoveProcessor(processor);
        }

        public void RemoveProcessor(long processorId)
        {
            System.RemoveProcessor(processorId);
        }

        public EntityGroup CreateGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            lock (syncObject)
            {
                if (!entitiesByGroup.ContainsKey(groupName))
                {
                    var group = new EntityGroup(groupName);
                    entitiesByGroup.Add(groupName, group);
                    OnGroupCreated(group);
                    return group;
                }
                else
                {
                    return entitiesByGroup[groupName];
                }
            }
        }

        public void AddGroup(EntityGroup group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            lock (syncObject)
            {
                if (!entitiesByGroup.ContainsKey(group.Name))
                {
                    entitiesByGroup.Add(group.Name, group);
                    OnGroupCreated(group);
                }
                else
                {
                    entitiesByGroup[group.Name].Add(group);
                }
            }
        }

        public void RemoveGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            lock (syncObject)
            {
                if (entitiesByGroup.ContainsKey(groupName))
                {
                    var group = entitiesByGroup[groupName];
                    entitiesByGroup.Remove(groupName);
                    OnGroupRemoved(group);
                }
            }
        }

        protected void OnGroupCreated(EntityGroup group)
        {
            group.GroupChanged += OnGroupChanged;
            GroupCreated?.Invoke(this, new EntityGroupEventArgs(group));
        }

        protected void OnGroupRemoved(EntityGroup group)
        {
            group.GroupChanged -= OnGroupChanged;
            GroupRemoved?.Invoke(this, new EntityGroupEventArgs(group));
        }

        protected void OnGroupChanged(object sender, GroupChangedEventArgs args)
        {
            GroupChanged?.Invoke(sender, args);
        }

        public void AddToGroup(Entity entity, string groupName, bool createIfNotExists = true)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            lock (syncObject)
            {
                EntityGroup group;
                if (entitiesByGroup.TryGetValue(groupName, out group))
                {
                    group.Add(entity);
                }
                else
                {
                    if (!createIfNotExists)
                    {
                        throw new Exception($"No entity group available with name {groupName}");
                    }
                    else
                    {
                        CreateGroup(groupName);
                        AddToGroup(entity, groupName);
                    }
                }
            }
        }

        public void RemoveFromGroup(Entity entity, string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            lock (syncObject)
            {
                EntityGroup group;
                if (entitiesByGroup.TryGetValue(groupName, out group))
                {
                    group.Remove(entity);
                }
                else
                {
                    throw new Exception($"No entity group available with name {groupName}");
                }
            }
        }

        public void AddToGroup(IEnumerable<Entity> entities, string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            lock (syncObject)
            {
                EntityGroup group;
                if (entitiesByGroup.TryGetValue(groupName, out group))
                {
                    group.Add(entities);
                }
                else
                {
                    throw new Exception($"No entity group available with name {groupName}");
                }
            }
        }

        public void RemoveFromGroup(IEnumerable<Entity> entities, string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            lock (syncObject)
            {
                EntityGroup group;
                if (entitiesByGroup.TryGetValue(groupName, out group))
                {
                    group.Remove(entities);
                }
                else
                {
                    throw new Exception($"No entity group available with name {groupName}");
                }
            }
        }

        public EntityGroup GetGroup(string groupName)
        {
            EntityGroup group;
            entitiesByGroup.TryGetValue(groupName, out group);
            return group;
        }

        public void ResetGroup(string groupName)
        {
            lock (syncObject)
            {
                EntityGroup group;
                if (entitiesByGroup.TryGetValue(groupName, out group))
                {
                    group.Clear();
                }
            }
        }

        public string[] GetAllGroupIds()
        {
            string[] groupIds = new string[entitiesByGroup.Count];
            entitiesByGroup.Keys.CopyTo(groupIds, 0);
            return groupIds;
        }

        public EntityGroup[] GetAllGroups()
        {
            EntityGroup[] groupIds = new EntityGroup[entitiesByGroup.Count];
            entitiesByGroup.Values.CopyTo(groupIds, 0);
            return groupIds;
        }

        public event EventHandler<EntityGroupEventArgs> GroupCreated;
        public event EventHandler<EntityGroupEventArgs> GroupRemoved;
        public event EventHandler<GroupChangedEventArgs> GroupChanged;
        public event EventHandler<EventArgs> WorldReseted;

        /// <summary>
        /// Search entity with given Uid
        /// </summary>
        /// <param name="uid"><see cref="IIdentifiable.Uid"/></param>
        /// <returns>First found entity</returns>
        public Entity FindEntity(long uid)
        {
            lock (syncObject)
            {
                availableEntities.TryGetValue(uid, out var entity);
                return entity;
            }
        }

        /// <summary>
        /// Return first entity with given name
        /// </summary>
        /// <param name="name"><see cref="IName.Name"/></param>
        /// <returns></returns>
        public Entity FindEntity(string name)
        {
            lock (syncObject)
            {
                foreach (var availableEntity in availableEntities.Values)
                {
                    if (availableEntity.Name == name)
                    {
                        return availableEntity;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Return all entities with given name
        /// </summary>
        /// <param name="name"><see cref="IName.Name"/></param>
        /// <returns></returns>
        public Entity[] FindEntities(string name)
        {
            lock (syncObject)
            {
                List<Entity> entities = new List<Entity>();
                foreach (var availableEntity in availableEntities.Values)
                {
                    if (availableEntity.Name == name)
                    {
                        entities.Add(availableEntity);
                    }
                }
                return entities.ToArray();
            }
        }

        public void Reset()
        {
            lock (syncObject)
            {
                rootEntities.Clear();
                availableEntities.Clear();
                entitiesByGroup.Clear();
                var systems = System.Processors;
                for (int i = 0; i < systems.Length; i++)
                {
                    systems[i].Reset();
                }
                WorldReseted?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Merge(EntityWorld world)
        {
            foreach (var entity in world.availableEntities)
            {
                AddEntity(entity.Value);
            }

            foreach (var group in entitiesByGroup)
            {
                AddGroup(group.Value);
            }
        }
    }
}
