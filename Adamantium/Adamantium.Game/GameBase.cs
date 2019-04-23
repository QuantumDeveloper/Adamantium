using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Compiler.Converter;
using Adamantium.Engine.Graphics;
using Adamantium.Win32;
using SharpDX;

namespace Adamantium.Engine
{
    /// <summary>
    /// Represent base class for game execution
    /// </summary>
    public abstract class GameBase : NamedObject, IRunningService
    {
        private bool continueRendering;
        private DisposeCollector unloadContentCollector;

        private IGraphicsDeviceManager graphicsDeviceManager;
        private IGraphicsDeviceService graphicsDeviceService;
        private GamePlatform gamePlatform;

        private double accumulatedFrameTime;


        /// <summary>
        /// Contains game time passed from game start and time from last frame
        /// </summary>
        protected GameTime GameTime { get; }

        private TimeSpan totalTime;

        private PreciseTimer gameTimer;

        private Double fpsTime;
        private Int32 fpsCounter;

        private Dictionary<Object, GameContext> contextsMapping;

        /// <summary>
        /// Read only collection of <see cref="GameWindow"/>s
        /// </summary>
        public GameWindow[] Windows => gamePlatform.Windows;

        /// <summary>
        /// Current focused <see cref="GameWindow"/>
        /// </summary>
        public GameWindow ActiveWindow => gamePlatform.ActiveWindow;

        /// <summary>
        /// Main <see cref="GameWindow"/>
        /// </summary>
        public GameWindow MainWindow => gamePlatform.MainWindow;

        /// <summary>
        /// Condition on which game loop will be exited
        /// </summary>
        public ShutDownMode ShutDownMode { get; set; }

        /// <summary>
        /// Create instance of Game class
        /// </summary>
        protected GameBase()
        {
            Services = new ServiceStorage();
            GameTime = new GameTime();
            gameTimer = new PreciseTimer();
            contextsMapping = new Dictionary<Object, GameContext>();
            IsFixedTimeStep = false;
            Content = new ContentManager(Services);
            Content.Resolvers.Add(new FileSystemContentResolver());
            Content.Resolvers.Add(new EffectContentResolver());
            ModelConverter = new ModelConverter();
            unloadContentCollector = new DisposeCollector();
            SystemManager = new GameSystemManager(this);
            ShutDownMode = ShutDownMode.OnLastWindowClosed;

            gamePlatform = GamePlatform.Create(this);
            gamePlatform.WindowCreated += OnWindowCreated;
            gamePlatform.WindowRemoved += OnWindowRemoved;
            gamePlatform.WindowActivated += OnWindowActivated;
            gamePlatform.WindowDeactivated += OnWindowDeactivated;
            gamePlatform.WindowSizeChanged += OnWindowSizeChanged;
            gamePlatform.WindowParametersChanging += OnWindowParametersChanging;
            gamePlatform.WindowParametersChanged += OnWindowParametersChanged;

            Services.Add(ModelConverter);
            Services.Add<IContentManager>(Content);
            Services.Add<SystemManager>(SystemManager);
            Services.Add<IGamePlatform>(gamePlatform);
            Services.Add<IRunningService>(this);
        }

        /// <summary>
        /// Add <see cref="IDisposable"/> instance to dispose list, which will be disposed and cleared when UnloadContent will be called
        /// </summary>
        /// <param name="disposeArg">Instance which implements <see cref="IDisposable"/></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T ToDisposeContent<T>(T disposeArg) where T : IDisposable
        {
            return unloadContentCollector.Collect(disposeArg);
        }

        private void OnWindowParametersChanged(object sender, GameWindowParametersEventArgs e)
        {
            WindowParametersChanged?.Invoke(this, e);
        }

        private void OnWindowParametersChanging(object sender, GameWindowParametersEventArgs e)
        {
            WindowParametersChanging?.Invoke(this, e);
        }

        private void OnWindowSizeChanged(object sender, GameWindowSizeChangedEventArgs e)
        {
            WindowSizeChanged?.Invoke(this, e);
        }

        private void OnWindowDeactivated(object sender, GameWindowEventArgs e)
        {
            WindowDeactivated?.Invoke(this, e);

        }

        private void OnWindowActivated(object sender, GameWindowEventArgs e)
        {
            WindowActivated?.Invoke(this, e);
        }

        private void OnWindowRemoved(object sender, GameWindowEventArgs e)
        {
            WindowRemoved?.Invoke(this, e);
            if (gamePlatform.Windows.Length == 0 && ShutDownMode == ShutDownMode.OnLastWindowClosed)
            {
                ShutDown();
            }
        }

        private void OnWindowCreated(object sender, GameWindowEventArgs e)
        {
            WindowCreated?.Invoke(this, e);
        }

        /// <summary>
        /// Represents a Content Manager, which can load all needed resources as Textures, Effects, Entity
        /// </summary>
        public ContentManager Content { get; set; }

        /// <summary>
        /// Built-in model converter to import model directly in engine
        /// </summary>
        public ModelConverter ModelConverter { get; set; }

        /// <summary>
        /// Represents a D3D graphics device which do all rendering work
        /// </summary>
        public D3DGraphicsDevice GraphicsDevice => graphicsDeviceService.GraphicsDevice;

        /// <summary>
        /// Game services which could be added to the game
        /// </summary>
        public IServiceStorage Services { get; }

        /// <summary>
        /// Enables or disables fixed framerate
        /// </summary>
        public Boolean IsFixedTimeStep { get; set; }

        /// <summary>
        /// Gets or set time step for limitation of rendering frequency
        /// <remarks>value must be in seconds</remarks>
        /// </summary>
        public Double TimeStep { get; set; }

        /// <summary>
        /// Title of the game to show in the window title bar
        /// </summary>
        public String Title { get; set; }

        /// <summary>
        /// Get value dies this Game is running. Return true if game is inside game loop
        /// </summary>
        public Boolean IsRunning { get; private set; }

        /// <summary>
        /// Swicth on/off parallel execution of BeginDraw/Draw/EndDraw methods
        /// </summary>
        public Boolean EnableParallelDrawBlocksExecution { get; set; }

        /// <summary>
        /// Gets game systems manager
        /// </summary>
        public SystemManager SystemManager { get; }

        /// <summary>
        /// Get or set value whether current Game is on Pause
        /// </summary>
        public Boolean IsPaused { get; set; }

        /// <summary>
        /// Desired number of frames per second
        /// </summary>
        public Int32 DesiredFPS { get; set; }

        /// <summary>
        /// Occurs when Game context switches to another control
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowActivated;

        /// <summary>
        /// Occurs when Game context got focus
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowDeactivated;

        /// <summary>
        /// Occurs when new Game context added to the list
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowCreated;


        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowEventArgs> WindowRemoved;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> WindowParametersChanging;

        /// <summary>
        /// Occurs when one of the Game contexts removed from the list
        /// </summary>
        public event EventHandler<GameWindowParametersEventArgs> WindowParametersChanged;

        /// <summary>
        /// Occurs when Game window client size changed
        /// </summary>
        public event EventHandler<GameWindowSizeChangedEventArgs> WindowSizeChanged;

        /// <summary>
        /// Calling this method will pause running service
        /// </summary>
        public void Pause()
        {
            if (!IsPaused)
            {
                IsPaused = true;
                Paused?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// Calling this method will resume running service
        /// </summary>
        public void Resume()
        {
            if (IsPaused)
            {
                IsPaused = false;
                Resumed?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// Occurs when game started
        /// </summary>
        public event EventHandler<EventArgs> Started;
        /// <summary>
        /// Occurs when game initialized
        /// </summary>
        public event EventHandler<EventArgs> Initialized;

        /// <summary>
        /// Occurs when game is paused 
        /// </summary>
        public event EventHandler Paused;

        /// <summary>
        /// Occurs when game resumed
        /// </summary>
        public event EventHandler Resumed;

        /// <summary>
        /// Occures when game loop is finished and game is completely stop running just before <see cref="Stopped"/> event
        /// </summary>
        public event EventHandler<EventArgs> ShuttingDown;

        /// <summary>
        /// Occures when game loop is finished and game is completely stop running
        /// </summary>
        public event EventHandler<EventArgs> Stopped;

        /// <summary>
        /// Fires when service is ready to work
        /// </summary>
        public event EventHandler<EventArgs> ContentLoading;

        /// <summary>
        /// Fires when service is going to unload all resources
        /// </summary>
        public event EventHandler<EventArgs> ContentUnloading;

        /// <summary>
        /// Run game loop on default control
        /// </summary>
        public void Run()
        {

        }

        /// <summary>
        /// Run game loop on the selected control
        /// </summary>
        /// <param name="context">Control which will be used for creating corresponding <see cref="GameWindow"/> and further rendering</param>
        public void Run(Object context)
        {
            if (!IsRunning)
            {
                var window = CreateWindowFromContext(context);
                Run(window);
            }
        }

        /// <summary>
        /// Run game loop on the selected control
        /// </summary>
        /// <param name="window"><see cref="GameWindow"/> which will be used for rendering</param>
        public void Run(GameWindow window)
        {
            if (!IsRunning)
            {
                RunInternal();
            }
        }

        private void RunInternal()
        {
            continueRendering = true;
            InitializeBeforRun();
            OnInitialized();
            Task.Factory.StartNew(StartGameLoop, TaskCreationOptions.LongRunning);
        }

        internal void OnInitialized()
        {
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        internal void OnStarted()
        {
            Started?.Invoke(this, new EventArgs());
        }

        internal void OnGameWindowSizeChanged(Object sender, GameWindowSizeChangedEventArgs e)
        {
            WindowSizeChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        public GameWindow CreateWindowFromContext(object context)
        {
            if (!contextsMapping.ContainsKey(context))
            {
                var gameContext = new GameContext(context);
                contextsMapping.Add(context, gameContext);
                return gamePlatform.CreateWindow(gameContext);
            }
            throw new ArgumentException("There are already game window created on the current context");
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        public GameWindow CreateWindowFromContext(object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None)
        {
            if (!contextsMapping.ContainsKey(context))
            {
                var window = gamePlatform.CreateWindow(context, surfaceFormat, depthFormat, msaaLevel);
                return window;
            }
            throw new ArgumentException("There are already game window created on the current context");
        }

        /// <summary>
        /// Remove a game window from the list by its context
        /// </summary>
        /// <param name="context">Game window to remove</param>
        public void RemoveWindowByContext(object context)
        {
            gamePlatform.RemoveWindow(context);
        }

        /// <summary>
        /// Switching game presentation from one control to another
        /// </summary>
        /// <param name="oldContext">previous game window</param>
        /// <param name="newContext">new game window</param>
        public void SwitchContext(object oldContext, object newContext)
        {
            if (oldContext == null)
            {
                throw new ArgumentNullException(nameof(oldContext));
            }

            if (newContext == null)
            {
                throw new ArgumentNullException(nameof(newContext));
            }

            GameContext gameContext;
            if (contextsMapping.TryGetValue(oldContext, out gameContext))
            {
                gamePlatform.RemoveWindow(gameContext);
            }
            var context = new GameContext(newContext);
            gamePlatform.SwitchContext(gameContext, context);
            if (!contextsMapping.ContainsKey(newContext))
            {
                contextsMapping.Add(newContext, context);
            }
        }

        /// <summary>
        /// Updates game time for each frame
        /// </summary>
        /// <param name="elapsed"></param>
        private void UpdateGameTime(double elapsed)
        {
            TimeSpan frameTimeSpan = TimeSpan.FromSeconds(elapsed);
            if (!IsPaused)
            {
                totalTime += frameTimeSpan;
            }
            if (GameTime.FramesCount > 0 && GameTime.FramesCount - 1 < UInt64.MaxValue)
            {
                GameTime.FramesCount++;
            }
            else
            {
                GameTime.FramesCount = 1;
            }

            GameTime.Update(elapsed, totalTime);
        }

        /// <summary>
        /// Calculates FPS count
        /// </summary>
        /// <param name="elapsed"></param>
        private void CalculateFps(double elapsed)
        {
            fpsCounter++;
            fpsTime += elapsed;
            if (fpsTime >= 1.0d)
            {
                GameTime.FpsCount = (fpsCounter) / (Single)fpsTime;
                fpsCounter = 0;
                fpsTime = 0;
            }
        }

        /// <summary>
        /// Start a game loop
        /// </summary>
        private void StartGameLoop()
        {
            try
            {
                OnStarted();

                while (continueRendering)
                {
                    if (IsFixedTimeStep)
                    {
                        accumulatedFrameTime += gameTimer.GetElapsedTime();

                        if (accumulatedFrameTime >= TimeStep)
                        {
                            MakePreparations();
                            UpdateCore(GameTime);
                            ExecuteDrawBlocks();

                            UpdateGameTime(accumulatedFrameTime);
                            CalculateFps(accumulatedFrameTime);
                            accumulatedFrameTime = 0;
                        }
                    }
                    else
                    {
                        var frametime = gameTimer.GetElapsedTime();

                        MakePreparations();
                        UpdateCore(GameTime);
                        ExecuteDrawBlocks();

                        UpdateGameTime(frametime);
                        CalculateFps(frametime);
                    }
                }

                OnStopped();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + exception.TargetSite);
            }
        }

        private void OnStopped()
        {
            IsRunning = false;
            ShuttingDown?.Invoke(this, EventArgs.Empty);
            FreeGameResources();
            Stopped?.Invoke(this, new EventArgs());
        }

        private void ExecuteDrawBlocks()
        {
            if (BeginScene())
            {
                SystemManager.Draw(GameTime);
                Draw(GameTime);
                EndScene();
            }
        }

        private void InitializeBeforRun()
        {
            graphicsDeviceService = Services.Get<IGraphicsDeviceService>();
            graphicsDeviceManager = Services.Get<IGraphicsDeviceManager>();
            graphicsDeviceManager.CreateDevice();

            InitializeCore();
            LoadContentCore();

            IsRunning = true;
        }

        private void InitializeCore()
        {
            graphicsDeviceService.DeviceCreated += GraphicsDeviceCreated;
            graphicsDeviceService.DeviceDisposing += GraphicsDeviceDisposing;
            Initialize();
        }

        private void LoadContentCore()
        {
            ContentLoading?.Invoke(this, EventArgs.Empty);
            LoadContent();
        }

        private void GraphicsDeviceCreated(object sender, EventArgs e)
        {
            LoadContentCore();
        }

        private void GraphicsDeviceDisposing(object sender, EventArgs e)
        {
            //unsubscribe from Disposing event to reduce possibility of cyclic dependency and
            //as a result StackOverFlow execption
            graphicsDeviceService.DeviceDisposing -= GraphicsDeviceDisposing;
            unloadContentCollector.DisposeAndClear();
            ContentUnloading?.Invoke(this, e);
            UnloadContent();
            //After finish ContentUnloading event, subscribe back to DeviceDisposing event
            graphicsDeviceService.DeviceDisposing += GraphicsDeviceDisposing;
        }

        /// <summary>
        /// Method for initialization of all resources needed for game at startup.
        /// At this point device already initialized
        /// </summary>
        protected virtual void Initialize()
        {
            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Default;
            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
        }

        /// <summary>
        /// Finish game loop and exit the game
        /// </summary>
        public virtual void ShutDown()
        {
            continueRendering = false;
        }

        /// <summary>
        /// Load content at startup after game resources initialization
        /// </summary>
        protected virtual void LoadContent() { }

        private void UpdateCore(GameTime gameTime)
        {
            SystemManager.Update(gameTime);
            ExecuteUpdateBlock(gameTime);
        }

        private void ExecuteUpdateBlock(GameTime gameTime)
        {
            BeginUpdate();
            Update(gameTime);
        }

        /// <summary>
        /// Method calls before <see cref="Update"/> and prepare game for update
        /// </summary>
        protected virtual void BeginUpdate()
        { }

        /// <summary>
        /// Method for updating game logic
        /// </summary>
        /// <param name="gameTime">GameTime conatins elapsed time, total time and FPS</param>
        protected virtual void Update(GameTime gameTime)
        { }

        /// <summary>
        /// Method for preparation before drawing
        /// </summary>
        protected virtual bool BeginScene()
        {
            if (graphicsDeviceManager != null && !graphicsDeviceManager.BeginScene())
            {
                return false;
            }
            GraphicsDevice.SetRenderTargets(MainWindow.DepthBuffer, MainWindow.BackBuffer);
            GraphicsDevice.SetViewport(MainWindow.Viewport);
            return true;
        }

        /// <summary>
        /// Method for drawing operation
        /// </summary>
        /// <param name="gameTime">GameTime contains elapsed time from the last frame, total time and current FPS</param>
        protected virtual void Draw(GameTime gameTime)
        { }

        /// <summary>
        /// Method for after drawing measures
        /// </summary>
        protected virtual void EndScene()
        {
            //Parallel.ForEach(gamePlatform.Windows, window => window.DisplayContent());
            for (int i = 0; i < gamePlatform.Windows.Length; i++)
            {
                gamePlatform.Windows[i].DisplayContent();
            }
        }

        /// <summary>
        /// Called at the end of the game to free all game resources
        /// </summary>
        protected virtual void UnloadContent()
        {

        }

        /// <summary>
        /// Called aftre EndScene to update all devices and resources to avoid resizing issues and black screens
        /// </summary>
        protected virtual void MakePreparations()
        {
            gamePlatform.MakePreparationsForNextFrame();
        }

        private void DisposeGraphicsDeviceEvents()
        {
            if (graphicsDeviceService != null)
            {
                graphicsDeviceService.DeviceCreated -= GraphicsDeviceCreated;
                graphicsDeviceService.DeviceDisposing -= GraphicsDeviceDisposing;
            }
        }

        private void FreeGameResources()
        {
            lock (this)
            {
                contextsMapping.Clear();

                var disposableGraphicsManager = graphicsDeviceManager as IDisposable;
                disposableGraphicsManager?.Dispose();

                DisposeGraphicsDeviceEvents();

                gamePlatform?.Dispose();
            }
        }
    }
}