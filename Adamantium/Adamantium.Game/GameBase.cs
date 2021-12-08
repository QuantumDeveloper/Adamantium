using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Compiler.Converter;
using Adamantium.Engine.Compiler.Models;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Graphics;
using Adamantium.Game.Events;
using Adamantium.Imaging;
using Adamantium.Win32;

namespace Adamantium.Game
{
    /// <summary>
    /// Represent base class for game execution
    /// </summary>
    public abstract class GameBase : NamedObject, IService
    {
        private DisposeCollector unloadContentCollector;
        
        private IGraphicsDeviceService graphicsDeviceService;
        private GamePlatform gamePlatform;
        private CancellationTokenSource cancellationTokenSource;
        private Thread gameLoopThread;

        private double accumulatedFrameTime;
        private IEventAggregator eventAggregator;
        
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
        /// Read only collection of <see cref="GameOutput"/>s
        /// </summary>
        public GameOutput[] Outputs => gamePlatform.Outputs;

        /// <summary>
        /// Current focused <see cref="GameOutput"/>
        /// </summary>
        public GameOutput ActiveOutput => gamePlatform.ActiveWindow;

        /// <summary>
        /// Main <see cref="GameOutput"/>
        /// </summary>
        public GameOutput MainOutput => gamePlatform.MainWindow;

        /// <summary>
        /// Condition on which game loop will be exited
        /// </summary>
        public ShutDownMode ShutDownMode { get; set; }
        
        public GameMode Mode { get; }

        /// <summary>
        /// Create instance of Game class
        /// </summary>
        protected GameBase(GameMode mode)
        {
            Mode = mode;
            Services = AdamantiumServiceLocator.Current;
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
            ShutDownMode = ShutDownMode.OnMainWindowClosed;

            eventAggregator = Services.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<GameOutputRemovedEvent>().Subscribe(OnOutputRemoved);

            gamePlatform = GamePlatform.Create(this);

            Services.RegisterInstance<ModelConverter>(ModelConverter);
            Services.RegisterInstance<IContentManager>(Content);
            Services.RegisterInstance<SystemManager>(SystemManager);
            Services.RegisterInstance<IGamePlatform>(gamePlatform);
            Services.RegisterInstance<IService>(this);

            cancellationTokenSource = new CancellationTokenSource();
            gameLoopThread = new Thread(StartGameLoop);
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

        private void OnOutputRemoved(GameOutput output)
        {
            if (gamePlatform.Outputs.Length == 0 && ShutDownMode == ShutDownMode.OnLastWindowClosed)
            {
                ShutDown();
            }
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
        public MainGraphicsDevice MainGraphicsDevice => graphicsDeviceService.MainGraphicsDevice;

        /// <summary>
        /// Game services which could be added to the game
        /// </summary>
        public IDependencyResolver Services { get; }

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
        /// Switch on/off parallel execution of BeginDraw/Draw/EndDraw methods
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
        /// Occurs when game loop is finished and game is completely stop running just before <see cref="Stopped"/> event
        /// </summary>
        public event EventHandler<EventArgs> ShuttingDown;

        /// <summary>
        /// Occurs when game loop is finished and game is completely stop running
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
            if (IsRunning) return;

            // if (!gamePlatform.HasOutputs)
            // {
            //     throw new GameStartException("Cannot start game because there are no game outputs");
            // }
            
            RunInternal();
        }

        /// <summary>
        /// Run game loop on the selected control
        /// </summary>
        /// <param name="context">Control which will be used for creating corresponding <see cref="GameOutput"/> and further rendering</param>
        public void Run(Object context)
        {
            if (IsRunning) return;
            
            var window = CreateWindowFromContext(context);
            Run(window);
        }

        /// <summary>
        /// Run game loop on the selected control
        /// </summary>
        /// <param name="window"><see cref="GameOutput"/> which will be used for rendering</param>
        public void Run(GameOutput window)
        {
            if (IsRunning) return;
            
            gamePlatform.AddOutput(window);
            RunInternal();
        }

        private void RunInternal()
        {
            InitializeBeforeRun();
            OnInitialized();
            
            //StartGameLoop();
            gameLoopThread.Start();

            if (Mode == GameMode.Standalone)
            {
                gamePlatform.Run(cancellationTokenSource.Token);
            }
        }

        internal void OnInitialized()
        {
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        internal void OnStarted()
        {
            Started?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="width">Initial window width</param>
        /// <param name="height">Initial window height</param>
        public GameOutput CreateWindow(uint width = 1280, uint height = 720)
        {
            return gamePlatform.CreateOutput(width, height);
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which Vulkan content will be rendered</param>
        public GameOutput CreateWindowFromContext(object context)
        {
            if (!contextsMapping.ContainsKey(context))
            {
                var gameContext = new GameContext(context);
                contextsMapping.Add(context, gameContext);
                return gamePlatform.CreateOutput(gameContext);
            }
            throw new ArgumentException("There are already game window created on the current context");
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which Vulkan content will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        public GameOutput CreateWindowFromContext(
            object context, 
            SurfaceFormat surfaceFormat, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            MSAALevel msaaLevel = MSAALevel.None)
        {
            if (!contextsMapping.ContainsKey(context))
            {
                var window = gamePlatform.CreateOutput(context, surfaceFormat, depthFormat, msaaLevel);
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
            gamePlatform.RemoveOutput(context);
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
                gamePlatform.RemoveOutput(gameContext);
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
                Console.WriteLine($"FPS = {GameTime.FpsCount}");
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

                while (!cancellationTokenSource.IsCancellationRequested)
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
                        var frameTime = gameTimer.GetElapsedTime();

                        MakePreparations();
                        UpdateCore(GameTime);
                        ExecuteDrawBlocks();

                        UpdateGameTime(frameTime);
                        CalculateFps(frameTime);
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

        private void InitializeBeforeRun()
        {
            graphicsDeviceService = Services.Resolve<IGraphicsDeviceService>();
            graphicsDeviceService.CreateMainDevice();

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
            // unsubscribe from Disposing event to reduce possibility of cyclic dependency and
            // as a result StackOverFlow exception
            graphicsDeviceService.DeviceDisposing -= GraphicsDeviceDisposing;
            unloadContentCollector.DisposeAndClear();
            ContentUnloading?.Invoke(this, e);
            UnloadContent();
            // After finish ContentUnloading event, subscribe back to DeviceDisposing event
            graphicsDeviceService.DeviceDisposing += GraphicsDeviceDisposing;
        }

        /// <summary>
        /// Method for initialization of all resources needed for game at startup.
        /// At this point device already initialized
        /// </summary>
        protected virtual void Initialize()
        {
            //GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Default;
            //GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
        }

        /// <summary>
        /// Finish game loop and exit the game
        /// </summary>
        public virtual void ShutDown()
        {
            cancellationTokenSource?.Cancel();
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
        /// <param name="gameTime">GameTime contains elapsed time, total time and FPS</param>
        protected virtual void Update(GameTime gameTime)
        { }

        /// <summary>
        /// Method for preparation before drawing
        /// </summary>
        protected virtual bool BeginScene()
        {
            return graphicsDeviceService.IsInitialized;
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
            for (int i = 0; i < gamePlatform.Outputs.Length; i++)
            {
                gamePlatform.Outputs[i].DisplayContent();
            }
        }

        /// <summary>
        /// Called at the end of the game to free all game resources
        /// </summary>
        protected virtual void UnloadContent()
        {

        }

        /// <summary>
        /// Called after EndScene to update all devices and resources to avoid resizing issues and black screens
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

                var disposableGraphicsService = graphicsDeviceService as IDisposable;
                disposableGraphicsService?.Dispose();

                DisposeGraphicsDeviceEvents();

                gamePlatform?.Dispose();
            }
        }
    }
}