using System;
using System.Collections.Generic;
using System.Threading;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine;
using Adamantium.Engine.Compiler.Models;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Managers;
using Adamantium.EntityFramework;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Game.Core.Input;
using Adamantium.Imaging;
using Adamantium.UI;
using Adamantium.Win32;

namespace Adamantium.Game
{
    public class Game : PropertyChangedBase, IGame
    {
        private readonly Dictionary<GameOutput, EntityService> drawSystems;

        private readonly DisposeCollector unloadContentCollector;
        
        private readonly GamePlatform gamePlatform;
        private CancellationTokenSource cancellationTokenSource;
        private readonly Thread gameLoopThread;

        private double accumulatedFrameTime;
        /// <summary>
        /// Contains game time passed from game start and time from last frame
        /// </summary>
        private AppTime appTime;

        private TimeSpan totalTime;

        private readonly PreciseTimer gameTimer;

        private Double fpsTime;
        private Int32 fpsCounter;

        private readonly Dictionary<Object, GameContext> contextsMapping;
        
        public Game(GameMode mode, bool enableDynamicRendering, IDependencyResolver resolver = null)
        {
            Mode = mode;

            EnableDynamicRendering = enableDynamicRendering;
            Resolver = resolver ?? new AdamantiumDependencyResolver();
            GameBuilder.Build(Resolver);
            
            appTime = new AppTime();
            gameTimer = new PreciseTimer();
            contextsMapping = new Dictionary<Object, GameContext>();
            IsFixedTimeStep = false;
            DesiredFPS = 60;
            
            Content = new ContentManager(Resolver);
            Content.Resolvers.Add(new FileSystemContentResolver());
            Content.Resolvers.Add(new EffectContentResolver());
            Content.Readers.Add(typeof(Entity), new ModelContentReader());
            
            ModelConverter = new ModelConverter();
            unloadContentCollector = new DisposeCollector();
            ShutDownMode = ShutDownMode.OnMainWindowClosed;
            
            EventAggregator = Resolver.Resolve<IEventAggregator>();
            EventAggregator.GetEvent<GameOutputRemovedEvent>().Subscribe(OnOutputRemoved);

            gamePlatform = GamePlatform.Create(this, Resolver);
            GraphicsDeviceService = new GraphicsDeviceService(true);
            GraphicsDeviceService.CreateMainDevice("Game", enableDynamicRendering);
            EntityWorld = new EntityWorld(Resolver);

            Resolver.RegisterInstance<ModelConverter>(ModelConverter);
            Resolver.RegisterInstance<IContentManager>(Content);
            Resolver.RegisterInstance<IGamePlatform>(gamePlatform);
            Resolver.RegisterInstance<IGame>(this);
            Resolver.RegisterInstance<IService>(this);
            Resolver.RegisterInstance<IGraphicsDeviceService>(GraphicsDeviceService);
            Resolver.RegisterInstance<EntityWorld>(EntityWorld);

            InputManager = new GameInputManager(this);
            GamePlayManager = new GamePlayManager(Resolver);
            Stopped += Game_Stopped;
            drawSystems = new Dictionary<GameOutput, EntityService>();
            gameLoopThread = new Thread(StartGameLoop);
        }
        
        protected IEventAggregator EventAggregator { get; }
        
        public EntityWorld EntityWorld { get; }
        
        public bool EnableDynamicRendering { get; }

        public GameInputManager InputManager { get; private set; }

        public ToolsManager ToolsManager { get; private set; }
        public LightManager LightManager { get; private set; }
        public CameraManager CameraManager { get; private set; }
        public GamePlayManager GamePlayManager { get; private set; }
        public IGraphicsDeviceService GraphicsDeviceService { get; set; }
        
        public bool IsPaused { get; private set; }
        
        /// <summary>
        /// Calling this method will pause running service
        /// </summary>
        public void Pause()
        {
            if (!IsPaused)
            {
                IsPaused = true;
                Paused?.Invoke(this, EventArgs.Empty);
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
                Resumed?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Read only collection of <see cref="GameOutput"/>s
        /// </summary>
        public IReadOnlyList<GameOutput> Outputs => gamePlatform.Outputs;

        /// <summary>
        /// Current focused <see cref="GameOutput"/>
        /// </summary>
        public GameOutput ActiveOutput => gamePlatform.ActiveWindow;

        /// <summary>
        /// Main <see cref="GameOutput"/>
        /// </summary>
        public GameOutput MainOutput => gamePlatform.MainWindow;

        public bool IsFixedTimeStep { get; set; }
        public double TimeStep => 1.0d / DesiredFPS;
        
        public UInt32 DesiredFPS { get; set; }
        
        /// <summary>
        /// Condition on which game loop will be exited
        /// </summary>
        public ShutDownMode ShutDownMode { get; set; }
        
        public GameMode Mode { get; }
        
        public string Title { get; set; }
        
        public bool IsRunning => cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested != true;
        
        /// <summary>
        /// Game services which could be added to the game
        /// </summary>
        public IDependencyResolver Resolver { get; }
        
        /// <summary>
        /// Represents a Content Manager, which can load all needed resources as Textures, Effects, Entity
        /// </summary>
        public ContentManager Content { get; set; }

        /// <summary>
        /// Built-in model converter to import model directly in engine
        /// </summary>
        public ModelConverter ModelConverter { get; set; }

        protected virtual void Initialize()
        {
            ToolsManager = new ToolsManager(this);
            LightManager = new LightManager(this);
            CameraManager = new CameraManager(this);
        }
        
        private void Game_Stopped(object sender, EventArgs e)
        {
            EntityWorld.Reset();
        }

        private void RemoveRenderProcessor(GameOutput window)
        {
            lock (drawSystems)
            {
                if (drawSystems.ContainsKey(window))
                {
                    EntityWorld.RemoveService(drawSystems[window]);
                    drawSystems.Remove(window);
                }
            }
        }

        public T CreateRenderService<T>(GameOutput window) where T : RenderingService
        {
            var system = EntityWorld.CreateService<T>(new object[] { EntityWorld, window });
            lock (drawSystems)
            {
                drawSystems.Add(window, system);
            }

            return system;
        }

        /// <summary>
        /// Run game loop on default control
        /// </summary>
        public void Run()
        {
            if (IsRunning) return;

            RunInternal();
        }

        /// <summary>
        /// Run game loop on the selected control
        /// </summary>
        /// <param name="context">Control which will be used for creating corresponding <see cref="GameOutput"/> and further rendering</param>
        public void Run(Object context)
        {
            if (IsRunning) return;
            
            var window = CreateOutputFromContext(context);
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

        private void OnInitialized()
        {
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        private void OnStarted()
        {
            Started?.Invoke(this, EventArgs.Empty);
        }

        public void AddOutput(GameOutput output)
        {
            gamePlatform.AddOutput(output);
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="width">Initial window width</param>
        /// <param name="height">Initial window height</param>
        public GameOutput CreateOutput(uint width = 1280, uint height = 720)
        {
            return gamePlatform.CreateOutput(width, height);
        }

        /// <summary>
        /// Create new game window from context and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which Vulkan content will be rendered</param>
        public GameOutput CreateOutputFromContext(object context)
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
        public GameOutput CreateOutputFromContext(
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
        /// <param name="elapsed">elapsed time from the last frame</param>
        protected void UpdateAppTime(double elapsed)
        {
            TimeSpan frameTimeSpan = TimeSpan.FromSeconds(appTime.FrameTime);
            if (!IsPaused)
            {
                totalTime += frameTimeSpan;
            }
            if (appTime.FramesCount > 0 && appTime.FramesCount - 1 < UInt64.MaxValue)
            {
                appTime.FramesCount++;
            }
            else
            {
                appTime.FramesCount = 1;
            }

            appTime.FrameTime = elapsed;
            appTime.TotalTime = totalTime;
            CalculateFps(ref appTime);

        }

        /// <summary>
        /// Calculates FPS count
        /// </summary>
        private void CalculateFps(ref AppTime appTime)
        {
            fpsCounter++;
            fpsTime += appTime.FrameTime;
            if (fpsTime >= 1.0d)
            {
                Console.WriteLine($"FPS = {fpsCounter}");
                appTime.Fps = (fpsCounter) / (Single)fpsTime;
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

                while (IsRunning)
                {
                    if (IsFixedTimeStep)
                    {
                        accumulatedFrameTime += gameTimer.GetElapsedTime();

                        if (accumulatedFrameTime >= TimeStep)
                        {
                            MakePreparations();
                            Update(appTime);
                            ExecuteDrawSequence();

                            UpdateAppTime(accumulatedFrameTime);
                            accumulatedFrameTime = 0;
                        }
                    }
                    else
                    {
                        var frameTime = gameTimer.GetElapsedTime();

                        MakePreparations();
                        Update(appTime);
                        ExecuteDrawSequence();

                        UpdateAppTime(frameTime);
                    }
                    
                }

                OnStopped();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
        }

        private void OnStopped()
        {
            ShuttingDown?.Invoke(this, EventArgs.Empty);
            FreeGameResources();
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteDrawSequence()
        {
            if (!gamePlatform.HasOutputs) return;
                
            if (BeginScene())
            {
                Draw(appTime);
                EndScene();
            }
        }

        private void InitializeBeforeRun()
        {
            GraphicsDeviceService.CreateMainDevice("", EnableDynamicRendering);
            cancellationTokenSource = new CancellationTokenSource();
            
            EntityWorld.Initialize();

            InitializeCore();
            LoadContentCore();
        }

        private void InitializeCore()
        {
            GraphicsDeviceService.DeviceCreated += GraphicsDeviceCreated;
            GraphicsDeviceService.DeviceDisposing += GraphicsDeviceDisposing;
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
            GraphicsDeviceService.DeviceDisposing -= GraphicsDeviceDisposing;
            unloadContentCollector.DisposeAndClear();
            ContentUnloading?.Invoke(this, e);
            UnloadContent();
            // After finish ContentUnloading event, subscribe back to DeviceDisposing event
            GraphicsDeviceService.DeviceDisposing += GraphicsDeviceDisposing;
        }

        /// <summary>
        /// Method for updating game logic
        /// </summary>
        /// <param name="gameTime">AppTime contains elapsed time, total time and FPS</param>
        protected virtual void Update(AppTime gameTime)
        {
            EntityWorld.ServiceManager.Update(gameTime);
        }

        /// <summary>
        /// Method for preparation before drawing
        /// </summary>
        protected virtual bool BeginScene()
        {
            return GraphicsDeviceService.IsReady;
        }

        /// <summary>
        /// Method for drawing operation
        /// </summary>
        /// <param name="gameTime">AppTime contains elapsed time from the last frame, total time and current FPS</param>
        protected virtual void Draw(AppTime gameTime)
        {
            EntityWorld.ServiceManager.Draw(gameTime);
        }

        /// <summary>
        /// Method for after drawing measures
        /// </summary>
        protected virtual void EndScene()
        {
            //Parallel.ForEach(gamePlatform.Windows, window => window.DisplayContent());
            foreach (var output in gamePlatform.Outputs)
            {
                output.DisplayContent();
            }
            EntityWorld.ServiceManager.OnFrameEnded();
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
            if (GraphicsDeviceService != null)
            {
                GraphicsDeviceService.DeviceCreated -= GraphicsDeviceCreated;
                GraphicsDeviceService.DeviceDisposing -= GraphicsDeviceDisposing;
            }
        }

        private void FreeGameResources()
        {
            lock (this)
            {
                contextsMapping.Clear();

                var disposableGraphicsService = GraphicsDeviceService as IDisposable;
                disposableGraphicsService?.Dispose();

                DisposeGraphicsDeviceEvents();

                gamePlatform?.Dispose();
            }
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
            RemoveRenderProcessor(output);
            if (gamePlatform.Outputs.Count == 0 && ShutDownMode == ShutDownMode.OnLastWindowClosed)
            {
                ShutDown();
            }
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

        private void UpdateCore(AppTime gameTime)
        {
            Update(gameTime);
        }

        public event EventHandler Initialized;
        public event EventHandler<EventArgs> Started;
        public event EventHandler<EventArgs> ShuttingDown;
        public event EventHandler<EventArgs> Stopped;
        public event EventHandler Paused;
        public event EventHandler Resumed;
        public event EventHandler<EventArgs> ContentLoading;
        public event EventHandler<EventArgs> ContentUnloading;
        
    }
}
