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
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Rendering;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Platforms.Windows;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.EntityServices;
using Adamantium.UI.Events;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Navigation;
using Adamantium.UI.Navigation;
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
    private bool firstWindowAdded;
    private Thread applicationLoopThread;
    private CancellationTokenSource cancellationTokenSource;

    // Phase 3.3 render thread (RenderThreadOptions.RenderThreadEnabled). The loop thread does Update + Record and hands the
    // recorded frame over WITHOUT waiting for it, so the next Update overlaps the render thread's apply + present.
    //
    // The frames themselves travel as DOUBLE-BUFFERED packets inside each window's RenderCache (a queue of deltas the applier
    // drains); this channel is only the WAKE-UP. It is bounded to ONE token and written with TryWrite, so a loop that gets
    // ahead by several frames does not pile up signals - the render thread wakes once and drains every packet published since.
    //
    // No backpressure token any more: the loop never blocks on the render (that lock-step was the 3.3 scaffold's flaw - the
    // loop ran at the render's pace, which is precisely the freeze it is meant to remove). Instead it stops RECORDING once
    // MaxFramesInFlight frames are unrendered - the marks simply stay in RenderDirty and fold into the next record - so the
    // loop keeps updating, animating and handling input at full speed while the render catches up.
    private Thread renderThread;
    // Serializes a whole render frame against window teardown / app shutdown. The render thread holds it around each
    // ExecuteDrawSequence; OnWindowRemoved / ShutDown take it so a device is never disposed while a frame is in flight.
    private readonly object _renderGate = new object();
    // OnExplicitShutDown makes ShutDown a public call that can arrive from any thread at any time - guard re-entry.
    private volatile bool _isShuttingDown;
    private int _framesInFlight;
    private AppTime _renderAppTime;   // the newest recorded frame's time; the render thread draws with the latest
    private const int MaxFramesInFlight = 2;

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
        
        ConfigureLogging();
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

    /// <summary>The presentation policy every window follows unless it states its own. Set once for the application -
    /// tear-free and paced for a shipped app, unthrottled while measuring or for an engine viewport. A window overrides
    /// it through <see cref="IWindow.PresentPolicy"/>; changing it here does not retroactively rebuild swapchains that
    /// already exist, so set it at startup.</summary>
    public Graphics.Core.Presentation.PresentPolicy PresentPolicy { get; set; } =
        Graphics.Core.Presentation.PresentPolicy.Adaptive;

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

    public Adamantium.Navigation.INavigationService Navigation { get; private set; }

    /// <summary>A window has been created and wants to be part of the application. QUEUED rather than registered here:
    /// this is called from the PUMP thread (the platform worker, as the OS window comes up), while the LOOP thread is
    /// walking the very collections it would join. The loop takes it at the start of the next frame.
    /// <para>Measured before the queue was used: restoring a saved layout opens several windows at once and the frame
    /// record threw "collection was modified".</para></summary>
    public void AddWindow(IWindow window)
    {
        lock (applicationLocker)
        {
            addedWindows.Add(window);
        }

        LoopSignal.Request();   // the next frame is what registers it - do not wait for the idle timeout
    }

    /// <summary>A window has gone. Handled RIGHT HERE, never queued: the OS surface dies with the window, and this is
    /// what parks the render thread (<c>_renderGate</c> in <see cref="OnWindowRemoved"/>) while its device resources
    /// are torn down. Deferred by even one frame, the render thread draws into a swapchain whose surface is already
    /// gone - measured: SurfaceLostKHR, then a pipeline barrier on a VK_NULL_HANDLE image, then an access violation.
    /// <para>The asymmetry with <see cref="AddWindow"/> is the point: arriving is only a bookkeeping change the loop
    /// must not see half-done, while LEAVING is a teardown that must finish before the window does.</para></summary>
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

        // Park the render thread at a frame boundary while this window's GPU resources are torn down: it draws EVERY window
        // on ONE thread, so disposing a window's device/swapchain from here (loop or pump thread) while it submits would AV.
        // The gate lets the current frame finish, holds the next off until the teardown + service removal are done, then the
        // render thread resumes and keeps drawing the OTHER windows. This is NOT a shutdown - the thread lives on. (App
        // shutdown - last/main window per ShutDownMode, or an explicit ShutDown - stops the thread instead; see ShutDown.)
        lock (_renderGate)
        {
            service.UnloadContent();
            windowToSystem.Remove(window);
            windowsCollection.Remove(window);
            EntityWorld.RemoveService(service);
        }
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
        // Before ANY window: on a cache cold for this driver this compiles every shader in throwaway child processes,
        // where the driver's intermittent crash costs a restart instead of the application. Returns at once when done.
        ShaderPrecompiler.EnsureCompiled(GraphicsDeviceService.ResourceLoaderDevice as GraphicsDevice);
        LoadThemes();
        SubscribeToEvents();
        
        EntityWorld.Initialize();
        OnInitialize();
        RegisterServices(Container);
        _visualRenderer = Container.Resolve<IVisualRenderer>();   // the DI singleton; its render device is created lazily, on first snapshot
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
        // Off-screen visual->texture renderer (UWP RenderTargetBitmap analog). Singleton, resolved on first use (after the
        // main device is up) - it then creates its own dedicated render device. Foundation for the drag-drop ghost and
        // VisualBrush/DrawingBrush bakes. An app can register its own before base to win.
        if (!containerRegistry.IsRegistered<IVisualRenderer>())
            containerRegistry.RegisterSingleton<IVisualRenderer, VisualRenderer>();

        // Drag ghost - the floating, click-through, per-pixel-alpha window that follows the cursor during a drag. Platform
        // impl; Windows = layered window + UpdateLayeredWindow. One reusable ghost (Show/Hide reuse the window).
        if (!containerRegistry.IsRegistered<IDragGhost>() && OperatingSystem.IsWindows())
            containerRegistry.RegisterSingleton<IDragGhost, Win32DragGhost>();

        // OS drag-drop bridge: a native drop target on every window (files/text from other apps) + the opt-in drag-out
        // of our own payloads (DragDrop.AllowExternalDrag). Windows = OLE; other platforms leave it unregistered and
        // only the in-app drag-drop runs.
        if (!containerRegistry.IsRegistered<INativeDragDrop>() && OperatingSystem.IsWindows())
            containerRegistry.RegisterSingleton<INativeDragDrop, WindowsDragDrop>();

        RegisterNavigationServices(containerRegistry);
    }

    // Batteries-included navigation (docs/NAVIGATION_PLAN.md). Each default is guarded, so an app can register its own
    // (view locator / nav service / window shell / region adapters) BEFORE calling base and win.
    private void RegisterNavigationServices(IContainerRegistry containerRegistry)
    {
        if (!containerRegistry.IsRegistered<IDependencyResolver>())
            containerRegistry.RegisterInstance<IDependencyResolver>(Container);
        if (!containerRegistry.IsRegistered<IViewLocator>())
            containerRegistry.RegisterSingleton<IViewLocator, ViewLocator>();
        if (!containerRegistry.IsRegistered<IWindowShellRegistry>())
            containerRegistry.RegisterSingleton<IWindowShellRegistry, WindowShellRegistry>();
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.IRegionManager>())
            containerRegistry.RegisterSingleton<Adamantium.Navigation.IRegionManager, Adamantium.Navigation.RegionManager>();
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.IWindowNavigationBackend>())
            containerRegistry.RegisterSingleton<Adamantium.Navigation.IWindowNavigationBackend, WindowNavigationBackend>();
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.INavigationService>())
            containerRegistry.RegisterSingleton<Adamantium.Navigation.INavigationService, Adamantium.Navigation.NavigationService>();

        if (!containerRegistry.IsRegistered<RegionAdapterMappings>())
        {
            var viewLocator = Container.Resolve<IViewLocator>();
            var mappings = new RegionAdapterMappings();
            mappings.Register<ContentControl>(new ContentControlRegionAdapter(viewLocator));
            mappings.Register<Selector>(new SelectorRegionAdapter(viewLocator));
            mappings.Register<ItemsControl>(new ItemsControlRegionAdapter(viewLocator));
            // Registered for the AREA specifically. A PaneGroup is a Selector and the Selector adapter already covers
            // it - one strip of tabs. What an area adds is the choice of PLACE, which a strip does not have.
            mappings.Register<DockingArea>(new DockingAreaRegionAdapter(viewLocator));
            containerRegistry.RegisterInstance<RegionAdapterMappings>(mappings);
        }

        // Dialogs (NAVIGATION_PLAN.md phase 2): the host registry seeded with the in-window OverlayDialogHost (Default ->
        // Overlay), plus the UI-free DialogService. A window-modal host registers alongside later.
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.IDialogHostRegistry>())
        {
            var dialogHosts = new Adamantium.Navigation.DialogHostRegistry();
            var overlayHost = new OverlayDialogHost(this, Container.Resolve<IViewLocator>());
            dialogHosts.Register(overlayHost.Kind, overlayHost);
            var windowHost = new WindowDialogHost(this, Container.Resolve<IViewLocator>(), Container.Resolve<IWindowShellRegistry>());
            dialogHosts.Register(windowHost.Kind, windowHost);
            containerRegistry.RegisterInstance<Adamantium.Navigation.IDialogHostRegistry>(dialogHosts);
        }
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.IDialogService>())
            containerRegistry.RegisterSingleton<Adamantium.Navigation.IDialogService, Adamantium.Navigation.DialogService>();

        // Overlays (floating in-window OverlayWindows) - a service of their own, distinct from dialogs.
        if (!containerRegistry.IsRegistered<Adamantium.Navigation.IOverlayService>())
        {
            var overlayService = new OverlayService(this, Container.Resolve<IViewLocator>(),
                Container.Resolve<Adamantium.Core.DependencyInjection.IDependencyResolver>());
            containerRegistry.RegisterInstance<Adamantium.Navigation.IOverlayService>(overlayService);
        }

        Navigation = Container.Resolve<Adamantium.Navigation.INavigationService>();
    }
    
    protected virtual void OnWindowCreated(IWindow wnd)
    {
        var t = Stopwatch.StartNew();
        OnWindowAdded(wnd);
        t.Stop();
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
            renderThread = new Thread(RenderThread) { IsBackground = true, Name = "AdamantiumRenderThread" };
            // Publish it BEFORE starting: BeginDraw checks it to refuse the inline RECORD on this thread (see
            // WindowRenderService - the record reads the live tree and would race the loop's own record).
            RenderThreadOptions.RenderThread = renderThread;
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
                // Nothing to do -> this BLOCKS on the loop pipe until something wants a frame. Everything below therefore runs
                // only because there is work: the frame does not begin until the loop is woken.
                WaitForWork();

                // Windows join and leave HERE, on the loop thread, before anything walks them. They are asked for from
                // the pump thread, which is why this is a queue and not a direct call.
                ProcessPendingWindows();

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

    /// <summary>Cap on how often the loop UPDATES while something is actually moving (an animation, an inertial scroll).
    /// Independent of the presented frame rate, which the render thread owns - that is the whole point of the split.</summary>
    public static uint UpdateRateHz { get; set; } = 120;

    /// <summary>Longest the loop will ever block on an empty pipe. Purely a safety net: every source that needs a frame writes
    /// to the pipe (see <see cref="LoopSignal"/>), so this only bounds the damage of one nobody thought of - a late frame
    /// rather than a frozen window.</summary>
    private const int IdleWakeMs = 250;

    private long _loopFrameStart;

    // The off-screen visual renderer (drag-drop ghost, live snapshots, VisualBrush bakes). Resolved once from the container
    // after service registration; its two queue drains are pumped from the frame loop below - RECORD on the loop thread,
    // DRAW on the render thread (see VisualRenderer for why the halves split across the two threads).
    private IVisualRenderer _visualRenderer;

    // The loop is BOTH paced and event-driven, and it needs both.
    //
    // PACE first: never run more often than UpdateRateHz. Waking on the pipe alone is not enough, because the loop feeds the
    // pipe ITSELF: the animation heartbeat marks its target's geometry dirty every tick, and a dirty mark is (rightly) a wake.
    // With only the pipe to gate on, a running animation meant the token was already queued by the frame that had just
    // finished - the loop never blocked at all, spun at ~400 000 empty frames a second, and published a packet to the render
    // thread on every one of them, drowning it.
    //
    // THEN block on the pipe, but only when there is genuinely nothing to do: nothing already dirty, and nothing animating.
    // Input, window events, layout invalidations, queued bindings and render-dirty marks all land in that one pipe, so an idle
    // or minimized window wakes for exactly nothing and the thread costs zero. Animations are the one thing that is TIME-driven
    // rather than event-driven - nothing "happens" to wake them, they simply need the next frame - so while one runs, the pace
    // above is what schedules the loop.
    private void WaitForWork()
    {
        var target = 1000.0 / Math.Max(1, UpdateRateHz);
        var elapsed = Stopwatch.GetElapsedTime(_loopFrameStart).TotalMilliseconds;
        var remaining = target - elapsed;
        if (remaining >= 1.0) Thread.Sleep((int)remaining);

        // EVERY stage, not just the content: the popups and the adorners keep their own marks now, and a frame owed to
        // one of them is owed by the loop all the same.
        if (!RenderDirty.AnyHasWork && !AnimationManager.HasActiveAnimations)
            LoopSignal.Wait(IdleWakeMs, cancellationTokenSource.Token);

        _loopFrameStart = Stopwatch.GetTimestamp();
    }

    private void OnCycleFinishedInternal()
    {
        // Live off-screen snapshots, stage 1 (RECORD): read the live subtree and build its cache HERE, on the loop thread,
        // once the frame is fully settled - Update, Record and the render Dispatch are all done, so the live tree is
        // quiescent and the render thread is consuming recorded packets, never the live components a read-only record walks.
        // Device-free; the GPU draw is stage 2, on the render thread (see ExecuteDrawSequence). A no-op when nothing pending.
        _visualRenderer?.RecordPendingSnapshots();

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
        _recordedThisFrame = false;

        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;

        // The render is MaxFramesInFlight frames behind: skip the record instead of blocking the loop on it. Nothing is lost -
        // RenderDirty is not cleared below, so this frame's marks stay pending and the next record folds them in (its packet is
        // simply a slightly bigger delta). The loop keeps running Update, animations and input at full speed, which is the whole
        // point of the split; without this cap the loop would instead race ahead publishing packets the render can't consume.
        if (threaded && Volatile.Read(ref _framesInFlight) >= MaxFramesInFlight) return;

        // EVERY window must have actually recorded before the dirty set may be cleared. A window whose renderer is not up yet
        // (at startup it never is, until the swapchain exists) records NOTHING - and clearing regardless threw away marks that
        // were never recorded. Nothing re-marks an already-clean component, so whatever the layout pass had built by then was
        // simply never drawn: that is why the first tab's content came up blank while every later tab was fine.
        // A SNAPSHOT, not the live collection: recording a frame runs managed code, and a window opened while it runs
        // (a restored layout puts several up at once) would otherwise break the walk mid-way.
        // Deliberately NOT under _renderGate: that gate is held by the RENDER thread for the length of a frame, so
        // taking it here - on the loop thread, every frame - serialises the two threads and the application stops
        // drawing entirely.
        var recordedAll = true;
        var services = new WindowRenderService[windowToSystem.Count];
        windowToSystem.Values.CopyTo(services, 0);

        foreach (var service in services)
            recordedAll &= service.RecordFrame();
        _recordedThisFrame = recordedAll;

        // Threaded: clear the dirty set HERE, on the LOOP thread, right after the record snapshotted it into the packets. The
        // applier (the render thread) consumes only those packets, never RenderDirty - it must not touch it at all, or it would
        // race the next Update's marks. Marks NOT recorded stay pending and fold into the next frame's packet.
        if (threaded && recordedAll) RenderDirty.Clear();
    }

    private bool _recordedThisFrame;

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
                RuntimeStats.PresentedFrames++;   // the honest frame rate once the render runs on its own thread
            }
        }

        // Live off-screen snapshots, stage 2 (DRAW): the GPU half. This runs on whichever thread owns the device this frame -
        // the render thread when threaded (under _renderGate), the loop thread inline - right after the window's own frame,
        // so the shared VkDevice is never touched by two threads at once. The record (stage 1) already built the cache off
        // the device on the loop thread; here we only submit + read back. A no-op when nothing pending.
        _visualRenderer?.DrawPendingSnapshots();

        return drew;
    }

    // Phase 3.3b: run the GPU frame either inline (default / decoupled-single-thread) or on the dedicated render thread.
    // Threaded, this PUBLISHES the recorded frame and returns without waiting - the loop overlaps the next Update with this
    // frame's apply + present. No render thread was spawned unless the flag was set at startup, so RenderThreadEnabled
    // without renderThread falls back to inline.
    private void DispatchRenderFrame(AppTime appTime)
    {
        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;

        // Render thread OFF: run inline on this (loop) thread. Clear RenderDirty ONCE here, after ExecuteDrawSequence has
        // recorded+applied EVERY window - not per-window in ApplyFrame, which (RenderDirty being one global set) let the
        // first window wipe it before a second window recorded, leaving the second window's content never re-recorded.
        if (!threaded)
        {
            var drewInline = ExecuteDrawSequence(appTime);
            if (drewInline) RenderDirty.Clear();
            return;
        }

        // Threaded: the loop NEVER draws. The device belongs to the render thread alone - no second frame path, so nothing to
        // synchronise, no barrier, no rendezvous. (The 3.3 scaffold ran settling swap frames INLINE on this thread, which is
        // why it needed to park the render thread first; that inline path was also the deadlock and the "two writers" race.
        // What it was really compensating for - a packet drawn against a stale projection while the presenter resizes - is now
        // carried BY the packet: the recorder captures the projection with the frame, and the applier uses that, never the
        // live window.)
        //
        // The record is already published into the window caches' packet queues, so there is nothing to hand over and nothing
        // to wait for. The render thread runs at ITS OWN pace and picks the packets up on its next frame.
        if (!_recordedThisFrame) return;
        _renderAppTime = appTime;
        Interlocked.Increment(ref _framesInFlight);
    }

    // The render thread's own frame loop. It does NOT wait for the update loop: it draws and presents at its own pace, taking
    // whatever the loop has published by then - packets if there are any, otherwise nothing, in which case the applier finds an
    // empty queue, classifies the frame Clean and REPLAYS the retained op stream (~1 ms even for a 60k-unit scene). That is what
    // makes the presented frame rate independent of Update: a 6-fps layout+record loop no longer means a 6-fps window, it just
    // means the content it shows advances 6 times a second while the compositor keeps presenting at its own rate.
    //
    // (This is the piece the 3.3 scaffold was missing: it woke the render thread once per published frame, so the render was
    // literally paced by the loop it was supposed to be decoupled from.)
    private void RenderThread()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            // Serialize the whole frame against window teardown / shutdown. A window being removed (or ShutDown) takes this
            // gate, so it waits for the in-flight frame to finish and then holds the next off while it disposes the device -
            // the render thread never runs mid-teardown (0xC0000005 in EndCommandBuffer/Submit otherwise). Re-check the token
            // INSIDE the gate: ShutDown flags cancellation then joins, so no new frame may start once shutdown has begun.
            lock (_renderGate)
            {
                if (cancellationTokenSource.IsCancellationRequested) break;
                Interlocked.Exchange(ref _framesInFlight, 0);   // whatever was published is about to be drained by the apply
                try
                {
                    ExecuteDrawSequence(_renderAppTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex));
                }
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

    // Only ARRIVALS are queued - see RemoveWindow for why a departure may not be. Under the lock the queue is emptied
    // into a local first: registering a window runs application code, and a handler that opens another window would
    // otherwise deadlock on the very lock it is holding.
    private void ProcessPendingWindows()
    {
        IWindow[] arrived;
        lock (applicationLocker)
        {
            if (addedWindows.Count == 0) return;

            arrived = addedWindows.ToArray();
            addedWindows.Clear();
        }

        foreach (var window in arrived) 
            OnWindowAdded(window);
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

    /// <summary>A frame slower than this (~20 fps) is treated as a STALL - a build/JIT hitch or a post-idle wake - not as
    /// real animation time, so the animation doesn't burn its duration during it. Healthy 30-120 fps frames are well under.</summary>
    private const double StallFrameSeconds = 1.0 / 20.0;

    /// <summary>How far the animation heartbeat advances THROUGH a stall frame - a sliver, so it near-pauses instead of
    /// jumping, yet still creeps forward if the low frame rate is sustained rather than a one-off hitch.</summary>
    private const double StallAnimationStep = 0.004;

    protected void Update(AppTime frameTime)
    {
        // Apply input marshalled onto this (loop) thread BEFORE anything reads/writes the visual tree, so window input
        // (and the layout invalidation it triggers) lands on the loop thread ahead of layout instead of racing it.
        Threading.Dispatcher.CurrentDispatcher?.DrainPending();
        // Drive time-based animations before the services update/layout. A HEALTHY frame advances by its real delta. A STALL
        // frame - a cold first-visit build (a tab's first measure/arrange/bake), a JIT hitch, or a wake after a long idle -
        // has a huge real delta the render can NOT present as smooth intermediate frames; advancing the animation by that
        // (even a clamped chunk) BURNS its duration during the stall, so it appears to jump straight ahead (the first-visit
        // "snap"). Advance a stall by only a sliver, so the animation effectively PAUSES through it and plays over the frames
        // actually presented. The sliver (not zero) keeps it progressing under a genuinely sustained low frame rate.
        var animationStep = frameTime.FrameTime < StallFrameSeconds ? frameTime.FrameTime : StallAnimationStep;
        AnimationManager.Tick(animationStep);
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
        // Idempotent: with OnExplicitShutDown this is a public call that can arrive from any thread, any time (even twice).
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        ShuttingDown?.Invoke(this, EventArgs.Empty);

        // Park the render thread the SAME way a window teardown does - via the gate, NOT a Join. Flag cancellation so the loop
        // won't begin another frame, then take the gate: it blocks until the in-flight frame finishes and holds the next off,
        // so the device is torn down with rendering provably parked (the 0xC0000005 in EndCommandBuffer/Submit is exactly a
        // teardown racing an in-flight frame). No Join: the render thread is a background thread; once it re-takes the gate it
        // sees the cancelled token and exits on its own. Joining the shared render thread would be wrong and is redundant here.
        cancellationTokenSource.Cancel();
        lock (_renderGate)
        {
            ContentUnloading?.Invoke(this, EventArgs.Empty);
            // Tear down any windows still open. OnMainWindowClosed / OnLastWindowClosed already removed theirs; OnExplicitShutDown
            // can shut down with live windows, whose services must be unloaded before the device service is freed.
            foreach (var service in new List<WindowRenderService>(windowToSystem.Values))
            {
                service.UnloadContent();
            }
            windowToSystem.Clear();
            FreeResources();
        }

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