using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.Events;
using Adamantium.EntityFramework.Payloads;

namespace Adamantium.EntityFramework
{
    public abstract class EntityService : PropertyChangedBase, IEntityService
    {
        private bool enabled;
        private ExecutionType updateExecutionType;
        private int updatePriority;
        private int previousDrawPriority;
        private int previousUpdatePriority;
        
        private int drawPriority;
        private ExecutionType drawExecutionType;
        private bool isVisible;

        protected IEventAggregator EventAggregator { get; }

        public EntityWorld EntityWorld { get; private set; }
        
        public IGraphicsDeviceService GraphicsDeviceService { get; protected set; }
        
        public GraphicsDevice GraphicsDevice { get; protected set; }
        
        public abstract bool IsUpdateService { get; }
        public abstract bool IsRenderingService { get; }
        protected AppTime AppTime { get; set; }
        protected IDependencyResolver DependencyResolver { get; }

        /// <summary>
        /// Gets unique identifier for the system
        /// </summary>
        public UInt128 Uid { get; }

        protected EntityService(EntityWorld world)
        {
            Uid = UidGenerator.Generate();
            EntityWorld = world;
            DependencyResolver = EntityWorld.DependencyResolver;
            IsVisible = true;
            Enabled = true;
            EventAggregator = DependencyResolver.Resolve<IEventAggregator>();
            PropertyChanged += OnPropertyChanged;
        }

        // TODO: check does this property should return all available entities
        public IReadOnlyList<Entity> Entities => EntityWorld.RootEntities;

        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        public bool Enabled
        {
            get => enabled;
            set => SetProperty(ref enabled, value);
        }

        public int UpdatePriority
        {
            get => updatePriority;
            set
            {
                if (updatePriority != value)
                {
                    previousUpdatePriority = updatePriority;
                }

                SetProperty(ref updatePriority, value);
            }
        }

        public virtual void Submit()
        {
            
        }

        public bool IsVisible 
        { 
            get => isVisible; 
            set => SetProperty(ref isVisible, value); 
        }

        public int DrawPriority
        {
            get => drawPriority;
            set
            {
                if (drawPriority != value)
                {
                    previousDrawPriority = updatePriority;
                }

                SetProperty(ref drawPriority, value);
            }
        }

        public ExecutionType UpdateExecutionType
        {
            get => updateExecutionType;
            set => SetProperty(ref updateExecutionType, value);
        }

        public ExecutionType DrawExecutionType
        {
            get => drawExecutionType;
            set => drawExecutionType = value;
        }

        public virtual bool CanDisplayContent => true;

        public virtual void Initialize()
        {
        }

        public IEntityProcessor Processor { get; set; }
        public void AttachProcessor(IEntityProcessor processor)
        {
            Processor = processor;
            Processor?.Attach(this);
        }

        public void DetachProcessor()
        {
            Processor?.Detach();
        }

        public virtual void Update(AppTime gameTime)
        {
            Processor?.Update(gameTime);
        }

        public virtual bool BeginDraw()
        {
            return IsVisible;
        }

        public virtual void Draw(AppTime gameTime)
        {
            Processor?.Draw(gameTime);
        }

        public virtual void EndDraw()
        {

        }

        public virtual void LoadContent()
        {
            Processor?.LoadContent();
        }

        public virtual void UnloadContent()
        {
            Processor?.UnloadContent();
        }

        public virtual void DisplayContent()
        {

        }

        public virtual void FrameEnded()
        {
            
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsVisible):
                    EventAggregator.GetEvent<ProcessorVisibilityChangedEvent>().Publish(new ProcessorStatePayload(this, IsVisible));
                    break;
                case nameof(Enabled):
                    EventAggregator.GetEvent<ProcessorEnabledChangedEvent>().Publish(new ProcessorStatePayload(this, Enabled));
                    break;
                case nameof(UpdatePriority):
                    EventAggregator.GetEvent<ProcessorPriorityChangedEvent>().Publish(new ProcessorPriorityPayload(this, ProcessorType.Update, previousUpdatePriority, UpdatePriority));
                    break;
                case nameof(DrawPriority):
                    EventAggregator.GetEvent<ProcessorPriorityChangedEvent>().Publish(new ProcessorPriorityPayload(this, ProcessorType.Draw, previousDrawPriority, DrawPriority));
                    break;
                case nameof(UpdateExecutionType):
                    {
                        var prevResult = UpdateExecutionType == ExecutionType.Sync ? ExecutionType.Async : ExecutionType.Sync;
                        EventAggregator.GetEvent<ProcessorExecutionTypeChangedEvent>().Publish(new ProcessorExecutionTypePayload(this, ProcessorType.Update, prevResult, UpdateExecutionType));
                        break;
                    }
                case nameof(DrawExecutionType):
                    {
                        var prevResult = DrawExecutionType == ExecutionType.Sync ? ExecutionType.Async : ExecutionType.Sync;
                        EventAggregator.GetEvent<ProcessorExecutionTypeChangedEvent>().Publish(new ProcessorExecutionTypePayload(this, ProcessorType.Draw, prevResult, DrawExecutionType));
                        break;
                    }
            }
        }
    }


}