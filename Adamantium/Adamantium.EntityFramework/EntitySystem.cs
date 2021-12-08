using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;

namespace Adamantium.EntityFramework
{
    public sealed class EntitySystem : SystemCore
    {
        private readonly object syncObject = new Object();

        private List<EntityProcessor> availables;
        private Dictionary<Int64, EntityProcessor> activeProcessors;

        private readonly List<EntityProcessor> processorsToAdd;
        private readonly List<EntityProcessor> processorsToRemove;
        private readonly List<EntityProcessor> pendingProcessors;

        private EntityProcessor[] _processors;

        public EntityWorld EntityWorld { get; }

        internal EntitySystem(EntityWorld world) : base(world.Services)
        {
            EntityWorld = world;
            Enabled = true;
            _processors = new EntityProcessor[0];
            activeProcessors = new Dictionary<long, EntityProcessor>();
            availables = new List<EntityProcessor>();
            processorsToAdd = new List<EntityProcessor>();
            processorsToRemove = new List<EntityProcessor>();
            pendingProcessors = new List<EntityProcessor>();
        }

        public EntityProcessor[] Processors => _processors;

        public event EventHandler<EntityProcessorEventArgs> ProcessorAdded;
        public event EventHandler<EntityProcessorEventArgs> ProcessorRemoved;

        public Action FrameEnded;

        private void OnProcessorAdded(EntityProcessor processor)
        {
            ProcessorAdded?.Invoke(this, new EntityProcessorEventArgs(processor));
        }

        private void OnProcessorRemoved(EntityProcessor processor)
        {
            processor?.UnloadContent();
            ProcessorRemoved?.Invoke(this, new EntityProcessorEventArgs(processor));
        }

        private void OnFrameEnded()
        {
            FrameEnded?.Invoke();
        }

        public T GetProcessor<T>() where T : EntityProcessor
        {
            foreach (var processor in Processors)
            {
                if (processor is T variable)
                {
                    return variable;
                }
            }
            return null;
        }

        public T[] GetProcessors<T>() where T : EntityProcessor
        {
            List<T> list = new List<T>();
            foreach (var processor in Processors)
            {
                if (processor is T variable)
                {
                    list.Add(variable);
                }
            }
            return list.ToArray();
        }

        public override void Initialize()
        {
            base.Initialize();
            lock (syncObject)
            {
                foreach (var handler in pendingProcessors)
                {
                    handler.Initialize();
                    handler.LoadContent();
                }
                pendingProcessors.Clear();
            }
        }

        public override void LoadContent()
        {
            base.LoadContent();
            lock (syncObject)
            {
                foreach (var handler in Processors)
                {
                    handler.LoadContent();
                }
            }
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
            lock (syncObject)
            {
                foreach (var handler in Processors)
                {
                    handler.UnloadContent();
                }
            }
        }

        /// <summary>
        /// Starts application update
        /// </summary>
        public override void BeginUpdate()
        {
            lock (syncObject)
            {
                SyncProcessors();
                foreach (var processor in Processors)
                {
                    processor.BeginUpdate();
                }
            }
        }

        public override void Update(IGameTime gameTime)
        {
            lock (syncObject)
            {
                foreach (var handler in Processors)
                {
                    handler.Update(gameTime);
                }
            }
        }

        public override void Draw(IGameTime gameTime)
        {
            base.Draw(gameTime);
            lock (syncObject)
            {
                foreach (var processor in Processors)
                {
                    if (processor.BeginDraw())
                    {
                        processor.Draw(gameTime);
                        processor.EndDraw();
                    }
                }
            }
        }

        public override void EndDraw()
        {
            base.EndDraw();
            OnFrameEnded();
        }

        private void SyncProcessors()
        {
            bool changesMade = false;
            if (processorsToRemove.Count > 0)
            {
                foreach (var entity in processorsToRemove)
                {
                    RemoveProcessorInternal(entity);
                }
                processorsToRemove.Clear();
                changesMade = true;
            }

            if (processorsToAdd.Count > 0)
            {
                foreach (var entity in processorsToAdd)
                {
                    AddProcessorInternal(entity);
                }
                processorsToAdd.Clear();
                changesMade = true;
            }

            if (changesMade)
            {
                _processors = availables.ToArray();
            }
        }

        public void AddProcessor(EntityProcessor processor)
        {
            lock (syncObject)
            {
                processorsToAdd.Add(processor);
            }
        }

        public void AddProcessors(IEnumerable<EntityProcessor> processors)
        {
            foreach (var processor in processors)
            {
                AddProcessor(processor);
            }
        }

        public void RemoveProcessor(long uid)
        {
            if (activeProcessors.TryGetValue(uid, out var processor))
            {
                RemoveProcessor(processor);
            }
        }

        public void RemoveProcessor(EntityProcessor processor)
        {
            lock (syncObject)
            {
                processorsToRemove.Add(processor);
            }
        }

        public void RemoveProcessors(IEnumerable<EntityProcessor> processors)
        {
            foreach (var processor in processors)
            {
                RemoveProcessor(processor);
            }
        }

        private void AddProcessorInternal(EntityProcessor processor)
        {
            if (!activeProcessors.TryGetValue(processor.Uid, out var result))
            {
                activeProcessors.Add(processor.Uid, processor);
                availables.Add(processor);
                if (AppService.IsRunning)
                {
                    processor.Initialize();
                    processor.LoadContent();
                }
                else
                {
                    pendingProcessors.Add(processor);
                }
                OnProcessorAdded(processor);
            }
        }

        private void RemoveProcessorInternal(EntityProcessor processor)
        {
            if (activeProcessors.ContainsKey(processor.Uid))
            {
                activeProcessors.Remove(processor.Uid);
                availables.Remove(processor);
                OnProcessorRemoved(processor);
            }
        }

        protected override void OnRunningServiceInitialized(object sender, EventArgs eventArgs)
        {
            base.OnRunningServiceInitialized(sender, eventArgs);
            foreach (var processor in pendingProcessors)
            {
                processor.Initialize();
                processor.LoadContent();
            }
            pendingProcessors.Clear();
        }

        public void Reset()
        {
            lock (syncObject)
            {
                availables.Clear();
                activeProcessors.Clear();
                processorsToAdd.Clear();
                processorsToRemove.Clear();
            }
        }
    }
}
