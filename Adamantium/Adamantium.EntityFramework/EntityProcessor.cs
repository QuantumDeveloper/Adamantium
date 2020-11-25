using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework
{
    public abstract class EntityProcessor : PropertyChangedBase, IIdentifiable, IUpdatable, IDrawable, IContentable
    {
        private readonly object syncObject = new Object();

        private Dictionary<Int64, Entity> availableEntities;
        private List<Entity> activeEntities;
        private Entity[] availables;

        private readonly List<Entity> entitiesToAdd;
        private readonly List<Entity> entitiesToRemove;
        private readonly long _uid;

        protected EntityWorld EntityWorld { get; private set; }
        protected IGameTime GameTime { get; set; }
        protected IDependencyResolver Services { get; }

        protected EntityProcessor(EntityWorld world)
        {
            EntityWorld = world;
            Services = EntityWorld.Services;
            availableEntities = new Dictionary<long, Entity>();
            availables = new Entity[0];
            activeEntities = new List<Entity>();
            entitiesToAdd = new List<Entity>();
            entitiesToRemove = new List<Entity>();
            _uid = UidGenerator.Generate();
            IsVisible = true;
            Enabled = true;
        }

        public void AddEntity(Entity entity)
        {
            lock (syncObject)
            {
                entitiesToAdd.Add(entity);
            }
        }

        public void AddEntities(IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
            {
                AddEntity(entity);
            }
        }

        public void RemoveEntity(Entity entity)
        {
            lock (syncObject)
            {
                entitiesToRemove.Add(entity);
            }
        }

        public void RemoveEntitites(IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
            {
                RemoveEntity(entity);
            }
        }

        public Entity GetEntity(Int64 entityId)
        {
            availableEntities.TryGetValue(entityId, out Entity result);
            return result;
        }

        protected virtual void OnEntityAdded(Entity entity)
        {
        }

        protected virtual void OnEntityRemoved(Entity entity)
        {
        }

        public virtual void Initialize()
        {
        }

        public Entity[] Entities => availables;

        private void SyncEntites()
        {
            bool changesMade = false;

            if (entitiesToRemove.Count > 0)
            {
                foreach (var entity in entitiesToRemove)
                {
                    RemoveEntityInternal(entity);
                }
                entitiesToRemove.Clear();
                changesMade = true;
            }

            if (entitiesToAdd.Count > 0)
            {
                foreach (var entity in entitiesToAdd)
                {
                    AddEntityInternal(entity);
                }
                entitiesToAdd.Clear();
                changesMade = true;
            }

            if (changesMade)
            {
                availables = activeEntities.ToArray();
            }
        }

        private void AddEntityInternal(Entity entity)
        {
            if (!availableEntities.ContainsKey(entity.Uid))
            {
                availableEntities.Add(entity.Uid, entity);
                activeEntities.Add(entity);
                OnEntityAdded(entity);
            }
        }

        private void RemoveEntityInternal(Entity entity)
        {
            if (availableEntities.ContainsKey(entity.Uid))
            {
                availableEntities.Remove(entity.Uid);
                OnEntityRemoved(entity);
            }
        }

        public void Reset()
        {
            lock (syncObject)
            {
                availableEntities.Clear();
                entitiesToAdd.Clear();
                entitiesToRemove.Clear();
            }
        }

        public long Uid => _uid;

        public virtual void BeginUpdate()
        {
            lock (syncObject)
            {
                SyncEntites();
            }
        }

        public virtual void Update(IGameTime gameTime)
        {
            
        }

        public bool Enabled { get; }

        public int UpdatePriority { get; }

        public ExecutionType UpdateExecutionType { get; set; }

        public event EventHandler<ExecutionTypeEventArgs> UpdateExecutionTypeChanged;
        public event EventHandler<StateEventArgs> EnabledChanged;
        public event EventHandler<PriorityEventArgs> UpdatePriorityChanged;

        public virtual bool BeginDraw()
        {
            return IsVisible;
        }

        public virtual void Draw(IGameTime gameTime)
        {
            
        }

        public virtual void EndDraw()
        {
            
        }

        public bool IsVisible { get; }

        public int DrawPriority { get; }

        public ExecutionType DrawExecutionType { get; set; }
        public event EventHandler<StateEventArgs> VisibilityChanged;
        public event EventHandler<PriorityEventArgs> DrawPriorityChanged;
        public event EventHandler<ExecutionTypeEventArgs> DrawExecutionTypeChanged;

        public virtual void LoadContent()
        {
            
        }

        public virtual void UnloadContent()
        {
            
        }

        
    }
}