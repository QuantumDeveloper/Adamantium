using System;
using System.Collections.ObjectModel;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Events;
using Adamantium.Game.GameInput;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI;
using AdamantiumVulkan.Core;

namespace Adamantium.Game
{
    /// <summary>
    /// Abstract class representing encapsulated rendering surface (control) with <see cref="GraphicsPresenter"/>
    /// </summary>
    public abstract class GameOutput : DisposableObject
    {
        internal int OutputId = 1;

        public GraphicsDevice GraphicsDevice { get; private set; }

        /// <summary>
        /// Contains <see cref="GameOutput"/> description
        /// </summary>
        public abstract GameWindowDescription Description { get; protected set; }

        public GameWindowType Type => (GameWindowType)Description.PresenterType;

        public GameContext GameContext { get; internal set; }

        public static GameWindowCursor DefaultCursor = GameWindowCursor.Arrow;
        
        /// <summary>
        /// Bounds of the <see cref="GameOutput"/> starting always from (0,0)
        /// </summary>
        public Rectangle ClientBounds { get; protected set; }

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="GameOutput"/> 
        /// </summary>
        public abstract GameWindowCursor Cursor { get; set; }

        /// <summary>
        /// Underlying control for rendering
        /// </summary>
        public abstract object NativeWindow { get; }

        /// <summary>
        /// Defines is <see cref="GameOutput"/> currently displayed
        /// </summary>
        public abstract Boolean IsVisible { get; }
        
        public abstract bool IsActive { get; } 

        internal abstract bool CanHandle(GameContext gameContext);

        internal abstract void Resize(uint width, uint height);

        internal abstract void SwitchContext(GameContext context);
        
        private void GenerateWindowName()
        {
            Name = $"Window_{GamePlatform.WindowId++}";
        }

        /// <summary>
        /// Initializes <see cref="GameOutput"/>
        /// </summary>
        protected GameOutput()
        {
            Viewport = new Viewport();
            Viewport.MaxDepth = 1.0f;
            Scissor = new Rect2D();
            GenerateWindowName();
        }
        
        /// <summary>
        /// Initialize 
        /// </summary>
        /// <param name="context"></param>
        protected abstract void Initialize(GameContext context);

        protected abstract void Initialize(
            GameContext context, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4);

        public Rect2D Scissor { get; protected set; }

        public Viewport Viewport { get; protected set; }

        public void DisplayContent()
        {
            if (IsUpToDate())
            {
                GraphicsDevice.Present();
            }
        }

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
            ClearState();
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

        internal static GameOutput New(GameContext gameContext)
        {
            if (gameContext.ContextType == GameContextType.RenderTargetPanel)
            {
                return new RenderTargetGameOutput(gameContext);
            }
            else if (gameContext.ContextType == GameContextType.Window)
            {
                return new AdamantiumGameOutput(gameContext);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }

        internal static GameOutput NewWindow(uint width, uint height)
        {
            var wnd = Window.New();
            wnd.Width = width;
            wnd.Height = height;
            return new AdamantiumGameOutput(new GameContext(wnd));
        }


        internal static GameOutput New(
            GameContext gameContext, 
            SurfaceFormat pixelFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.X4)
        {
            if (gameContext.ContextType == GameContextType.RenderTargetPanel)
            {
                return new RenderTargetGameOutput(gameContext, pixelFormat, depthFormat, msaaLevel);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }

        public virtual void TakeScreenshot(string path, ImageFileType fileType)
        {
            GraphicsDevice?.TakeScreenshot(path, fileType);
        }

        public void UpdatePresenter()
        {
            ResizePresenter();
            ClearState();
        }

        internal void ResizePresenter()
        {
            GraphicsDevice.ResizePresenter(
                Description.Width, 
                Description.Height, 
                Description.BuffersCount,
                Description.PixelFormat,
                Description.DepthFormat);
        }

        internal void SetPresentOptions()
        {
            //Presenter.PresentInterval = Description.PresentInterval;
            //Presenter.PresentFlags = Description.PresentFlags;
        }

        /// <summary>
        /// Occurs when window got focus
        /// </summary>
        public event Action<GameOutput> Activated;

        /// <summary>
        /// Occurs when window lost focus
        /// </summary>
        public event Action<GameOutput> Deactivated;

        /// <summary>
        /// Occurs when window size has changed
        /// </summary>
        public event Action<GameOutputSizeChangedPayload> SizeChanged;

        /// <summary>
        /// Occurs after GraphicsPresenter finish updating (recreated or resized)
        /// </summary>
        public event Action<GameOutputParametersPayload> ParametersChanged;

        /// <summary>
        /// Occurs before GraphicsPresenter updated (recreated or resized)
        /// </summary>
        public event Action<GameOutputParametersPayload> ParametersChanging;

        /// <summary>
        /// Occurs when key was pressed
        /// </summary>
        public Action<KeyboardInput> KeyInput;

        /// <summary>
        /// Occurs when mouse button was released
        /// </summary>
        public Action<MouseInput> MouseInput;

        /// <summary>
        /// Occurs when window is closed
        /// </summary>
        public event EventHandler<EventArgs> Closed;

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
            ParametersChanging?.Invoke(new GameOutputParametersPayload(this, reason));
        }

        /// <summary>
        /// Called when GraphicsPresenter updated (recreated or resized)
        /// </summary>
        /// <param name="reason"></param>
        internal void OnWindowParametersChanged(ChangeReason reason)
        {
            ParametersChanged?.Invoke(new GameOutputParametersPayload(this, reason));
        }

        /// <summary>
        /// Called after GraphicsPresenter has been resized
        /// </summary>
        internal void OnWindowSizeChanged()
        {
            SizeChanged?.Invoke(new GameOutputSizeChangedPayload(this, new Size(Width, Height)));
        }

        internal void OnKeyInput(KeyboardInput args)
        {
            KeyInput?.Invoke(args);
        }

        internal void OnMouseInput(MouseInput args)
        {
            MouseInput?.Invoke(args);
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


        public virtual UInt32 Width
        {
            get => Description.Width;
            set
            {
                if (Description.Width != value)
                {
                    Description.Width = value;
                    ResizeRequested = true;
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
                    ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

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
