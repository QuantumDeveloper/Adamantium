using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.ECS;
using Adamantium.Graphics.Core;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.EntityServices;
using Adamantium.UI.Events;
using Adamantium.UI.Platforms.MacOS;
using Adamantium.UI.Platforms.Windows;
using Adamantium.UI.Rendering;
using Adamantium.UI.Services;
using Adamantium.UI.Themes.FluentDarkTheme;
using Adamantium.Vulkan.Loader;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using UnhandledExceptionEventArgs = Adamantium.UI.Core.RoutedEvents.UnhandledExceptionEventArgs;
using UnhandledExceptionEventHandler = Adamantium.UI.Core.RoutedEvents.UnhandledExceptionEventHandler;

namespace Adamantium.UI;

public abstract class UIApplication : FundamentalUIComponent, IService, IUIApplication, IWindowPlatformService
{
    private readonly object applicationLocker = new object();
    
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

    // Phase 3.3b render thread (RenderThreadOptions.RenderThreadEnabled). The loop thread does Update + Record and hands the
    // recorded frame to renderThread through a bounded FULL/FREE channel pair, then returns WITHOUT waiting - so it runs the
    // NEXT frame's Update concurrently with the render thread applying + presenting this one (the anti-freeze overlap).
    //   _renderFull  loop -> render : "this frame is recorded, apply + present it" (carries the frame's AppTime).
    //   _renderFree  render -> loop : "I have consumed the packet, you may overwrite it" (backpressure). Seeded with ONE
    //                token, so exactly one packet is ever in flight - the single RenderCache packet is never overwritten
    //                while the render thread is still reading it, and the loop stays at most ~1 frame ahead.
    private Thread renderThread;
    private readonly Channel<AppTime> _renderFull = Channel.CreateBounded<AppTime>(1);
    private readonly Channel<bool> _renderFree = Channel.CreateBounded<bool>(1);

    static UIApplication()
    {
        VulkanDllMap.Register();
    }

    protected UIApplication()
    {
        Current = this;
        UIAppContext.Initialize(this, this);
        DesiredFPS = 60;
        appTime = new AppTime();
        ShutDownMode = ShutDownMode.OnMainWindowClosed;
        windowToSystem = new Dictionary<IWindow, WindowRenderService>();
        addedWindows = new List<IWindow>();
        closedWindows = new List<IWindow>();
        windowsCollection = new AdamantiumCollection<IWindow>();
        
        preciseTimer = new PreciseTimer();

        Container = new AdamantiumDependencyContainer();
        EventAggregator = Container.Resolve<IEventAggregator>();
        ApplicationBuilder.Build(Container);
        ResourceManager = CreateResourceManager();
        ThemeManager = CreateThemeManager(Container);
        UIContext =  new UIContext(Container, this);

        GraphicsDeviceService = new GraphicsDeviceService(Container.Resolve<IGraphicsDeviceFactory>(), EnableGraphicsDebug);
        Container.RegisterInstance<IGraphicsDeviceService>(GraphicsDeviceService);
        Container.RegisterSingleton<IResourceFactory, ResourceFactory>();
        Container.RegisterSingleton<IGraphicsContext, GraphicsContext>();
        GraphicsContext = Container.Resolve<IGraphicsContext>();
        EntityWorld = new EntityWorld(Container);
        RegisterBasicServices(Container);
        
        applicationLoopThread = new Thread(ApplicationLoopThread);
        Keyboard.KeyDownEvent.RegisterClassHandler<IUIComponent>(new KeyEventHandler(KeyEventHandler), true);
        
        ConfigureLogging();
    }

    private void KeyEventHandler(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.UpArrow)
        {
            var fluentDark = ThemeManager["FluentDark"];
            ThemeManager.SetTheme(fluentDark);
        }
        else if (e.Key == Key.DownArrow)
        {
            var fluentLight = ThemeManager["FluentLight"];
            ThemeManager.SetTheme(fluentLight);
        }
    }

    protected virtual IThemeManager CreateThemeManager(IDependencyContainer container)
    {
        return new ThemeManager(Container);
    }

    protected virtual IResourceManager CreateResourceManager()
    {
        var resourceManager = new ResourceManager();
        
        return resourceManager;
    }

    private void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            .WriteTo.File("logs/uilogs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public static UIApplication Current { get; private set; }

    public static readonly AdamantiumProperty EnableGraphicsDebugProperty =
        AdamantiumProperty.Register(nameof(EnableGraphicsDebugProperty), typeof(bool), typeof(UIApplication),
            new PropertyMetadata(false, GraphicsDebugChangedCallback));

    private static void GraphicsDebugChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is UIApplication { GraphicsDeviceService: not null } ui)
        {
            Log.Logger.Debug("GraphicsDebugChangedCallback called");
            ui.GraphicsDeviceService.IsInDebugMode = (bool)e.NewValue;
            ui.GraphicsDeviceService.DeviceUpdateNeeded = true;
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

    public IResourceManager ResourceManager { get; }
    public IThemeManager ThemeManager { get; private set; }
    public IDispatcher Dispatcher { get; private set; }
    public IUIContext UIContext { get; private set; }

    public void AddWindow(IWindow window)
    {
        OnWindowAdded(window);
    }

    public void RemoveWindow(IWindow window)
    {
        OnWindowRemoved(window);
    }

    public void SetActiveWindow(IWindow window)
    {
        ActiveWindow = window;
    }

    public void InactivateWindow(IWindow window)
    {
        if (ActiveWindow == window) 
            ActiveWindow = null;
    }

    public void ExecuteOnUIThread(Action action)
    {
        Dispatcher.Invoke(action);
    }

    public async Task ExecuteOnUIThreadAsync(Action action)
    {
        await Dispatcher.InvokeAsync(action);
    }

    public IReadOnlyList<IWindow> Windows => windowsCollection;
    public IWindowWorkerService GetWindowWorker(IUIContext uiContext)
    {
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                return new Win32WindowWorker(uiContext);
            case Platform.OSX:
                return new MacOSWindowWorker(uiContext);
            default:
                throw new NotSupportedException($"{Configuration.Platform} does not yet supported for windowing system");
        }
    }

    public ShutDownMode ShutDownMode { get; set; }

    public Type StartupType { get; set; }

    public IDependencyContainer Container { get; private set; }

    protected IGraphicsDeviceService GraphicsDeviceService { get; private set; }
    
    public IGraphicsContext GraphicsContext { get; private set; }
    
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
        if (!windowToSystem.TryGetValue(window, out var service)) return;

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
        Threading.Dispatcher.Initialize(UIContext);
        Dispatcher = Threading.Dispatcher.CurrentDispatcher;
        GraphicsDeviceService.IsInDebugMode = EnableGraphicsDebug;
        GraphicsDeviceService.CreateMainDevice("Adamantium Main");
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

    private void LoadThemes()
    {
        var theme = new FluentDark();
        ThemeManager.AddTheme(theme.Name, theme);
        var lightTheme = new FluentLight();
        ThemeManager.AddTheme(lightTheme.Name, lightTheme);
        ThemeManager.SetTheme(theme);
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
        if (RenderThreadOptions.RenderThreadEnabled)
        {
            _renderFree.Writer.TryWrite(true);   // seed the single in-flight token so the first record may proceed
            renderThread = new Thread(RenderThread) { IsBackground = true, Name = "AdamantiumRenderThread" };
            renderThread.Start();
        }
        applicationLoopThread.Start();
        Dispatcher.Run(cancellationTokenSource.Token);
    }

    public void Run(IWindow window)
    {
        if (IsRunning) return;
        
        MainWindow = window ?? throw new ArgumentNullException($"{nameof(window)}");

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
            if (window == null) 
                return;
            
            MainWindow = window;
            MainWindow.AttachContextAndInitialize(UIContext);
            MainWindow.Show();
        }
    }

    private void ApplicationLoopThread()
    {
        Dispatcher.UIThread = Thread.CurrentThread;

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
                        RecordRenderFrame();
                        DispatchRenderFrame(appTime);

                        UpdateAppTime(accumulatedFrameTime);
                        accumulatedFrameTime = 0;
                    }
                }
                else
                {
                    Update(appTime);
                    RecordRenderFrame();
                    DispatchRenderFrame(appTime);

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

    // Phase 3.2 Step 2b (flag-gated, docs/RENDER_THREAD_PLAN.md): record the DEVICE-FREE render packet for EVERY window
    // HERE - after the whole Update phase (all layout settled) and before any GPU Draw. This is the precondition for moving
    // the applier to a dedicated render thread (Phase 3.3): the record reads the live tree while it is quiescent; the apply
    // (in BeginDraw) consumes the packet. The default single-threaded path skips this entirely - record stays inline in
    // BeginDraw (byte-identical). ONE RenderDirty.Clear after ALL windows record, so a second window still sees the full
    // dirty set this frame (in the inline path each window's ApplyFrame clears, which a second window would race).
    private void RecordRenderFrame()
    {
        if (RenderThreadOptions.SingleThreaded || DisableRendering) return;

        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;
        if (threaded)
        {
            // Backpressure: block until the render thread has consumed (applied + presented) the PREVIOUS packet before we
            // overwrite the single RenderCache packet with this frame's record. The loop's Update already ran concurrently
            // with that render frame; this caps the loop to ~1 frame ahead.
            try { BlockingRead(_renderFree.Reader); }
            catch (OperationCanceledException) { return; }
        }

        foreach (var service in windowToSystem.Values)
            service.RecordFrame();

        // Threaded STEADY state clears the dirty set HERE, on the LOOP thread, right after the record snapshotted it - the
        // applier (render thread) consumes the frozen buffers, never RenderDirty, so the render thread must not touch it
        // (it would race the next Update's marks). NOT while a swap is settling: then RecordFrame deferred to the inline
        // path (DispatchRenderFrame's barrier), which consumes + clears RenderDirty itself - clearing here would wipe the
        // marks before that inline record reads them.
        if (threaded && !RenderDirty.IsSettlingStructural)
            RenderDirty.Clear();
    }

    // Returns whether the frame actually drew (BeginScene passed). RenderDirty.Clear is NOT done here - this method runs on
    // the render thread in the threaded-steady path, and the render thread must never touch RenderDirty (it would race the
    // loop's Update marks). The LOOP-thread callers clear instead: RecordRenderFrame (threaded steady) or DispatchRenderFrame
    // (inline decoupled / the threaded resize barrier).
    private bool ExecuteDrawSequence(AppTime appTime)
    {
        if (DisableRendering) return false;

        var drew = BeginScene();
        if (drew)
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
        return drew;
    }

    // Phase 3.3b: run the GPU frame either inline (default / decoupled-single-thread) or on the dedicated render thread.
    // Threaded, this PUBLISHES the recorded frame and returns without waiting - the loop overlaps the next Update with this
    // frame's apply + present. No render thread was spawned unless the flag was set at startup, so RenderThreadEnabled
    // without renderThread falls back to inline.
    private void DispatchRenderFrame(AppTime appTime)
    {
        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;

        // Render thread OFF: run inline on this (loop) thread. The decoupled-inline path clears RenderDirty here (its record
        // ran inline - loop-level or the BeginDraw fallback); the single-threaded default leaves the clear to ApplyFrame.
        if (!threaded)
        {
            var drewInline = ExecuteDrawSequence(appTime);
            if (!RenderThreadOptions.SingleThreaded && drewInline) RenderDirty.Clear();
            return;
        }

        // Render thread ON, but a structural swap (resize / DPI / theme) is settling: STOP-THE-WORLD barrier. The render
        // thread is idle here (RecordRenderFrame drained it via the free token) and RecordFrame deferred to the inline path,
        // so run the whole frame INLINE on this loop thread - keeping the live-tree read off the render thread while Update
        // mutates it - clear on this thread, and return the free token ourselves (no packet was handed off).
        if (RenderDirty.IsSettlingStructural)
        {
            var drewBarrier = ExecuteDrawSequence(appTime);
            if (drewBarrier) RenderDirty.Clear();
            _renderFree.Writer.TryWrite(true);
            return;
        }

        // Steady state: publish and return immediately (overlap). TryWrite always succeeds - RecordRenderFrame took the
        // single free token, so the render thread has consumed the previous packet and _renderFull is empty.
        _renderFull.Writer.TryWrite(appTime);
    }

    private void RenderThread()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            AppTime appTime;
            try { appTime = BlockingRead(_renderFull.Reader); }   // wait for the loop to publish a recorded frame
            catch (OperationCanceledException) { break; }         // shutdown
            try
            {
                ExecuteDrawSequence(appTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex));
            }
            finally
            {
                _renderFree.Writer.TryWrite(true);   // packet consumed -> the loop may overwrite it with the next frame
            }
        }
    }

    // Block the calling thread until an item arrives (or shutdown cancels the token, which throws OperationCanceledException).
    private T BlockingRead<T>(ChannelReader<T> reader)
    {
        var pending = reader.ReadAsync(cancellationTokenSource.Token);
        return pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
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
        // Apply input marshalled onto this (loop) thread BEFORE anything reads/writes the visual tree, so window input
        // (and the layout invalidation it triggers) lands on the loop thread ahead of layout instead of racing it.
        Threading.Dispatcher.CurrentDispatcher?.DrainPending();
        // Drive time-based animations once per frame (FrameTime is in seconds) before the services update/layout.
        AnimationManager.Tick(frameTime.FrameTime);
        EntityWorld.ServiceManager.Update(frameTime);
    }

    protected void Draw(AppTime frameTime)
    {
        EntityWorld.ServiceManager.Draw(frameTime);
    }

    protected virtual void OnBeforeEndScene()
    {
        
    }

    protected void EndScene()
    {
        GraphicsDeviceService.RaiseFrameFinished();
        EntityWorld.ServiceManager.Present();
    }

    public void ShutDown()
    {
        ShuttingDown?.Invoke(this, EventArgs.Empty);
        cancellationTokenSource.Cancel();   // the render thread's BlockingRead observes this (OperationCanceled) and exits
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