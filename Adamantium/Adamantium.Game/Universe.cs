using System;
using System.Collections.Generic;
using System.Threading;
using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Compiler.Models;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Services;
using Serilog;

namespace Adamantium.Game;

public class Universe : PropertyChangedBase, IUniverse
{
    private readonly Dictionary<UniverseOutput, EntityService> drawSystems = [];

    private readonly DisposeCollector unloadContentCollector;
        
    private readonly UniversePlatform gamePlatform;
    private CancellationTokenSource cancellationTokenSource;
    private readonly Thread gameLoopThread;

    private double accumulatedFrameTime;
    /// <summary>
    /// Contains game time passed from game start and time from last frame
    /// </summary>
    private AppTime appTime;

    private TimeSpan totalTime;

    // The game's OWN rendering cost, kept apart from the cadence it is ticked at. See DrawTimeMs / RenderFps.
    private readonly System.Diagnostics.Stopwatch renderTimer = new();
    private double renderSeconds, renderWindow;
    private int renderedFrames;

    private readonly PreciseTimer gameTimer;

    private Double fpsTime;
    private Int32 fpsCounter;

    private readonly Dictionary<Object, OutputContext> contextsMapping;
        
    public Universe(
        UniverseMode mode,
        bool enableDebug, 
        IGraphicsDeviceService graphicsDeviceService = null, 
        IDependencyContainer container = null)
    {
        Mode = mode;

        Container = container ?? new AdamantiumDependencyContainer();
        UniverseBuilder.Build(Container);
            
        appTime = new AppTime();
        gameTimer = new PreciseTimer();
        contextsMapping = new Dictionary<Object, OutputContext>();
        IsFixedTimeStep = false;
        DesiredFPS = 60;
        Content = new ContentManager(Container);
        // Cooked artifacts (.aemf etc.) take precedence over raw source; falls back to the file system.
        Content.Resolvers.Add(new CookedContentResolver());
        Content.Resolvers.Add(new FileSystemContentResolver());
        Content.Resolvers.Add(new EffectContentResolver());
        // Model files -> SceneData (runtime parse); image files -> GPU Texture for material maps. The texture reader
        // resolves the device lazily from ServiceProvider at load time (ResourceLoaderDevice exists by then).
        Content.Readers.Add(typeof(SceneData), new SceneDataContentReader());
        Content.Readers.Add(typeof(Texture), new TextureContentReader());
            
        ModelConverter = new ModelConverter();
        unloadContentCollector = new DisposeCollector();
        ShutDownMode = ShutDownMode.OnMainWindowClosed;
            
        EventAggregator = Container.Resolve<IEventAggregator>();
        EventAggregator.GetEvent<UniverseOutputRemovedEvent>().Subscribe(OnOutputRemoved);
        EventAggregator.GetEvent<UniverseOutputCreatedEvent>().Subscribe(OnOutputCreated);
        var factory = Container.Resolve<IGraphicsDeviceFactory>();

        if (mode is UniverseMode.Standalone or UniverseMode.Primary)
        {
            GraphicsDeviceService = new GraphicsDeviceService(factory, enableDebug);
            //GraphicsDeviceService.CreateMainDevice("Game", enableDynamicRendering);
        }
        else
        {
            //GraphicsDeviceService = Container.Resolve<IGraphicsDeviceService>();
            GraphicsDeviceService = graphicsDeviceService;
        }
            
        gamePlatform = new UniversePlatform(this);
        EntityWorld = new EntityWorld(Container);
            
        Container.RegisterInstance<ModelConverter>(ModelConverter);
        Container.RegisterInstance<IContentManager>(Content);
        Container.RegisterInstance<IUniversePlatform>(gamePlatform);
        Container.RegisterInstance<IUniverse>(this);
        Container.RegisterInstance<IAdamantiumApplication>(this);
        Container.RegisterInstance<IGraphicsDeviceService>(GraphicsDeviceService);
        Container.RegisterInstance<EntityWorld>(EntityWorld);
            
        Stopped += Game_Stopped;
        gameLoopThread = new Thread(StartGameLoop);
    }
        
    protected IEventAggregator EventAggregator { get; }
        
    public EntityWorld EntityWorld { get; }
        
    public IGraphicsDeviceService GraphicsDeviceService { get; set; }

    public bool IsInitialized { get; private set; }
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
    /// Read only collection of <see cref="UniverseOutput"/>s
    /// </summary>
    public IReadOnlyList<UniverseOutput> Outputs => gamePlatform.Outputs;


    /// <summary>
    /// Main <see cref="UniverseOutput"/>
    /// </summary>
    public UniverseOutput MainOutput => gamePlatform.MainWindow;

    public bool IsFixedTimeStep { get; set; }
    public double TimeStep => 1.0d / DesiredFPS;
        
    public UInt32 DesiredFPS { get; set; }

    /// <summary>What ONE of the game's own frames costs on the CPU - recording the scene and submitting it - averaged
    /// over the last second. The GPU's own time is not in it: <c>Draw</c> records commands, it does not wait for them.
    /// </summary>
    public Double DrawTimeMs { get; private set; }

    /// <summary>Frames per second the game's RENDERING could sustain, from <see cref="DrawTimeMs"/>. Deliberately not
    /// the rate it is ticked at: a hosted game is driven once per frame of whoever hosts it, so counting ticks reports
    /// the HOST and says nothing about what the scene costs.</summary>
    public Single RenderFps { get; private set; }

    /// <summary>
    /// Condition on which game loop will be exited
    /// </summary>
    public ShutDownMode ShutDownMode { get; set; }
        
    public UniverseMode Mode { get; }
        
    public string Title { get; set; }
        
    public bool IsRunning => cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested != true;

    public void InitializeUniverse()
    {
        InitializeBeforeRun();
    }

    /// <summary>
    /// Game services which could be added to the game
    /// </summary>
    public IDependencyContainer Container { get; }
        
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
    }
        
    private void Game_Stopped(object sender, EventArgs e)
    {
        EntityWorld.Reset();
    }

    private void RemoveRenderProcessor(UniverseOutput window)
    {
        lock (drawSystems)
        {
            if (drawSystems.Remove(window, out var system))
            {
                EntityWorld.RemoveService(system);
            }
        }
    }

    /// <summary>
    /// Creates the service that draws <paramref name="window"/>; it is removed together with the output.
    /// </summary>
    public T CreateRenderService<T>(UniverseOutput window) where T : EntityService
    {
        var system = EntityWorld.CreateService<T>(EntityWorld, window);
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
    /// <param name="context">Control which will be used for creating corresponding <see cref="UniverseOutput"/> and further rendering</param>
    public void Run(object context)
    {
        if (IsRunning) return;

        var window = CreateOutputFromContext(context);
        Run(window);
    }

    public void RunOnce(AppTime time)
    {
        MakePreparations();
        Update(time);
        ExecuteDrawSequence2(time);
        FrameFinished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Run game loop on the selected control
    /// </summary>
    /// <param name="window"><see cref="UniverseOutput"/> which will be used for rendering</param>
    public void Run(UniverseOutput window)
    {
        if (IsRunning) return;
            
        gamePlatform.AddOutput(window);
        RunInternal();
    }

    private void RunInternal()
    {
        InitializeBeforeRun();
            
        //StartGameLoop();
        gameLoopThread.Start();

        if (Mode == UniverseMode.Standalone)
        {
            gamePlatform.Run(cancellationTokenSource.Token);
        }
    }

    public void Submit()
    {
        EndScene();
    }

    private void OnInitialized()
    {
        IsInitialized = true;
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    private void OnStarted()
    {
        Started?.Invoke(this, EventArgs.Empty);
    }

    public void AddOutput(UniverseOutput output)
    {
        gamePlatform.AddOutput(output);
    }

    /// <summary>
    /// Create new game window from context and add it to the list of game windows
    /// </summary>
    /// <param name="width">Initial window width</param>
    /// <param name="height">Initial window height</param>
    public UniverseOutput CreateOutput(uint width = 1280, uint height = 720)
    {
        return gamePlatform.CreateOutput(width, height);
    }

    /// <summary>
    /// Create new game window from context and add it to the list of game windows
    /// </summary>
    /// <param name="context">Window, in which Vulkan content will be rendered</param>
    public UniverseOutput CreateOutputFromContext(object context)
    {
        if (!contextsMapping.ContainsKey(context))
        {
            var gameContext = new OutputContext(context);
            var output = gamePlatform.CreateOutput(gameContext);
            contextsMapping.Add(context, gameContext);
            return output;
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
    public UniverseOutput CreateOutputFromContext(
        object context, 
        SurfaceFormat surfaceFormat, 
        DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
        MSAALevel msaaLevel = MSAALevel.None)
    {
        if (!contextsMapping.ContainsKey(context))
        {
            var window = gamePlatform.CreateOutput(context, surfaceFormat, depthFormat, msaaLevel);
            contextsMapping.Add(context, window.OutputContext);
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
        contextsMapping.Remove(context);
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

        if (!contextsMapping.Remove(oldContext, out var gameContext))
        {
            if (!contextsMapping.ContainsKey(newContext))
            {
                CreateOutputFromContext(newContext);
            }
            return;
        }
        var context = new OutputContext(newContext);
        gamePlatform.SwitchContext(gameContext, context);
        contextsMapping[newContext] = context;
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
                        ExecuteDrawSequence(appTime);

                        UpdateAppTime(accumulatedFrameTime);
                        accumulatedFrameTime = 0;
                    }
                }
                else
                {
                    var frameTime = gameTimer.GetElapsedTime();

                    MakePreparations();
                    Update(appTime);
                    ExecuteDrawSequence(appTime);

                    UpdateAppTime(frameTime);
                }
                FrameFinished?.Invoke(this, EventArgs.Empty);
            }

            OnStopped();
        }
        catch (Exception exception)
        {
            // Reaching here means the loop is over - this is the game thread's outermost frame - so this is the only
            // record that it stopped at all, let alone why.
            Log.Logger.Fatal(exception, "Game loop stopped");
        }
    }
        
    private void ExecuteDrawSequence(AppTime gameTime)
    {
        if (!gamePlatform.HasOutputs) return;
                
        if (BeginScene())
        {
            Draw(gameTime);
            EndScene();
        }
    }

    private void ExecuteDrawSequence2(AppTime gameTime)
    {
        if (!gamePlatform.HasOutputs) return;

        if (BeginScene())
        {
            renderTimer.Restart();
            Draw(gameTime);
            renderSeconds += renderTimer.Elapsed.TotalSeconds;
            renderedFrames++;
        }

        // Over a second, like the loop's own counter: a single frame is noise, and a rate recomputed every frame is
        // unreadable on screen.
        renderWindow += gameTime.FrameTime;
        if (renderWindow < 1.0) return;

        DrawTimeMs = renderedFrames > 0 ? renderSeconds * 1000.0 / renderedFrames : 0;
        RenderFps = renderSeconds > 0 ? (Single)(renderedFrames / renderSeconds) : 0;

        renderWindow = 0;
        renderSeconds = 0;
        renderedFrames = 0;
    }

    private void InitializeBeforeRun()
    {
        if (IsInitialized) return;
            
        if (Mode is UniverseMode.Standalone or UniverseMode.Primary)
        {
            GraphicsDeviceService.CreateMainDevice("");
        }

        cancellationTokenSource = new CancellationTokenSource();
            
        EntityWorld.Initialize();

        InitializeCore();
        LoadContentCore();
        OnInitialized();
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
        gamePlatform.UpdateInput(gameTime);
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
            
        //EntityWorld.ServiceManager.Submit();
        //EntityWorld.ServiceManager.Present();
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
        OutputsSettled?.Invoke(this, EventArgs.Empty);
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

    private void OnOutputCreated(UniverseOutput output)
    {
        var entity = new CameraTemplate().BuildEntity(
            null, $"Main camera for {output.Name}", Vector3.Zero, Vector3.ForwardLH, -Vector3.Up,
            output.Width, output.Height, 0.1f, 1000000.0f);
        entity.Transform.Position = new Vector3(0, 0, -20);
        EntityWorld.EntityManager.AddEntity(entity);
        output.Camera = entity.GetComponent<Camera>();
    }

    private void OnOutputRemoved(UniverseOutput output)
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
        
    private void OnStopped()
    {
        ShuttingDown?.Invoke(this, EventArgs.Empty);
        FreeGameResources();
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Load content at startup after game resources initialization
    /// </summary>
    protected virtual void LoadContent() { }

    public event EventHandler Initialized;
    public event EventHandler FrameFinished;
    public event EventHandler OutputsSettled;
    public event EventHandler<EventArgs> Started;
    public event EventHandler<EventArgs> ShuttingDown;
    public event EventHandler<EventArgs> Stopped;
    public event EventHandler Paused;
    public event EventHandler Resumed;
    public event EventHandler<EventArgs> ContentLoading;
    public event EventHandler<EventArgs> ContentUnloading;
        
}