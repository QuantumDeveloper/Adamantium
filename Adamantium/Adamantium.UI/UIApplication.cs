using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Controls;
using Adamantium.UI.Events;
using Adamantium.UI.Input;
using Adamantium.UI.Processors;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Threading;
using AdamantiumVulkan;
using Serilog;
using UnhandledExceptionEventArgs = Adamantium.UI.RoutedEvents.UnhandledExceptionEventArgs;
using UnhandledExceptionEventHandler = Adamantium.UI.RoutedEvents.UnhandledExceptionEventHandler;

namespace Adamantium.UI;

public abstract class UIApplication : AdamantiumComponent, IService, IUIApplication
{
    private object applicationLocker = new object();
    
    private Dictionary<IWindow, WindowService> windowToSystem;
    
    private double accumulatedFrameTime;
    private TimeSpan totalTime;
    private PreciseTimer preciseTimer;
    private Double fpsTime;
    private Int32 fpsCounter;
    private AppTime appTime;
        
    private IWindow mainWindow;
    private AdamantiumCollection<IWindow> windowsCollection;
    private List<IWindow> addedWindows;
    private List<IWindow> closedWindows;
    private bool firstWindowAdded;
    private Thread applicationLoopThread;
    private CancellationTokenSource cancellationTokenSource;

    static UIApplication()
    {
        VulkanDllMap.Register();
        ApplicationBuilder.Build(AdamantiumDependencyContainer.Current);
    }

    protected UIApplication()
    {
        Current = this;
        DesiredFPS = 60;
        appTime = new AppTime();
        ShutDownMode = ShutDownMode.OnMainWindowClosed;
        windowToSystem = new Dictionary<IWindow, WindowService>();
        addedWindows = new List<IWindow>();
        closedWindows = new List<IWindow>();
        windowsCollection = new AdamantiumCollection<IWindow>();
        
        preciseTimer = new PreciseTimer();
        
        Container = AdamantiumDependencyContainer.Current;
        EventAggregator = Container.Resolve<IEventAggregator>();

        GraphicsDeviceService = new GraphicsDeviceService(EnableGraphicsDebug);
        
        EntityWorld = new EntityWorld(Container);
        RegisterBasicServices(Container);
        
        applicationLoopThread = new Thread(ApplicationLoopThread);
        
        ConfigureLogging();
    }

    private void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/uilogs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public static UIApplication Current { get; private set; }

    public static readonly AdamantiumProperty EnableGraphicsDebugProperty =
        AdamantiumProperty.Register(nameof(EnableGraphicsDebugProperty), typeof(bool), typeof(UIApplication),
            new PropertyMetadata(true, GraphicsDebugChangedCallback));

    private static void GraphicsDebugChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is UIApplication ui)
        {
            if (ui.GraphicsDeviceService != null)
            {
                Log.Logger.Debug("GraphicsDebugChangedCallback called");
                ui.GraphicsDeviceService.IsInDebugMode = (bool)e.NewValue;
                ui.GraphicsDeviceService.DeviceUpdateNeeded = true;
            }
        }
    }
    
    public bool EnableGraphicsDebug
    {
        get => GetValue<bool>(EnableGraphicsDebugProperty);
        set => SetValue(EnableGraphicsDebugProperty, value);
    }

    public IWindow MainWindow
    {
        get => mainWindow;
        set
        {
            if (mainWindow != null)
            {
                mainWindow.Closed -= MainWindow_Closed;
            }
            mainWindow = value;
            if (mainWindow != null)
            {
                mainWindow.Closed += MainWindow_Closed;
            }
        }
    }
    
    public IWindow ActiveWindow { get; private set; }

    public IThemeManager ThemeManager { get; private set; }

    public IReadOnlyList<IWindow> Windows => windowsCollection;

    public ShutDownMode ShutDownMode { get; set; }

    public Uri StartupUri { get; set; }

    public AdamantiumDependencyContainer Container { get; private set; }

    protected IGraphicsDeviceService GraphicsDeviceService { get; private set; }
    
    protected IEventAggregator EventAggregator { get; private set; }
    
    public EntityWorld EntityWorld { get; private set; }

    public bool IsRunning => cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested != true;
    public bool IsPaused { get; private set; }
    public bool IsFixedTimeStep { get; set; }
    public double TimeStep => 1.0d / DesiredFPS;
    public uint DesiredFPS { get; set; }
    
    public bool DisableRendering { get; set; }

    internal MouseDevice MouseDevice => MouseDevice.CurrentDevice;
    internal KeyboardDevice KeyboardDevice => KeyboardDevice.CurrentDevice;

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        MainWindow.Closed -= MainWindow_Closed;
        MainWindow = null;
    }
    
    protected virtual void OnWindowCreated(IWindow wnd)
    {
        // lock (applicationLocker)
        // {
        //     addedWindows.Add(wnd);
        // }
        var t = Stopwatch.StartNew();
        OnWindowAdded(wnd);
        t.Stop();
        Log.Logger.Information($"Service created in {t.ElapsedMilliseconds} ms");
    }

    protected virtual void OnWindowClosed(IWindow wnd)
    {
        // lock (applicationLocker)
        // {
        //     closedWindows.Add(wnd);
        // }
        OnWindowRemoved(wnd);
    }

    private void RecreateDevicesAndServices()
    {
        Log.Logger.Debug("======Starting recreating sequence======");
        EntityWorld.RemoveAllServices();
        EntityWorld.RemoveAllEntities();
        EntityWorld.ForceUpdate();
        windowToSystem.Clear();
        foreach (var window in Windows)
        {
            window.InvalidateRender();
        }
        GraphicsDeviceService.ChangeOrCreateDevice("Adamantium Main", true);
        foreach (var window in Windows)
        {
            CreateWindowService(window);
        }
        EntityWorld.ForceUpdate();
        Log.Logger.Debug("======Finish recreating sequence======");
    }

    private void CreateWindowService(IWindow window)
    {
        var windowService = EntityWorld.CreateService<WindowService>(EntityWorld, window);
        var entity = new Entity();
        entity.AddComponent(window);
        EntityWorld.AddEntity(entity);
        EntityWorld.ForceUpdate();

        windowToSystem.Add(window, windowService);
    }
    
    private void OnWindowAdded(IWindow window)
    {
        CreateWindowService(window);
        
        windowsCollection.Add(window);

        if (!firstWindowAdded)
        {
            firstWindowAdded = true;
        }
    }

    private void OnWindowRemoved(IWindow window)
    {
        if (!windowToSystem.ContainsKey(window)) return;
        
        var service = windowToSystem[window];
        service.UnloadContent();
        EntityWorld.RemoveService(service);
        windowToSystem.Remove(window);
        windowsCollection.Remove(window);

        if (window == MainWindow)
        {
            MainWindow = null;
        }
    }

    private void Initialize()
    {
        cancellationTokenSource = new CancellationTokenSource();
        Dispatcher.Initialize();
        GraphicsDeviceService.IsInDebugMode = EnableGraphicsDebug;
        GraphicsDeviceService.CreateMainDevice("Adamantium Main", true);
        ThemeManager = new ThemeManager(Container);
        SubscribeToEvents();
        
        EntityWorld.Initialize();
        OnInitialize();
        RegisterServices(Container);

        if (MainWindow != null)
        {
            OnWindowCreated(MainWindow);
        }
    }

    protected virtual void OnInitialize()
    {
        
    }

    private void SubscribeToEvents()
    {
        EventAggregator.GetEvent<WindowCreatedEvent>().Subscribe(OnWindowCreated, ThreadOption.UIThread);
        EventAggregator.GetEvent<WindowClosedEvent>().Subscribe(OnWindowClosed, ThreadOption.UIThread);
        EventAggregator.GetEvent<WindowActivatedEvent>().Subscribe(OnWindowActivated, ThreadOption.UIThread);
        EventAggregator.GetEvent<WindowDeactivatedEvent>().Subscribe(OnWindowDeactivated, ThreadOption.UIThread);
    }

    private void RegisterBasicServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance<IService>(this);
        containerRegistry.RegisterInstance<IUIApplication>(this);
        containerRegistry.RegisterInstance<EntityWorld>(EntityWorld);
        containerRegistry.RegisterInstance<IGraphicsDeviceService>(GraphicsDeviceService);
    }

    protected virtual void RegisterServices(IContainerRegistry containerRegistry)
    {
        
    }

    private void OnWindowActivated(IWindow obj)
    {
        ActiveWindow = obj;
    }
    
    private void OnWindowDeactivated(IWindow obj)
    {
        if (ActiveWindow == obj) ActiveWindow = null;
    }

    public virtual void Run()
    {
        if (IsRunning) return;

        Initialize();
        OnStartupInternal();
        applicationLoopThread.Start();
        Dispatcher.CurrentDispatcher.Run(cancellationTokenSource.Token);
    }

    public void Run(IWindow window)
    {
        if (IsRunning) return;
        
        Dispatcher.Initialize();

        MainWindow = window ?? throw new ArgumentNullException($"{nameof(window)}");
        MainWindow.Show();
        
        Run();
    }

    public void Run(object context)
    {
        if (context is IWindow wnd)
        {
            Run(wnd);
        }
        else
        {
            throw new ArgumentException($"{nameof(context)} should be of type IWindow, but currently it is of type {context.GetType()}");
        }
    }

    private void OnStartupInternal()
    {
        Started?.Invoke(this, EventArgs.Empty);
        OnStartup();
    }

    protected virtual void OnStartup()
    {
        
    }

    private int cycle = 0;
    private void ApplicationLoopThread()
    {
        Dispatcher.CurrentDispatcher.UIThread = Thread.CurrentThread;

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                //Log.Logger.Information($"Current cycle: {cycle}");
                var frameTime = preciseTimer.GetElapsedTime();
                //ProcessPendingWindows();
                if (IsFixedTimeStep)
                {
                    accumulatedFrameTime += frameTime;

                    if (accumulatedFrameTime >= TimeStep)
                    {
                        Update(appTime);
                        ExecuteDrawSequence(appTime);

                        UpdateAppTime(accumulatedFrameTime);
                        accumulatedFrameTime = 0;
                    }
                }
                else
                {
                    Update(appTime);
                    ExecuteDrawSequence(appTime);
                    UpdateAppTime(frameTime);
                }

                OnCycleFinishedInternal();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex));
            }

            cycle++;
        }
    }

    private void OnCycleFinishedInternal()
    {
        CheckExitConditions();
        if (GraphicsDeviceService.DeviceUpdateNeeded)
        {
            RecreateDevicesAndServices();
        }
        CycleFinished?.Invoke(this, EventArgs.Empty);
        OnCycleFinished();
    }

    protected virtual void OnCycleFinished()
    {
        
    }

    private void ExecuteDrawSequence(AppTime appTime)
    {
        if (DisableRendering) return;

        if (BeginScene())
        {
            Draw(appTime);
            EndScene();
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
    /// <param name="elapsed"></param>
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

    private void ProcessPendingWindows()
    {
        lock (applicationLocker)
        {
            for (int i = 0; i < closedWindows.Count; ++i)
            {
                OnWindowRemoved(closedWindows[i]);
            }
            closedWindows.Clear();

            for (int i = 0; i < addedWindows.Count; ++i)
            {
                OnWindowAdded(addedWindows[i]);
            }
            addedWindows.Clear();
        }
    }

    protected void CheckExitConditions()
    {
        // Solving an issue with early closing on the renderCycle
        if (ShutDownMode != ShutDownMode.OnExplicitShutDown && !firstWindowAdded) return;

        switch (ShutDownMode)
        {
            case ShutDownMode.OnMainWindowClosed when MainWindow == null:
            case ShutDownMode.OnLastWindowClosed when Windows.Count == 0:
                ShutDown();
                break;
        }
    }

    protected virtual bool BeginScene()
    {
        return GraphicsDeviceService.IsReady;
    }

    protected void Update(AppTime frameTime)
    {
        EntityWorld.ServiceManager.Update(frameTime);
    }

    protected void Draw(AppTime frameTime)
    {
        EntityWorld.ServiceManager.Draw(frameTime);
    }

    protected void EndScene()
    {
        EntityWorld.ServiceManager.DisplayContent();
    }

    public void ShutDown()
    {
        ShuttingDown?.Invoke(this, EventArgs.Empty);
        cancellationTokenSource.Cancel();
        ContentUnloading?.Invoke(this, EventArgs.Empty);
        FreeResources();
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    private void FreeResources()
    {
        if (GraphicsDeviceService is IDisposable disposableDevice)
        {
            disposableDevice?.Dispose();
        }
    }

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

    public event EventHandler<EventArgs> Started;
    public event EventHandler<EventArgs> ShuttingDown;
    public event EventHandler<EventArgs> Stopped;
    public event EventHandler Paused;
    public event EventHandler Resumed;
    public event EventHandler<EventArgs> ContentLoading;
    public event EventHandler<EventArgs> ContentUnloading;
    public event EventHandler CycleFinished;
    public event UnhandledExceptionEventHandler UnhandledException;
}