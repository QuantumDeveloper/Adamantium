using System;
using System.Diagnostics;
using System.Windows.Input;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.Win32;
using MouseEventArgs = Adamantium.UI.Input.MouseEventArgs;

namespace Adamantium.Engine
{
    /// <summary>
    /// Abstract class representing encapsulated rendering surface (control) with <see cref="GraphicsPresenter"/>
    /// </summary>
    public abstract class GameWindow : DisposableObject
    {
        internal int WindowId = 1;

        internal GraphicsPresenter Presenter { get; set; }

        public Texture2DDescription GetDescription()
        {
            return Description.ToTexture2DDescription();
        }

        /// <summary>
        /// Contains <see cref="GameWindow"/> description
        /// </summary>
        protected abstract GameWindowDescription Description { get; set; }

        public GameWindowType Type => (GameWindowType)Description.PresenterType;

        public GameContext GameContext { get; internal set; }

        public static GameWindowCursor DefaultCursor = GameWindowCursor.Arrow;


        /// <summary>
        /// Bounds of the <see cref="GameWindow"/> starting always from (0,0)
        /// </summary>
        public abstract Rectangle ClientBounds { get; }

        /// <summary>
        /// Cursor type that will be displayed when mouse cursor will enter <see cref="GameWindow"/> 
        /// </summary>
        public abstract GameWindowCursor Cursor { get; set; }

        /// <summary>
        /// Underlying control for rendering
        /// </summary>
        public abstract object NativeWindow { get; }

        /// <summary>
        /// Defines is <see cref="GameWindow"/> currently displayed
        /// </summary>
        public abstract Boolean IsVisible { get; set; }

        internal abstract bool CanHandle(GameContext gameContext);

        internal abstract void Resize(int width, int height);

        internal abstract void SwitchContext(GameContext context);

        /// <summary>
        /// Initializes <see cref="GameWindow"/>
        /// </summary>
        protected GameWindow()
        {
            GenerateWindowName();
        }


        /// <summary>
        /// Initialize 
        /// </summary>
        /// <param name="context"></param>
        protected abstract void Initialize(GameContext context);

        protected abstract void Initialize(GameContext context, SurfaceFormat pixelFormat,
           DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.X4);

        public RenderTarget2D BackBuffer => Presenter.BackBuffer;

        public DepthStencilBuffer DepthBuffer => Presenter.DepthBuffer;

        public ViewportF Viewport => Presenter.Viewport;


        internal void DisposePresenter()
        {
            Presenter?.Dispose();
            Presenter = null;
        }

        protected String GeneratePresenterName()
        {
            return "Presenter " + WindowId;
        }

        public void DisplayContent()
        {
            if (IsUpToDate())
            {
                Presenter.Present();
            }
        }

        private void GenerateWindowName()
        {
            Name = $"Window_{GamePlatform.WindowId++}";
        }

        internal virtual GraphicsPresenter CreatePresenter(GraphicsDevice device)
        {
            return null;
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

        internal virtual void Show()
        { }

        internal static GameWindow New(GameContext gameContext)
        {
            if (gameContext.ContextType == GameContextType.RenderTargetPanel)
            {
                return new GameWindowAdamantium(gameContext);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }


        internal static GameWindow New(GameContext gameContext, SurfaceFormat pixelFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.X4)
        {
            if (gameContext.ContextType == GameContextType.RenderTargetPanel)
            {
                return new GameWindowAdamantium(gameContext, pixelFormat, depthFormat, msaaLevel);
            }
            throw new NotSupportedException(gameContext.ContextType + " game context is not currently supported");
        }

        public virtual void TakeScreenShot(string path, ImageFileType fileType)
        {
            Presenter?.TakeScreenshot(path, fileType);
        }

        internal void ResizePresenter()
        {
            Presenter?.Resize(Description.Width, Description.Height, Description.BuffersCount, Description.PixelFormat,
               Description.DepthFormat, Description.Flags);
        }

        internal void SetPresentOptions()
        {
            Presenter.PresentInterval = Description.PresentInterval;
            Presenter.PresentFlags = Description.PresentFlags;
        }

        /// <summary>
        /// Occurs when window got focus
        /// </summary>
        public event EventHandler<EventArgs> Activated;

        /// <summary>
        /// Occurs when window lost focus
        /// </summary>
        public event EventHandler<EventArgs> Deactivated;

        /// <summary>
        /// Occurs when window size has changed
        /// </summary>
        public event EventHandler<EventArgs> SizeChanged;

        /// <summary>
        /// Occurs when window size or position has changed
        /// </summary>
        public event EventHandler<GameWindowBoundsChangedEventArgs> BoundsChanged;

        /// <summary>
        /// Occurs after GraphicsPresenter finish updating (recreated or resized)
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> ParametersChanged;

        /// <summary>
        /// Occurs before GraphicsPresenter updated (recreated or resized)
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> ParametersChanging;

        /// <summary>
        /// Occurs when key was pressed
        /// </summary>
        public event EventHandler<KeyboardInputEventArgs> KeyDown;

        /// <summary>
        /// Occurs when key was released
        /// </summary>
        public event EventHandler<KeyboardInputEventArgs> KeyUp;

        /// <summary>
        /// Occurs when mouse button was pressed
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseDown;

        /// <summary>
        /// Occurs when mouse button was released
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseUp;

        /// <summary>
        /// Occurs when mouse wheel was scrolled
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseWheel;

        /// <summary>
        /// Occurs when physical mouse position changed
        /// </summary>
        public event EventHandler<MouseInputEventArgs> MouseDelta;

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
            ParametersChanging?.Invoke(this, new GameWindowParametersEventArgs(this, reason));
        }

        /// <summary>
        /// Called when GraphicsPresenter updated (recreated or resized)
        /// </summary>
        /// <param name="reason"></param>
        internal void OnWindowParametersChanged(ChangeReason reason)
        {
            ParametersChanged?.Invoke(this, new GameWindowParametersEventArgs(this, reason));
        }

        /// <summary>
        /// Called after GraphicsPresenter has been resized
        /// </summary>
        internal void OnWindowSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called after GraphicsPresenter has been resized
        /// </summary>
        internal void OnWindowBoundsChanged()
        {
            BoundsChanged?.Invoke(this, new GameWindowBoundsChangedEventArgs(this, ClientBounds));
        }

        internal void OnKeyDown(KeyboardInputEventArgs args)
        {
            KeyDown?.Invoke(this, args);
        }

        internal void OnKeyUp(KeyboardInputEventArgs args)
        {
            KeyUp?.Invoke(this, args);
        }

        internal void OnMouseDown(MouseInputEventArgs args)
        {
            MouseDown?.Invoke(this, args);
        }

        internal void OnMouseUp(MouseInputEventArgs args)
        {
            MouseUp?.Invoke(this, args);
        }

        internal void OnMouseWheel(MouseInputEventArgs args)
        {
            MouseWheel?.Invoke(this, args);
        }

        internal void OnMouseDelta(MouseInputEventArgs delta)
        {
            MouseDelta?.Invoke(this, delta);
        }

        internal void OnActivated()
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }

        internal void OnDeactivated()
        {
            Deactivated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disposes of object resources.
        /// </summary>
        /// <param name="disposeManagedResources">If true, managed resources should be
        /// disposed of in addition to unmanaged resources.</param>
        protected override void Dispose(bool disposeManagedResources)
        {
            Presenter?.Dispose();
            Presenter = null;
            base.Dispose(disposeManagedResources);
        }


        public virtual Int32 Width
        {
            get => Description.Width;
            protected set
            {
                if (Description.Width != value)
                {
                    Description.Width = value;
                    ResizeRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual Int32 Height
        {
            get => Description.Height;
            protected set
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
            get { return Description.PixelFormat; }
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
            get { return Description.DepthFormat; }
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
            get { return Description.MSAALevel; }
            set
            {
                if (Description.MSAALevel != value)
                {
                    Description.MSAALevel = value;
                    UpdateRequested = true;
                    RaisePropertyChanged();
                }
            }
        }

        public virtual SwapEffect PresenterSwapEffect
        {
            get { return Description.SwapEffect; }
            set
            {
                if (Description.SwapEffect != value)
                {
                    Description.SwapEffect = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }


        public virtual Boolean IsWindowed
        {
            get { return Description.IsWindowed; }
            set
            {
                if (Description.IsWindowed != value)
                {
                    Description.IsWindowed = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }


        public virtual Rational RefreshRate
        {
            get { return Description.RefreshRate; }
            set
            {
                if (Description.RefreshRate != value)
                {
                    Description.RefreshRate = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public virtual Int32 BuffersCount
        {
            get { return Description.BuffersCount; }
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

        public virtual SwapEffect SwapEffect
        {
            get { return Description.SwapEffect; }
            set
            {
                if (Description.SwapEffect != value)
                {
                    Description.SwapEffect = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public virtual Usage Usage
        {
            get { return Description.Usage; }
            set
            {
                if (Description.Usage != value)
                {
                    Description.Usage = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public virtual Scaling Scaling
        {
            get { return Description.Scaling; }
            set
            {
                if (Description.Scaling != value)
                {
                    Description.Scaling = value;
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        public virtual AlphaMode AlphaMode
        {
            get => Description.AlphaMode;
            set
            {
                if (SetProperty(Description.AlphaMode, value))
                {
                    if (Description.PresenterType == PresenterType.Swapchain)
                    {
                        UpdateRequested = true;
                    }
                }
            }
        }

        public virtual SwapChainFlags Flags
        {
            get { return Description.Flags; }
            set
            {
                if (Description.Flags != value)
                {
                    Description.Flags = value;
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
            get { return Description.PresentInterval; }
            set { Description.PresentInterval = value; }
        }

        public virtual PresentFlags PresentFlags
        {
            get { return Description.PresentFlags; }
            set { Description.PresentFlags = value; }
        }
    }
}
