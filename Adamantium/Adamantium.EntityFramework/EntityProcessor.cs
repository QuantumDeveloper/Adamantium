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

        private readonly long _uid;

        protected EntityWorld EntityWorld { get; private set; }
        protected IGameTime GameTime { get; set; }
        protected IDependencyResolver Services { get; }

        protected EntityProcessor(EntityWorld world)
        {
            EntityWorld = world;
            Services = EntityWorld.Services;
            availableEntities = new Dictionary<long, Entity>();
            _uid = UidGenerator.Generate();
            IsVisible = true;
            Enabled = true;
        }
        
        public virtual void Initialize()
        {
        }

        public Entity[] Entities => EntityWorld.RootEntities;

        public virtual void Reset()
        {
        }

        public long Uid => _uid;

        public virtual void BeginUpdate()
        {
            
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