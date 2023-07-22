using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly List<Entity> rootEntities;
        private readonly Dictionary<UInt128, Entity> availableEntities;
        private readonly Dictionary<String, EntityGroup> entitiesByGroup;
        private readonly List<Entity> entitiesToAdd;
        private readonly List<Entity> entitiesToRemove;

        public EntityWorld(IDependencyResolver container)
        {
            rootEntities = new List<Entity>();
            availableEntities = new Dictionary<UInt128, Entity>();
            entitiesByGroup = new Dictionary<String, EntityGroup>();
            entitiesToAdd = new List<Entity>();
            entitiesToRemove = new List<Entity>();

            DependencyResolver = container ?? throw new ArgumentNullException($"{nameof(container)} should not be null");
            ServiceManager = new EntityServiceManager(this);
            ServiceManager.FrameEnded += FrameEnded;
        }

        public IReadOnlyList<Entity> RootEntities => rootEntities.AsReadOnly();

        public EntityServiceManager ServiceManager { get; }

        public IDependencyResolver DependencyResolver { get; }

        public void Initialize()
        {
            ServiceManager.InitializeResources();
        }

        private void FrameEnded()
        {
            SyncEntities();
        }
        
        private void SyncEntities()
        {
            lock (syncObject)
            {
                if (entitiesToRemove.Count > 0)
                {
                    foreach (var entity in entitiesToRemove)
                    {
                        RemoveEntityInternal(entity);
                    }
                    entitiesToRemove.Clear();
                }

                if (entitiesToAdd.Count > 0)
                {
                    foreach (var entity in entitiesToAdd)
                    {
                        AddEntityInternal(entity);
                    }
                    entitiesToAdd.Clear();
                }
            }
        }
        
        private void AddEntityInternal(Entity entity)
        {
            if (availableEntities.TryAdd(entity.Uid, entity))
            {
                rootEntities.Add(entity);
                OnEntityAdded(entity);
            }
        }

        private void RemoveEntityInternal(Entity entity)
        {
            if (availableEntities.Remove(entity.Uid))
            {
                rootEntities.Remove(entity);
                OnEntityRemoved(entity);
            }
        }

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

        public void AddEntity(Entity root)
        {
            if (root == null)
            {
                return;
            }

            lock (syncObject)
            {
                if (availableEntities.ContainsKey(root.Uid)) return;
                
                entitiesToAdd.Add(root);
            }
        }
        
        public void RemoveEntity(Entity root)
        {
            if (root == null)
                return;

            if (root.Owner == null && rootEntities.Contains(root))
            {
                rootEntities.Remove(root);
            }

            lock (syncObject)
            {
                if (!availableEntities.ContainsKey(root.Uid)) return;
                
                root.Owner?.RemoveDependency(root);
                entitiesToRemove.Add(root);
            }
        }

        public void RemoveAllEntities()
        {
            RemoveEntities(RootEntities.ToArray());
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

        public T GetService<T>() where T : EntityService
        {
            return ServiceManager.GetService<T>();
        }

        public T[] GetServices<T>() where T : EntityService
        {
            return ServiceManager.GetServices<T>();
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

        public T CreateService<T>(params object[] args) where T : EntityService
        {
            var service = (T)Activator.CreateInstance(typeof(T), args);
            AddService(service);
            return service;
        }

        public void AddService(EntityService service)
        {
            ServiceManager.AddService(service);
        }

        public void RemoveService(EntityService service)
        {
            ServiceManager.RemoveService(service);
        }

        public void RemoveService(UInt128 processorId)
        {
            ServiceManager.RemoveService(processorId);
        }
        
        public void AddServices(params EntityService[] services)
        {
            ServiceManager.AddServices(services);
        }
        
        public void RemoveAllServices()
        {
            ServiceManager.RemoveAllServices();
        }

        public void RemoveServices(params EntityService[] services)
        {
            ServiceManager.RemoveServices(services);
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
        public Entity FindEntity(UInt128 uid)
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
                ServiceManager.Reset();
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

        public void ForceUpdate()
        {
            SyncEntities();
            ServiceManager.SyncServices();
        }
    }
}
