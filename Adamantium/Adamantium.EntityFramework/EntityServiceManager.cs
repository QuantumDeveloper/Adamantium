using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.EntityFramework
{
    public sealed class EntityServiceManager : PropertyChangedBase
    {
        private readonly object syncObject = new object();

        private readonly Dictionary<Int64, EntityService> activeServices;

        private readonly List<EntityService> servicesToAdd;
        private readonly List<EntityService> servicesToRemove;
        private readonly List<EntityService> pendingServices;

        private readonly AdamantiumCollection<EntityService> services;
        
        private IService appService;

        public EntityWorld EntityWorld { get; }
        
        /// <summary>
        /// A service registry that provides methods to register and unregister services.
        /// </summary>
        public IDependencyResolver DependencyResolver { get; }

        internal EntityServiceManager(EntityWorld world)
        {
            EntityWorld = world;
            DependencyResolver = EntityWorld.DependencyResolver;
            services = new AdamantiumCollection<EntityService>();
            activeServices = new Dictionary<long, EntityService>();
            servicesToAdd = new List<EntityService>();
            servicesToRemove = new List<EntityService>();
            pendingServices = new List<EntityService>();
        }
        
        public IReadOnlyCollection<EntityService> Services => services.AsReadOnly();

        public Action FrameEnded;
        
        internal void InitializeResources()
        {
            appService = DependencyResolver.Resolve<IService>();
            appService.Started += OnServiceStarted;
            appService.ShuttingDown += OnServiceShuttingDown;
        }
        
        private void OnServiceShuttingDown(object sender, EventArgs e)
        {
            UnloadContent();
        }

        private void OnServiceAdded(EntityService service)
        {
            ServiceAdded?.Invoke(this, new EntityServiceEventArgs(service));
        }

        private void OnServiceRemoved(EntityService service)
        {
            service?.UnloadContent();
            ServiceRemoved?.Invoke(this, new EntityServiceEventArgs(service));
        }

        public void OnFrameEnded()
        {
            lock (syncObject)
            {
                foreach (var service in services)
                {
                    service.FrameEnded();
                }
            }
            FrameEnded?.Invoke();
            SyncServices();
        }

        public T GetService<T>() where T : EntityService
        {
            foreach (var service in Services)
            {
                if (service is T variable)
                {
                    return variable;
                }
            }
            return null;
        }

        public T[] GetServices<T>() where T : EntityService
        {
            List<T> list = new List<T>();
            foreach (var service in Services)
            {
                if (service is T variable)
                {
                    list.Add(variable);
                }
            }
            return list.ToArray();
        }

        public void Initialize()
        {
            lock (syncObject)
            {
                foreach (var service in pendingServices)
                {
                    service.Initialize();
                    service.LoadContent();
                }
                pendingServices.Clear();
            }
        }

        public void LoadContent()
        {
            lock (syncObject)
            {
                foreach (var service in Services)
                {
                    service.LoadContent();
                }
            }
        }

        public void UnloadContent()
        {
            lock (syncObject)
            {
                foreach (var service in Services)
                {
                    service.UnloadContent();
                }
            }
        }

        public void Update(AppTime gameTime)
        {
            lock (syncObject)
            {
                foreach (var handler in Services)
                {
                    handler.Update(gameTime);
                }
            }
        }

        public void Draw(AppTime gameTime)
        {
            lock (syncObject)
            {
                foreach (var service in Services)
                {
                    if (service.BeginDraw())
                    {
                        service.Draw(gameTime);
                        service.EndDraw();
                    }
                }
            }
        }

        public void DisplayContent()
        {
            lock (syncObject)
            {
                foreach (var service in Services)
                {
                    if (service.CanDisplayContent)
                    {
                        service.DisplayContent();
                    }
                }
            }
            OnFrameEnded();
        }

        internal void SyncServices()
        {
            if (servicesToRemove.Count > 0)
            {
                foreach (var entity in servicesToRemove)
                {
                    RemoveServiceInternal(entity);
                }
                servicesToRemove.Clear();
            }

            if (servicesToAdd.Count > 0)
            {
                foreach (var entity in servicesToAdd)
                {
                    AddServiceInternal(entity);
                }
                servicesToAdd.Clear();
            }
        }

        public void AddService(EntityService service)
        {
            lock (syncObject)
            {
                servicesToAdd.Add(service);
            }
        }

        public void AddServices(IEnumerable<EntityService> services)
        {
            foreach (var service in services)
            {
                AddService(service);
            }
        }

        public void RemoveService(long uid)
        {
            if (activeServices.TryGetValue(uid, out var service))
            {
                RemoveService(service);
            }
        }

        public void RemoveService(EntityService service)
        {
            lock (syncObject)
            {
                servicesToRemove.Add(service);
            }
        }

        public void RemoveAllServices()
        {
            foreach (var entityService in services)
            {
                RemoveService(entityService);
            }
        }

        public void RemoveServices(IEnumerable<EntityService> services)
        {
            foreach (var service in services)
            {
                RemoveService(service);
            }
        }

        private void AddServiceInternal(EntityService service)
        {
            if (!activeServices.TryGetValue(service.Uid, out var result))
            {
                activeServices.Add(service.Uid, service);
                services.Add(service);
                if (appService.IsRunning)
                {
                    service.Initialize();
                    service.LoadContent();
                }
                else
                {
                    pendingServices.Add(service);
                }
                OnServiceAdded(service);
            }
        }

        private void RemoveServiceInternal(EntityService service)
        {
            if (service == null) return;
            
            if (activeServices.ContainsKey(service.Uid))
            {
                service.UnloadContent();
                activeServices.Remove(service.Uid);
                services.Remove(service);
                OnServiceRemoved(service);
            }
        }

        private void OnServiceStarted(object sender, EventArgs e)
        {
            Initialize();
            LoadContent();
            foreach (var service in pendingServices)
            {
                service.Initialize();
                service.LoadContent();
            }
            pendingServices.Clear();
        }

        public void Reset()
        {
            lock (syncObject)
            {
                services.Clear();
                activeServices.Clear();
                servicesToAdd.Clear();
                servicesToRemove.Clear();
            }
        }
        
        public event EventHandler<EntityServiceEventArgs> ServiceAdded;
        public event EventHandler<EntityServiceEventArgs> ServiceRemoved;

        internal struct UpdatePriorityComparer : IComparer<IUpdatable>
        {
            public static readonly UpdatePriorityComparer Default = new UpdatePriorityComparer();

            public int Compare(IUpdatable left, IUpdatable right)
            {
                if (Equals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                return left.UpdatePriority.CompareTo(right.UpdatePriority);
            }
        }

        internal struct DrawPriorityComparer : IComparer<IDrawable>
        {
            public static readonly DrawPriorityComparer Default = new DrawPriorityComparer();

            public int Compare(IDrawable left, IDrawable right)
            {
                if (Equals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                return left.DrawPriority.CompareTo(right.DrawPriority);
            }
        }
    }
}
