using Adamantium.Core;
using Adamantium.Core.Collections;
using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Graphics.Core;

namespace Adamantium.ECS
{
    public sealed class EntityServiceManager : PropertyChangedBase
    {
        private readonly object syncObject = new object();

        private readonly Dictionary<UInt128, EntityService> activeServices;

        private readonly List<EntityService> servicesToAdd;
        private readonly List<EntityService> servicesToRemove;
        private readonly List<EntityService> pendingServices;

        private readonly AdamantiumCollection<EntityService> services;
        
        private IService appService;

        public EntityWorld EntityWorld { get; }
        
        /// <summary>
        /// A service registry that provides methods to register and unregister services.
        /// </summary>
        public IDependencyResolver Container { get; }

        internal EntityServiceManager(EntityWorld world)
        {
            EntityWorld = world;
            Container = EntityWorld.DependencyResolver;
            services = new AdamantiumCollection<EntityService>();
            activeServices = new Dictionary<UInt128, EntityService>();
            servicesToAdd = new List<EntityService>();
            servicesToRemove = new List<EntityService>();
            pendingServices = new List<EntityService>();
        }
        
        public IReadOnlyCollection<EntityService> Services => services.AsReadOnly();

        // The per-frame phases (Update, Draw, Present) iterate THIS immutable snapshot, not the live collection under a lock.
        //
        // They used to hold `syncObject` for their whole body - and Draw's body is an entire GPU frame: BeginDraw's fence wait,
        // the draw, submit, present. Once Draw moved to the render thread that lock became a hard lock-step: the loop thread
        // entering Update BLOCKED until the render thread had finished presenting. Measured on the 60k grid: 60-280 ms of a
        // 200 ms loop frame was the loop standing still on this lock - more than layout and the render record combined, and
        // invisible to every phase timer, because waiting is not work. It is exactly the backpressure the render-thread split
        // exists to remove, hidden one layer down.
        //
        // The collection itself only ever changes in SyncServices (adds/removes are queued and applied there), so a snapshot
        // published on each change is all the iterators need - and they need no lock at all.
        private volatile EntityService[] _snapshot = [];

        private void RepublishSnapshot() => _snapshot = [.. services];

        public Action FrameEnded;
        
        internal void InitializeResources()
        {
            appService = Container.Resolve<IService>();
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
            var list = new List<T>();
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

        // No lock: iterate the published snapshot. Update runs on the loop thread and Draw/Present on the render thread, and
        // sharing one lock made the loop wait out the whole GPU frame (see _snapshot).
        public void Update(AppTime gameTime)
        {
            foreach (var handler in _snapshot)
            {
                handler.Update(gameTime);
            }
        }

        public void Draw(AppTime gameTime)
        {
            foreach (var service in _snapshot)
            {
                if (!service.IsRenderingService) continue;

                if (!service.BeginDraw()) continue;

                OnDrawStarted?.Invoke(service, gameTime);
                service.Draw(gameTime);
                service.EndDraw();
                OnDrawFinished?.Invoke(service, gameTime);
                service.Submit();
            }
        }

        public void Present()
        {
            foreach (var service in _snapshot)
            {
                if (service.CanDisplayContent)
                {
                    service.Present();
                }
            }
            OnFrameEnded();
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

        internal void SyncServices()
        {
            lock (syncObject)
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

                RepublishSnapshot();   // the ONLY place the service set changes - so the only place the iterators' view moves
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

        public void RemoveService(UInt128 uid)
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

        public event Action<IEntityService, AppTime> OnDrawStarted;

        public event Action<IEntityService, AppTime> OnDrawFinished; 
    }
}
