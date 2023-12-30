using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using Adamantium.UI.Resources;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Services;
using Adamantium.UI.Threading;
using AdamantiumVulkan;
using Serilog;
using UnhandledExceptionEventArgs = Adamantium.UI.RoutedEvents.UnhandledExceptionEventArgs;
using UnhandledExceptionEventHandler = Adamantium.UI.RoutedEvents.UnhandledExceptionEventHandler;

namespace Adamantium.UI;

public abstract class UIApplication : AdamantiumComponent, IService, IUIApplication, IWindowPlatformService
{
    private object applicationLocker = new object();
    
    private Dictionary<IWindow, WindowRenderService> windowToSystem;
    
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
        windowToSystem = new Dictionary<IWindow, WindowRenderService>();
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

    public Type StartupType { get; set; }

    public AdamantiumDependencyContainer Container { get; private set; }

    protected IGraphicsDeviceService GraphicsDeviceService { get; private set; }
    
    protected IEventAggregator EventAggregator { get; private set; }
    
    public EntityWorld EntityWorld { get; private set; }

    public bool IsRunning => cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested != true;
    
    public bool IsInitialized { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsFixedTimeStep { get; set; }
    public double TimeStep => 1.0d / DesiredFPS;
    public uint DesiredFPS { get; set; }
    
    public bool DisableRendering { get; set; }

    internal MouseDevice MouseDevice => MouseDevice.CurrentDevice;
    internal KeyboardDevice KeyboardDevice => KeyboardDevice.CurrentDevice;

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        MainWindow = null;
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
            window.InvalidateRender(true);
        }
        GraphicsDeviceService.ChangeOrCreateMainDevice("Adamantium Main", true);
        foreach (var window in Windows)
        {
            CreateWindowService(window);
        }
        EntityWorld.ForceUpdate();
        Log.Logger.Debug("======Finish recreating sequence======");
    }

    private void CreateWindowService(IWindow window)
    {
        var windowService = EntityWorld.CreateService<WindowRenderService>(EntityWorld, window);
        windowToSystem.Add(window, windowService);
        var entity = new Entity();
        entity.AddComponent(window);
        EntityWorld.EntityManager.AddEntity(entity);
        EntityWorld.ForceUpdate();
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
        windowToSystem.Remove(window);
        windowsCollection.Remove(window);
        EntityWorld.RemoveService(service);
        EntityWorld.ForceUpdate();

        if (window == MainWindow)
        {
            MainWindow = null;
        }
    }

    private void Initialize()
    {
        if (IsInitialized) return;
        
        cancellationTokenSource = new CancellationTokenSource();
        Dispatcher.Initialize();
        GraphicsDeviceService.IsInDebugMode = EnableGraphicsDebug;
        GraphicsDeviceService.CreateMainDevice("Adamantium Main", true);
        ThemeManager = new ThemeManager(Container);
        LoadResources();
        LoadThemes();
        SubscribeToEvents();
        
        EntityWorld.Initialize();
        OnInitialize();
        RegisterServices(Container);
        IsInitialized = true;
        
        if (MainWindow != null)
        {
            OnWindowCreated(MainWindow);
        }
    }

    protected virtual void OnInitialize()
    {
    }

    private void LoadResources()
    {
        var resourceDictionaries = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.IsSubclassOf(typeof(ResourceDictionary))).ToList();
        foreach (var @class in resourceDictionaries)
        {
            var resource = (IResourceDictionary)Activator.CreateInstance(@class);
            ResourceRepository.AddResourceDictionary(resource);
        }

        var styles = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.IsSubclassOf(typeof(StyleSet))).ToList();
        foreach (var @class in styles)
        {
            var resource = (IStyleSet)Activator.CreateInstance(@class);
            ResourceRepository.AddStyleSet(resource);
        }
    }

    private void LoadThemes()
    {
        var themes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => x.IsSubclassOf(typeof(Theme))).ToList();
        foreach (var @class in themes)
        {
            var theme = (Theme)Activator.CreateInstance(@class);
            ThemeManager.AddTheme(theme.Name, theme);
        }
    }

    private void SubscribeToEvents()
    {
        EventAggregator.GetEvent<WindowCreatedEvent>().Subscribe(OnWindowCreated);
        EventAggregator.GetEvent<WindowClosedEvent>().Subscribe(OnWindowClosed);
        EventAggregator.GetEvent<WindowActivatedEvent>().Subscribe(OnWindowActivated);
        EventAggregator.GetEvent<WindowDeactivatedEvent>().Subscribe(OnWindowDeactivated);
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
        containerRegistry.RegisterSingleton<IThemeManager>(ThemeManager);
    }
    
    protected virtual void OnWindowCreated(IWindow wnd)
    {
        var t = Stopwatch.StartNew();
        OnWindowAdded(wnd);
        t.Stop();
        Log.Logger.Information($"Service created in {t.ElapsedMilliseconds} ms");
    }

    protected virtual void OnWindowClosed(IWindow wnd)
    {
        OnWindowRemoved(wnd);
    }

    protected void OnWindowActivated(IWindow obj)
    {
        ActiveWindow = obj;
    }
    
    protected void OnWindowDeactivated(IWindow obj)
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

    public void RunOnce(AppTime time)
    {
        
    }

    private void OnStartupInternal()
    {
        Started?.Invoke(this, EventArgs.Empty);
        OnStartup();
    }

    protected virtual void OnStartup()
    {
        if (StartupType != null && typeof(IWindow).IsAssignableFrom(StartupType))
        {
            var window = (IWindow)Activator.CreateInstance(StartupType);
            MainWindow = window;
            window?.Show();
        }
    }

    private void ApplicationLoopThread()
    {
        Dispatcher.CurrentDispatcher.UIThread = Thread.CurrentThread;

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var frameTime = preciseTimer.GetElapsedTime();
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
    }

    private void ExecuteDrawSequence(AppTime appTime)
    {
        if (DisableRendering) return;

        if (BeginScene())
        {
            try
            {
                Draw(appTime);
                OnBeforeEndScene();
            }
            finally
            {
                EndScene();
            }
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
            Console.WriteLine($"App FPS = {fpsCounter}");
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

    protected virtual void OnBeforeEndScene()
    {
        
    }

    // protected virtual void Submit()
    // {
    //     
    // }

    protected void EndScene()
    {
        GraphicsDeviceService.RaiseFrameFinished();
        EntityWorld.ServiceManager.Present();
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