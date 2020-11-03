using System;
using System.Threading.Tasks;
using Adamantium.Core.Collections;

namespace Adamantium.Engine.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Implementation of System manager
    /// </summary>
    public abstract class SystemManager
    {
        private object syncObject = new object();


        private readonly AdamantiumCollection<ISystem> allSystems;
        private Dictionary<Type, List<ISystem>> systemsByType;
        private Dictionary<ExecutionType, List<IUpdatable>> updatableSystems;
        private Dictionary<ExecutionType, List<IDrawable>> drawableSystems;
        private Dictionary<long, IContentable> contentableSystems;
        private Dictionary<long, ISystem> systemUids;
        private List<ISystem> pendingSystems;

        private IService runningService;

        List<Task> updateTasks = new List<Task>();
        List<Task> drawTasks = new List<Task>();

        private bool isShuttingDown = false;

        /// <summary>
        /// Initializes instance of <see cref="SystemManager"/>
        /// </summary>
        /// <param name="runningService"></param>
        protected SystemManager(IService runningService)
        {
            this.runningService = runningService;

            systemsByType = new Dictionary<Type, List<ISystem>>();
            allSystems = new AdamantiumCollection<ISystem>();
            updatableSystems = new Dictionary<ExecutionType, List<IUpdatable>>();
            drawableSystems = new Dictionary<ExecutionType, List<IDrawable>>();
            contentableSystems = new Dictionary<long, IContentable>();
            systemUids = new Dictionary<long, ISystem>();
            pendingSystems = new List<ISystem>();

            this.runningService.Initialized += RunningServiceInitialized;
            this.runningService.ContentLoading += RunningServiceContentLoading;
            this.runningService.ContentUnloading += RunningServiceContentUnloading;
            this.runningService.ShuttingDown += RunningServiceShuttingDown;
        }

        private void RunningServiceInitialized(object sender, EventArgs e)
        {
            InitializePendingSystems();
        }

        private void RunningServiceContentLoading(object sender, EventArgs e)
        {
            lock (contentableSystems)
            {
                foreach (var system in contentableSystems)
                {
                    system.Value.LoadContent();
                }
            }
        }

        private void RunningServiceContentUnloading(object sender, EventArgs e)
        {
            if (isShuttingDown)
            {
                return;
            }

            lock (contentableSystems)
            {
                foreach (var system in contentableSystems)
                {
                    system.Value.UnloadContent();
                }
            }
        }

        private void RunningServiceShuttingDown(object sender, EventArgs e)
        {
            RemoveAllSystems();
        }


        /// <summary>
        /// Read only collection of all <see cref="ISystem"/>s, added to this <see cref="SystemManager"/> instance
        /// </summary>
        public IReadOnlyCollection<ISystem> Systems => allSystems.AsReadOnly();

        /// <summary>
        /// Initializing each system, which were added to the manager before application was initialized.
        /// </summary>
        private void InitializePendingSystems()
        {
            lock (pendingSystems)
            {
                foreach (var system in pendingSystems)
                {
                    system.Initialize();
                }
                pendingSystems.Clear();
            }
        }


        /// <summary>
        /// Adds system to manager
        /// </summary>
        /// <param name="system">System to add</param>
        /// <returns>Unique id of the added system</returns>
        public virtual void AddSystem(ISystem system)
        {
            lock (syncObject)
            {
                if (systemUids.ContainsKey(system.Uid))
                {
                    return;
                }

                systemUids.Add(system.Uid, system);
                allSystems.Add(system);

                List<ISystem> systems;
                if (systemsByType.TryGetValue(system.GetType(), out systems))
                {
                    systems.Add(system);
                }
                else
                {
                    systems = new List<ISystem> { system };
                    systemsByType.Add(system.GetType(), systems);
                }

                if (runningService.IsRunning)
                {
                    system.Initialize();
                }
                else
                {
                    pendingSystems.Add(system);
                }

                if (system is IContentable contentable)
                {
                    lock (contentableSystems)
                    {
                        contentableSystems.Add(system.Uid, contentable);
                    }

                    if (runningService.IsRunning)
                    {
                        contentable.LoadContent();
                    }
                }

                if (system is IUpdatable updatable)
                {
                    lock (updatableSystems)
                    {
                        List<IUpdatable> list;
                        if (!updatableSystems.TryGetValue(updatable.UpdateExecutionType, out list))
                        {
                            list = new List<IUpdatable>();
                            list.Add(updatable);
                            updatableSystems.Add(updatable.UpdateExecutionType, list);
                        }
                        else
                        {
                            if (!list.Contains(updatable))
                            {
                                list.Add(updatable);
                            }
                            list.Sort(UpdatePriorityComparer.Default);
                        }
                    }
                    updatable.UpdatePriorityChanged += UpdatePriorityChanged;
                    updatable.UpdateExecutionTypeChanged += UpdateExecutionTypeChanged;
                }

                if (system is IDrawable drawable)
                {
                    lock (drawableSystems)
                    {
                        List<IDrawable> list;
                        if (!drawableSystems.TryGetValue(drawable.DrawExecutionType, out list))
                        {
                            list = new List<IDrawable>();
                            list.Add(drawable);
                            drawableSystems.Add(drawable.DrawExecutionType, list);
                        }
                        else
                        {
                            list.Add(drawable);
                            list.Sort(DrawPriorityComparer.Default);
                        }
                    }
                    drawable.DrawPriorityChanged += DrawPriorityChanged;
                    drawable.DrawExecutionTypeChanged += DrawExecutionTypeChanged;
                }

                SystemAdded?.Invoke(this, new SystemEventArgs(system));

            }
        }

        private void UpdateExecutionTypeChanged(Object sender, ExecutionTypeEventArgs e)
        {
            lock (updatableSystems)
            {
                updatableSystems[e.PreviousExecutionType].Remove(sender as IUpdatable);
                updatableSystems[e.CurrentExecutionType].Add(sender as IUpdatable);
                updatableSystems[e.CurrentExecutionType].Sort(UpdatePriorityComparer.Default);
            }
        }

        private void DrawExecutionTypeChanged(Object sender, ExecutionTypeEventArgs e)
        {
            lock (drawableSystems)
            {
                drawableSystems[e.PreviousExecutionType].Remove(sender as IDrawable);
                drawableSystems[e.CurrentExecutionType].Add(sender as IDrawable);
                drawableSystems[e.CurrentExecutionType].Sort(DrawPriorityComparer.Default);
            }
        }

        private void UpdatePriorityChanged(Object sender, PriorityEventArgs e)
        {
            lock (updatableSystems)
            {
                var updatable = (IUpdatable)sender;
                var list = updatableSystems[updatable.UpdateExecutionType];
                list.Sort(UpdatePriorityComparer.Default);
            }
        }

        private void DrawPriorityChanged(object sender, PriorityEventArgs e)
        {
            lock (drawableSystems)
            {
                var drawable = (IDrawable)sender;
                var list = drawableSystems[drawable.DrawExecutionType];
                list.Sort(DrawPriorityComparer.Default);
            }
        }

        /// <summary>
        /// Creates system from <see cref="Type"/> and parameters for constaructor and adds system to manager
        /// </summary>
        /// <param name="args">Constructor parameters for <see cref="ISystem"/></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>Unique id of the added system</returns>
        public ISystem CreateSystem<T>(object[] args) where T : ISystem
        {
            var system = (ISystem)Activator.CreateInstance(typeof(T), args);
            AddSystem(system);
            return system;
        }

        /// <summary>
        /// Removes all <see cref="ISystem"/>s
        /// </summary>
        public void RemoveAllSystems()
        {
            isShuttingDown = true;
            lock (syncObject)
            {
                foreach (var system in allSystems)
                {
                    var contentable = system as IContentable;
                    contentable?.UnloadContent();

                    var disposable = system as IDisposable;
                    disposable?.Dispose();
                }

                allSystems.Clear();
                systemUids.Clear();
                systemsByType.Clear();
                pendingSystems.Clear();
                contentableSystems.Clear();
                updatableSystems.Clear();
                drawableSystems.Clear();
            }
            isShuttingDown = false;
        }

        /// <summary>
        /// Removes <see cref="ISystem"/> by instance
        /// </summary>
        /// <param name="system">concrete system</param>
        public bool RemoveSystem(ISystem system)
        {
            if (system == null)
            {
                return false;
            }

            if (!runningService.IsRunning)
            {
                pendingSystems.Remove(system);
            }

            List<ISystem> lst;
            if (systemsByType.TryGetValue(system.GetType(), out lst))
            {
                lst.Remove(system);
            }

            lock (this)
            {
                if (!systemUids.ContainsKey(system.Uid))
                {
                    return false;
                }

                allSystems.Remove(system);
                systemUids.Remove(system.Uid);
            }

            lock (contentableSystems)
            {
                IContentable contentable;
                if (contentableSystems.TryGetValue(system.Uid, out contentable))
                {
                    contentableSystems.Remove(system.Uid);

                    if (runningService.IsRunning)
                    {
                        contentable.UnloadContent();
                    }
                }
            }

            var updatable = system as IUpdatable;
            if (updatable != null)
            {
                lock (updatableSystems)
                {
                    List<IUpdatable> list;
                    if (updatableSystems.TryGetValue(updatable.UpdateExecutionType, out list))
                    {
                        list.Remove(updatable);
                        list.Sort(UpdatePriorityComparer.Default);
                    }
                }
                updatable.UpdatePriorityChanged -= UpdatePriorityChanged;
                updatable.UpdateExecutionTypeChanged -= UpdateExecutionTypeChanged;
            }

            var drawable = system as IDrawable;
            if (drawable != null)
            {
                lock (drawableSystems)
                {
                    List<IDrawable> list;
                    if (drawableSystems.TryGetValue(drawable.DrawExecutionType, out list))
                    {
                        list.Remove(drawable);
                        list.Sort(DrawPriorityComparer.Default);
                    }
                }
                drawable.DrawPriorityChanged -= DrawPriorityChanged;
                drawable.DrawExecutionTypeChanged -= DrawExecutionTypeChanged;
            }

            SystemRemoved?.Invoke(this, new SystemEventArgs(system));

            return true;
        }

        /// <summary>
        /// Removes system by unique id
        /// </summary>
        /// <param name="uid">System id</param>
        public bool RemoveSystem(long uid)
        {
            if (!systemUids.ContainsKey(uid))
            {
                return false;
            }

            return RemoveSystem(systemUids[uid]);
        }

        /// <summary>
        /// Returns collection of <see cref="ISystem"/>s
        /// </summary>
        /// <typeparam>Type of <see cref="ISystem"/></typeparam>
        /// <typeparam name="T"><see cref="ISystem"/> type</typeparam>
        /// <returns>Returns <see cref="IReadOnlyCollection{T}"/> of systems available by <typeparam>T</typeparam></returns>
        public T[] GetSystems<T>() where T : ISystem
        {
            lock (syncObject)
            {
                List<T> collection = new List<T>();
                foreach (var system in allSystems)
                {
                    if (system is T)
                    {
                        collection.Add((T)system);
                    }
                }
                return collection.ToArray();
            }
        }

        /// <summary>
        /// Retrieves <see cref="ISystem"/> instance by its uid
        /// </summary>
        /// <param name="uid">System unique id</param>
        /// <returns><see cref="ISystem"/> instance.</returns>
        public ISystem GetSystem(Int64 uid)
        {
            ISystem system;
            if (systemUids.TryGetValue(uid, out system))
            {
                return system;
            }
            return null;
        }

        /// <summary>
        /// Runs all <see cref="IUpdatable"/> systems
        /// </summary>
        /// <param name="time"></param>
        public virtual void Update(IGameTime time)
        {
            lock (updatableSystems)
            {
                List<IUpdatable> updatables;
                if (updatableSystems.TryGetValue(ExecutionType.Async, out updatables))
                {
                    updateTasks.Clear();
                    foreach (var updatable in updatables)
                    {
                        if (updatable.Enabled)
                        {
                            updateTasks.Add(Task.Factory.StartNew(() => ExecuteUpdateBlock(updatable, time)));
                        }
                    }
                }

                if (updatableSystems.TryGetValue(ExecutionType.Sync, out updatables))
                {
                    foreach (var updatable in updatables)
                    {
                        if (updatable.Enabled)
                        {
                            ExecuteUpdateBlock(updatable, time);
                        }
                    }
                }

                Task.WaitAll(updateTasks.ToArray());
            }
        }

        private void ExecuteUpdateBlock(IUpdatable updatableSystem, IGameTime appTime)
        {
            updatableSystem.BeginUpdate();
            updatableSystem.Update(appTime);
        }

        /// <summary>
        /// Runs all <see cref="IDrawable"/> systems
        /// </summary>
        /// <param name="time"></param>
        public virtual void Draw(IGameTime time)
        {
            lock (drawableSystems)
            {
                List<IDrawable> drawables;
                if (drawableSystems.TryGetValue(ExecutionType.Async, out drawables))
                {
                    drawTasks.Clear();
                    foreach (var drawable in drawables)
                    {
                        if (drawable.IsVisible)
                        {
                            drawTasks.Add(Task.Factory.StartNew(() => ExecuteDrawBlock(drawable, time)));
                        }
                    }
                }

                if (drawableSystems.TryGetValue(ExecutionType.Sync, out drawables))
                {
                    foreach (var drawable in drawables)
                    {
                        if (drawable.IsVisible)
                        {
                            ExecuteDrawBlock(drawable, time);
                        }
                    }
                }

                Task.WaitAll(drawTasks.ToArray());
            }
        }

        private void ExecuteDrawBlock(IDrawable drawable, IGameTime time)
        {
            if (drawable.BeginDraw())
            {
                drawable.Draw(time);
                drawable.EndDraw();
            }
        }

        /// <summary>
        /// Fires when new <see cref="ISystem"/> is added
        /// </summary>
        public event EventHandler<SystemEventArgs> SystemAdded;

        /// <summary>
        /// Fires when existing <see cref="ISystem"/> is removed
        /// </summary>
        public event EventHandler<SystemEventArgs> SystemRemoved;

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
