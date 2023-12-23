using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.EntityFramework.Templates;

namespace Adamantium.EntityFramework
{
    public class EntityWorld
    {
        public EntityWorld(IDependencyResolver container)
        {
            DependencyResolver = container ?? throw new ArgumentNullException($"{nameof(container)} should not be null");
            EntityManager = new EntityManager(this);
            ServiceManager = new EntityServiceManager(this);
            ServiceManager.FrameEnded += FrameEnded;
        }

        public IReadOnlyList<Entity> RootEntities => EntityManager.RootEntities;
        public EntityManager EntityManager { get; }
        public EntityServiceManager ServiceManager { get; }
        public IDependencyResolver DependencyResolver { get; }

        public void Initialize()
        {
            EntityManager.InitializeResources();
            ServiceManager.InitializeResources();
        }

        private void FrameEnded()
        {
            EntityManager.SyncEntities();
        }
        
        public Entity CreateEntity(string name, Entity owner = null, bool createDisabled = false)
        {
            return EntityManager.CreateEntity(name, owner, createDisabled);
        }

        public void AddEntity(Entity root, CameraProjectionType projectionType = CameraProjectionType.Perspective)
        {
            EntityManager.AddEntity(root);
        }

        public void SetCameraProjectionType(Entity rootEntity, CameraProjectionType projectionType)
        {
            EntityManager.SetCameraProjectionType(rootEntity, projectionType);
        }
        
        public CameraProjectionType GetCameraProjectionType(Entity rootEntity)
        {
            return EntityManager.GetCameraProjectionType(rootEntity);
        }

        public CameraProjectionType[] GetAvailableCameraProjectionTypes()
        {
            return EntityManager.GetAvailableCameraProjectionTypes();
        }
        
        public void RemoveEntity(Entity root)
        {
            EntityManager.RemoveEntity(root);
        }

        public void RemoveAllEntities()
        {
            RemoveEntities(RootEntities.ToArray());
        }
        
        public void RemoveEntities(IEnumerable<Entity> entities)
        {
            EntityManager.RemoveEntities(entities);
        }

        public async Task<Entity> CreateEntityFromTemplate(
            IEntityTemplate template, 
            Entity owner = null, 
            string name = "", 
            bool createDisabled = false)
        {
            return await EntityManager.CreateEntityFromTemplate(template, owner, name, createDisabled);
        }

        public T GetService<T>() where T : EntityService
        {
            return ServiceManager.GetService<T>();
        }

        public T[] GetServices<T>() where T : EntityService
        {
            return ServiceManager.GetServices<T>();
        }

        internal void OnEntityAdded(Entity entity)
        {
            EntityAdded?.Invoke(this, new EntityEventArgs(entity));
        }

        internal void OnEntityRemoved(Entity entity)
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
            return EntityManager.CreateGroup(groupName);
        }

        public void AddGroup(EntityGroup group)
        {
            EntityManager.AddGroup(group);
        }

        public void RemoveGroup(string groupName)
        {
            EntityManager.RemoveGroup(groupName);
        }

        public void AddToGroup(Entity entity, string groupName, bool createIfNotExists = true)
        {
            EntityManager.AddToGroup(entity, groupName, createIfNotExists);
        }

        public void RemoveFromGroup(Entity entity, string groupName)
        {
            EntityManager.RemoveFromGroup(entity, groupName);
        }

        public void RemoveFromGroup(IEnumerable<Entity> entities, string groupName)
        {
            EntityManager.RemoveFromGroup(entities, groupName);
        }

        public EntityGroup GetGroup(string groupName)
        {
            return EntityManager.GetGroup(groupName);
        }

        public void ResetGroup(string groupName)
        {
            EntityManager.ResetGroup(groupName);
        }

        public string[] GetAllGroupIds()
        {
            return EntityManager.GetAllGroupIds();
        }

        public EntityGroup[] GetAllGroups()
        {
            return EntityManager.GetAllGroups();
        }
        
        /// <summary>
        /// Search entity with given Uid
        /// </summary>
        /// <param name="uid"><see cref="IIdentifiable.Uid"/></param>
        /// <returns>First found entity</returns>
        public Entity FindEntity(UInt128 uid)
        {
            return EntityManager.FindEntity(uid);
        }

        /// <summary>
        /// Return first entity with given name
        /// </summary>
        /// <param name="name"><see cref="IName.Name"/></param>
        /// <returns></returns>
        public Entity FindEntity(string name)
        {
            return EntityManager.FindEntity(name);
        }

        /// <summary>
        /// Return all entities with given name
        /// </summary>
        /// <param name="name"><see cref="IName.Name"/></param>
        /// <returns></returns>
        public Entity[] FindEntities(string name)
        {
            return EntityManager.FindEntities(name);
        }
        
        internal void OnGroupCreated(EntityGroup group)
        {
            group.GroupChanged += OnGroupChanged;
            GroupCreated?.Invoke(this, new EntityGroupEventArgs(group));
        }

        internal void OnGroupRemoved(EntityGroup group)
        {
            group.GroupChanged -= OnGroupChanged;
            GroupRemoved?.Invoke(this, new EntityGroupEventArgs(group));
        }

        protected void OnGroupChanged(object sender, GroupChangedEventArgs args)
        {
            GroupChanged?.Invoke(sender, args);
        }

        public void Reset()
        {
            EntityManager.Reset();
            ServiceManager.Reset();
            WorldReseted?.Invoke(this, EventArgs.Empty);
        }

        public void Merge(EntityWorld world)
        {
            foreach (var entity in world.EntityManager.RootEntities)
            {
                AddEntity(entity);
            }

            foreach (var group in world.EntityManager.GetAllGroups())
            {
                AddGroup(group);
            }
        }

        public void ForceUpdate()
        {
            EntityManager.SyncEntities();
            ServiceManager.SyncServices();
        }
        
        public event EventHandler<EntityGroupEventArgs> GroupCreated;
        public event EventHandler<EntityGroupEventArgs> GroupRemoved;
        public event EventHandler<GroupChangedEventArgs> GroupChanged;
        public event EventHandler<EventArgs> WorldReseted;
    }
}
