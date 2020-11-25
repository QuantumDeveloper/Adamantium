using System;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core.Content;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// Base class definiting an abstract system
    /// </summary>
    public abstract class SystemCore : PropertyChangedBase, ISystem, IUpdatable, IDrawable, IContentable
    {
        private int drawPriority;
        private int updatePriority;
        private bool isVisible;
        private bool isEnabled;
        private ExecutionType updatExecutionType;
        private ExecutionType drawExecutionType;
        private readonly DisposeCollector contentCollector = new DisposeCollector();
        protected SystemManager SystemManager { get; }
        protected readonly IService AppService;

        /// <summary>
        /// Constructs <see cref="ISystem"/>
        /// </summary>
        /// <param name="container"><see cref="IDependencyResolver"/> instance. Could be null</param>
        protected SystemCore(IDependencyResolver container)
        {
            Services = container;
            AppService = Services.Resolve<IService>();
            AppService.Initialized += OnRunningServiceInitialized;
            isEnabled = true;
            isVisible = true;
            Uid = UidGenerator.Generate();
            SystemManager = container.Resolve<SystemManager>();
        }

        protected virtual void OnRunningServiceInitialized(object sender, EventArgs eventArgs)
        {
            
        }

        /// <summary>
        /// A service registry that provides methods to register and unregister services.
        /// </summary>
        public IDependencyResolver Services { get; }

        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets unique identifier for the system
        /// </summary>
        public Int64 Uid { get; internal set; }

        /// <summary>
        /// This method is called when the component is added to the game.
        /// </summary>
        /// <remarks>
        /// This method can be used for tasks like querying for services the component needs and setting up non-graphics resources.
        /// </remarks>
        public virtual void Initialize() { }

        /// <summary>
        /// Gets the update order relative to other game components. Lower values are updated first.
        /// </summary>
        /// <value>The update order.</value>
        public int UpdatePriority
        {
            get => updatePriority;
            set
            {
                if (updatePriority != value)
                {
                    var oldPriority = updatePriority;
                    updatePriority = value;
                    OnUpdatePriorityChanged(new PriorityEventArgs(oldPriority, value));
                }
            }
        }

        /// <summary>
        /// Starts application update
        /// </summary>
        public virtual void BeginUpdate() { }

        /// <summary>
        /// This method is called when this game component is updated.
        /// </summary>
        /// <param name="gameTime">The current timing.</param>
        public virtual void Update(IGameTime gameTime) { }

        /// <summary>
        /// Gets a value indicating whether the game component's Update method should be called  by <see cref="Core.SystemManager.Update"/>.
        /// </summary>
        /// <value><c>true</c> if update is enabled; otherwise, <c>false</c>.</value>
        public bool Enabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    OnEnabledChanged(new StateEventArgs(value));
                }
            }
        }


        /// <summary>
        /// Starts the drawing of a frame. This method is followed by calls to Draw and EndDraw.
        /// </summary>
        /// <returns><c>true</c> if Draw should occur, <c>false</c> otherwise</returns>
        public virtual bool BeginDraw()
        {
            return IsVisible;
        }

        /// <summary>
        /// Draws this instance.
        /// </summary>
        /// <param name="gameTime">The current timing.</param>
        public virtual void Draw(IGameTime gameTime) { }

        /// <summary>
        /// Ends the drawing of a frame. This method is preceded by calls to BeginScene and Draw.
        /// </summary>
        public virtual void EndDraw() { }

        /// <summary>
        /// Gets a value indicating whether the <see cref="IDrawable.Draw"/> method should be called by <see cref="Core.SystemManager.Draw"/>.
        /// </summary>
        /// <value><c>true</c> if this drawable component is visible; otherwise, <c>false</c>.</value>
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (isVisible != value)
                {
                    isVisible = value;
                    OnVisibilityChanged(new StateEventArgs(value));
                }
            }
        }


        /// <summary>
        /// Gets the draw order relative to other objects. <see cref="IDrawable"/> objects with a lower value are drawn first.
        /// </summary>
        /// <value>The draw order.</value>
        public int DrawPriority
        {
            get => drawPriority;
            set
            {
                if (drawPriority != value)
                {
                    var oldPriority = drawPriority;
                    drawPriority = value;
                    OnDrawPriorityChanged(new PriorityEventArgs(oldPriority, value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the way how this system will be processed in Update phase
        /// </summary>
        public ExecutionType UpdateExecutionType
        {
            get => updatExecutionType;

            set
            {
                if (updatExecutionType != value)
                {
                    var oldExecType = updatExecutionType;
                    updatExecutionType = value;
                    OnUpdateExecutionTypeChanged(new ExecutionTypeEventArgs(oldExecType, value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the way how this system will be processed in Draw phase
        /// </summary>
        public ExecutionType DrawExecutionType
        {
            get => drawExecutionType;

            set
            {
                if (drawExecutionType != value)
                {
                    var oldExecType = drawExecutionType;
                    drawExecutionType = value;
                    OnDrawExecutionTypeChanged(new ExecutionTypeEventArgs(oldExecType, value));
                }
            }
        }

        /// <summary>
        /// Loads the content.
        /// </summary>
        public virtual void LoadContent() { }

        /// <summary>
        /// Called when graphics resources need to be unloaded. Override this method to unload any game-specific graphics resources.
        /// </summary>
        public virtual void UnloadContent() { }

        /// <summary>
        /// Adds an object to be disposed automatically when <see cref="UnloadContent"/> is called. See remarks.
        /// </summary>
        /// <typeparam name="T">Type of the object to dispose</typeparam>
        /// <param name="disposable">The disposable object.</param>
        /// <returns>The disposable object.</returns>
        /// <remarks>
        /// Use this method for any content that is not loaded through the <see cref="ContentManager"/>.
        /// </remarks>
        protected T ToDisposeContent<T>(T disposable) where T : IDisposable
        {
            return contentCollector.Collect(disposable);
        }

        /// <summary>
        /// Called when <see cref="IsVisible"/> property changed and call <see cref="VisibilityChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="IsVisible"/> property</param>
        protected virtual void OnVisibilityChanged(StateEventArgs e)
        {
            VisibilityChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Called when <see cref="Enabled"/> property changed and call <see cref="EnabledChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="Enabled"/> property</param>
        protected virtual void OnEnabledChanged(StateEventArgs e)
        {
            EnabledChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Called when <see cref="UpdatePriority"/> property changed and call <see cref="UpdatePriorityChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="UpdatePriority"/> property</param>
        protected virtual void OnUpdatePriorityChanged(PriorityEventArgs e)
        {
            UpdatePriorityChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Called when <see cref="DrawPriority"/> property changed and call <see cref="DrawPriorityChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="DrawPriority"/> property</param>
        protected virtual void OnDrawPriorityChanged(PriorityEventArgs e)
        {
            DrawPriorityChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Called when <see cref="UpdateExecutionType"/> property changed and call <see cref="UpdateExecutionTypeChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="UpdateExecutionType"/> property</param>
        protected virtual void OnUpdateExecutionTypeChanged(ExecutionTypeEventArgs e)
        {
            UpdateExecutionTypeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Called when <see cref="DrawExecutionType"/> property changed and call <see cref="DrawExecutionTypeChanged"/> event
        /// </summary>
        /// <param name="e">Contains system Uid and current value of <see cref="DrawExecutionType"/> property</param>
        protected virtual void OnDrawExecutionTypeChanged(ExecutionTypeEventArgs e)
        {
            DrawExecutionTypeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Occurs when the <see cref="IUpdatable.Enabled"/> property changes.
        /// </summary>
        public event EventHandler<StateEventArgs> EnabledChanged;

        /// <summary>
        /// Occurs when the <see cref="IDrawable.IsVisible"/> property changes.
        /// </summary>
        public event EventHandler<StateEventArgs> VisibilityChanged;

        /// <summary>
        /// Occurs when the <see cref="IUpdatable.UpdatePriority"/> property changes.
        /// </summary>
        public event EventHandler<PriorityEventArgs> UpdatePriorityChanged;

        /// <summary>
        /// Occurs when the <see cref="IDrawable.DrawPriority"/> property changes.
        /// </summary>
        public event EventHandler<PriorityEventArgs> DrawPriorityChanged;

        /// <summary>
        /// Occurs when the <see cref="IUpdatable.UpdateExecutionType"/> property changes.
        /// </summary>
        public event EventHandler<ExecutionTypeEventArgs> UpdateExecutionTypeChanged;

        /// <summary>
        /// Occurs when the <see cref="IDrawable.DrawExecutionType"/> property changes.
        /// </summary>
        public event EventHandler<ExecutionTypeEventArgs> DrawExecutionTypeChanged;
    }
}
