using Adamantium.Core;
using Adamantium.Core.Events;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.Vulkan.Core;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Game.Core
{
    /// <summary>
    /// Abstract class representing encapsulated rendering surface (control) with <see cref="GraphicsPresenter"/>
    /// </summary>
    public abstract class UniverseOutput : DisposableObject
    {
        public Guid Id { get; } 

        public IGraphicsDevice GraphicsDevice { get; private set; }
        
        protected IEventAggregator EventAggregator { get; }

        /// <summary>
        /// Contains <see cref="UniverseOutput"/> description
        /// </summary>
        public abstract UniverseOutputDescription Description { get; protected set; }

        public GameWindowType Type => (GameWindowType)Description.PresenterType;

        public OutputContext OutputContext { get; internal set; }

        public static OutputCursor DefaultCursor = OutputCursor.Arrow;
        
        /// <summary>
        /// Bounds of the <see cref="UniverseOutput"/> starting always from (0,0)
        /// </summary>
        public Rectangle ClientBounds { get; protected set; }

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="UniverseOutput"/> 
        /// </summary>
        public abstract OutputCursor Cursor { get; set; }

        /// <summary>
        /// Underlying control for rendering
        /// </summary>
        public abstract object NativeWindow { get; }

        /// <summary>
        /// Defines is <see cref="UniverseOutput"/> currently displayed
        /// </summary>
        public abstract Boolean IsVisible { get; }
        
        public abstract bool IsActive { get; } 
        
        public abstract WindowState State { get; set; }

        internal abstract bool CanHandle(OutputContext gameContext);

        internal abstract void Resize(uint width, uint height);

        internal abstract void SwitchContext(OutputContext context);
        
        private void GenerateWindowName()
        {
            Name = $"Window_{UniversePlatform.WindowId++}";
        }

        /// <summary>
        /// Initializes <see cref="UniverseOutput"/>
        /// </summary>
        protected UniverseOutput(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
            Viewport = new Viewport();
            Viewport.MaxDepth = 1.0f;
            Scissor = new Rect2D();
            GenerateWindowName();
            Id = Guid.NewGuid();
        }
        
        /// <summary>
        /// Initialize 
        /// </summary>
        /// <param name="context"></param>
        protected abstract void Initialize(OutputContext context);

        protected abstract void Initialize(
            OutputContext context, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4);

        public Rect2D Scissor { get; protected set; }

        public Viewport Viewport { get; protected set; }
        
        public GraphicsPresenter Presenter { get; protected set; }

        /// <summary>
        /// Keyboard, pointer and - while this output is the active one - gamepad input of this output.
        /// Set when the platform takes the output in; until then its input goes nowhere.
        /// </summary>
        public InputWormhole Input { get; internal set; }

        public virtual void CopyOutput(IGraphicsDevice graphicsDevice)
        {
            graphicsDevice.BlitImage(GraphicsDevice.CurrentCommandBuffer, 
                GraphicsDevice.CurrentRenderTarget,
                Presenter.GetCurrentImage());
        }

        public void DisplayContent()
        {
            if (IsUpToDate())
            {
                Presenter.Present();
            }
        }

        public void SetGraphicsDevice(IGraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
            Presenter = GraphicsPresenter.Create(graphicsDevice, Description.ToPresentationParameters());
            ClearState();
            OnDeviceSet();
        }

        protected virtual void OnDeviceSet()
        {
            
        }

        internal Boolean ResizeRequested { get; set; }

        internal Boolean UpdateRequested { get; set; }


        public Boolean IsUpToDate()
        {
            return !ResizeRequested && !UpdateRequested;
        }

        internal void ClearState()
        {
            ResizeRequested = false;
            UpdateRequested = false;
        }

        public virtual void Show()
        {
        }

        public virtual void Close()
        {
        }

        public static UniverseOutput New(IEventAggregator eventAggregator, OutputContext gameContext)
        {
            if (gameContext.ContextType == OutputContextType.RenderTargetPanel)
            {
                return new RenderTargetUniverseOutput(eventAggregator, gameContext);
            }
            else if (gameContext.ContextType == OutputContextType.Window)
            {
                return new WindowUniverseOutput(eventAggregator, gameContext);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }

        public static UniverseOutput NewWindow(IEventAggregator eventAggregator, uint width, uint height)
        {
            var wnd = new Window();
            wnd.Width = width;
            wnd.Height = height;
            return new WindowUniverseOutput(eventAggregator, new OutputContext(wnd));
        }

        internal static UniverseOutput New(
            IEventAggregator eventAggregator,
            OutputContext gameContext, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4)
        {
            if (gameContext.ContextType == OutputContextType.RenderTargetPanel)
            {
                return new RenderTargetUniverseOutput(eventAggregator, gameContext, pixelFormat, depthFormat, msaaLevel);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }

        public virtual async Task TakeScreenshotAsync(string path, ImageFileType fileType)
        {
            await Presenter?.TakeScreenshotAsync(path, fileType);
        }

        public void UpdatePresenter()
        {
            ResizePresenter();
            ClearState();
        }

        internal void ResizePresenter()
        {
            Presenter.Resize(Description);
        }

        internal void SetPresentOptions()
        {
            //Presenter.PresentInterval = Description.PresentInterval;
            //Presenter.PresentFlags = Description.PresentFlags;
        }

        /// <summary>
        /// Occurs when window got focus
        /// </summary>
        public event Action<UniverseOutput> Activated;

        /// <summary>
        /// Occurs when window lost focus
        /// </summary>
        public event Action<UniverseOutput> Deactivated;

        /// <summary>
        /// Occurs when window size has changed
        /// </summary>
        public event Action<UniverseOutputSizeChangedPayload> SizeChanged;

        /// <summary>
        /// Occurs after GraphicsPresenter finish updating (recreated or resized)
        /// </summary>
        public event Action<UniverseOutputParametersPayload> ParametersChanged;

        /// <summary>
        /// Occurs before GraphicsPresenter updated (recreated or resized)
        /// </summary>
        public event Action<UniverseOutputParametersPayload> ParametersChanging;

        /// <summary>
        /// Occurs when window is closed
        /// </summary>
        public event EventHandler<EventArgs> Closed;

        /// <summary>
        /// Occurs when window state changed
        /// </summary>
        public Action<WindowStatePayload> StateChanged;

        internal void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when GraphicsPresenter updated (recreated or resized)
        /// </summary>
        /// <param name="reason"></param>
        internal void OnWindowParametersChanging(ChangeReason reason)
        {
            ParametersChanging?.Invoke(new UniverseOutputParametersPayload(this, Description, reason));
        }

        /// <summary>
        /// Called when GraphicsPresenter updated (recreated or resized)
        /// </summary>
        /// <param name="reason"></param>
        internal void OnWindowParametersChanged(ChangeReason reason)
        {
            ParametersChanged?.Invoke(new UniverseOutputParametersPayload(this, Description, reason));
        }

        /// <summary>
        /// Called after GraphicsPresenter has been resized
        /// </summary>
        internal void OnWindowSizeChanged()
        {
            SizeChanged?.Invoke(new UniverseOutputSizeChangedPayload(this, new Size(Width, Height)));
        }

        internal void OnKeyInput(KeyboardInput args)
        {
            Input?.OnKeyboardInput(args);
        }

        internal void OnMouseInput(MouseInput args)
        {
            Input?.OnMouseInput(args);
        }

        internal void OnActivated()
        {
            Activated?.Invoke(this);
        }

        internal void OnDeactivated()
        {
            Deactivated?.Invoke(this);
        }
        
        protected void UpdateViewportAndScissor(uint width, uint height)
        {
            Viewport.Width = width;
            Viewport.Height = height;
            
            Scissor.Extent = new Extent2D();
            Scissor.Extent.Width = width;
            Scissor.Extent.Height = height;
            Scissor.Offset = new Offset2D();
        }

        /// <summary>
        /// Disposes of object resources.
        /// </summary>
        /// <param name="disposeManagedResources">If true, managed resources should be
        /// disposed of in addition to unmanaged resources.</param>
        protected override void Dispose(bool disposeManagedResources)
        {
            GraphicsDevice?.Dispose();
            base.Dispose(disposeManagedResources);
        }

        protected void RaiseSizeChangedEvent(UniverseOutputSizeChangedPayload payload)
        {
            SizeChanged?.Invoke(payload);
        }


        public virtual UInt32 Width
        {
            get => Description.Width;
            set
            {
                if (Description.Width != value)
                {
                    Description.Width = value;
                    //ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual UInt32 Height
        {
            get => Description.Height;
            set
            {
                if (Description.Height != value)
                {
                    Description.Height = value;
                    //ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Converts a point in screen coordinates into the coordinates of the surface this output draws on.
        /// </summary>
        public abstract Vector2F PointToSurface(Vector2F absolute);

        /// <summary>
        /// Pins the pointer where it stands and hides it, so a drag the game is running carries on past the edge of
        /// the screen and arrives as motion rather than as a position. Released with false, which puts the cursor back.
        /// </summary>
        public abstract void HoldPointer(bool hold, Vector2F origin);

        public virtual IntPtr Handle
        {
            get => Description.Handle;
            protected set
            {
                if (Description.Handle != value)
                {
                    Description.Handle = value;
                    UpdateRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual SurfaceFormat PixelFormat
        {
            get => Description.PixelFormat;
            set
            {
                if (Description.PixelFormat != value)
                {
                    Description.PixelFormat = value;
                    ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual DepthFormat DepthFormat
        {
            get => Description.DepthFormat;
            set
            {
                if (Description.DepthFormat != value)
                {
                    Description.DepthFormat = value;
                    ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual MSAALevel MSAALevel
        {
            get => Description.MsaaLevel;
            set
            {
                if (Description.MsaaLevel != value)
                {
                    Description.MsaaLevel = value;
                    UpdateRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual UInt32 BuffersCount
        {
            get => Description.BuffersCount;
            set
            {
                if (Description.BuffersCount != value)
                {
                    Description.BuffersCount = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        ResizeRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public virtual PresentInterval PresentInterval
        {
            get => Description.PresentInterval;
            set => Description.PresentInterval = value;
        }
    }
}
