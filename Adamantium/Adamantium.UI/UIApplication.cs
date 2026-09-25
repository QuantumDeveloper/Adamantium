using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.ECS;
using Adamantium.Multiverse;
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
using Adamantium.UI.Rendering;
using Adamantium.UI.Themes.FluentTheme;
using Adamantium.Vulkan.Loader;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using UnhandledExceptionEventArgs = Adamantium.UI.Core.RoutedEvents.UnhandledExceptionEventArgs;
using UnhandledExceptionEventHandler = Adamantium.UI.Core.RoutedEvents.UnhandledExceptionEventHandler;

namespace Adamantium.UI;

public abstract class UIApplication : FundamentalUIComponent, IAdamantiumApplication, IUIApplication, IWindowPlatformService
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

    // The loop never waits for the render: past MaxFramesInFlight unrendered frames it stops recording, not updating.
    private Thread renderThread;
    // Held around each render frame, so window teardown and shutdown never dispose a device mid-frame.
    private readonly object _renderGate = new object();
    // ShutDown can arrive from any thread at any time; this guards re-entry.
    private volatile bool _isShuttingDown;
    private int _framesInFlight;
    private AppTime _renderAppTime;   // time of the newest recorded frame
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
        var satellites = new Satellites();
        satellites.Add<IAdamantiumApplication>(this);
        EntityWorld = new EntityWorld(Container, satellites);
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

    // A capped file per launch, the last few kept: one file a day grew to 149 MB and mixed the runs together.
    private const int RetainedLogRuns = 10;
    private const long LogSizeLimitBytes = 32L * 1024 * 1024;

    private void ConfigureLogging()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);
        SweepOldLogs(directory);

        var path = Path.Combine(directory, $"adamantium_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            // Stops at the limit instead of rolling: the first occurrences, the ones worth reading, are at the top.
            .WriteTo.File(path, fileSizeLimitBytes: LogSizeLimitBytes, rollOnFileSizeLimit: false)
            .CreateLogger();
    }

    private static void SweepOldLogs(string directory)
    {
        try
        {
            var stale = new DirectoryInfo(directory)
                .GetFiles("adamantium_*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(RetainedLogRuns);

            foreach (var file in stale) file.Delete();
        }
        catch (IOException)
        {
            // Held open by another instance; not worth failing a launch over.
        }
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

    /// <summary>The presentation policy of every window that does not set its own <see cref="IWindow.PresentPolicy"/>.
    /// Set it at startup: swapchains that already exist are not rebuilt.</summary>
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

    /// <summary>Queues a window to join on the next frame: this runs on the pump thread while the loop walks the
    /// collections it would join.</summary>
    public void AddWindow(IWindow window)
    {
        lock (applicationLocker)
        {
            addedWindows.Add(window);
        }

        LoopSignal.Request();   // registered on the next frame; do not wait for the idle timeout
    }

    /// <summary>Tears the window down right away, never queued: a frame late, the render thread would draw into a
    /// surface that is already gone.</summary>
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
        // The render thread must not draw while the devices it draws with are destroyed and remade.
        lock (_renderGate)
        {
            EntityWorld.RemoveAllServices();
            EntityWorld.RemoveAllEntities();
            EntityWorld.ForceUpdate();
            windowToSystem.Clear();
            foreach (var window in Windows)
            {
                window.InvalidateRender(true);
            }
            Graphics.Fonts.FontAtlasStore.Reset();
            GraphicsDeviceService.ChangeOrCreateMainDevice("Adamantium Main", true);
            foreach (var window in Windows)
            {
                CreateWindowService(window);
            }
            EntityWorld.ForceUpdate();
            Interlocked.Exchange(ref _framesInFlight, 0);
        }
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

        // The render thread waits between frames while this window's resources go, then draws the other windows on.
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
        // Before any window: a crash of the driver's compiler then costs a child process, not the application.
        ShaderPrecompiler.EnsureCompiled(GraphicsDeviceService.ResourceLoaderDevice as GraphicsDevice);
        LoadThemes();
        SubscribeToEvents();
        
        EntityWorld.Initialize();
        OnInitialize();
        RegisterServices(Container);
        _visualRenderer = Container.Resolve<IVisualRenderer>();   // its device is created on the first snapshot
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
        // One theme with light and dark variants: two themes rebuilt every template just to change the colors.
        var fluent = new Fluent();
        ThemeManager.AddTheme(fluent.Name, fluent);

        // A second theme proves the first is a theme and not the framework's own look.
        var editorPro = new Themes.EditorProTheme.EditorPro();
        ThemeManager.AddTheme(editorPro.Name, editorPro);

        // Owns the window and the caption so far and borrows the rest from Fluent.
        var macOs = new Themes.MacOsTheme.MacOs();
        ThemeManager.AddTheme(macOs.Name, macOs);

        // Opens on Fluent, or on the theme ADAM_THEME names: a theme is fully exercised only when current from the first frame.
        var requested = Environment.GetEnvironmentVariable("ADAM_THEME");
        var startOn = string.IsNullOrEmpty(requested) ? fluent : ThemeManager[requested] ?? fluent;
        ThemeManager.SetTheme(startOn);
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
        containerRegistry.RegisterInstance<IAdamantiumApplication>(this);
        containerRegistry.RegisterInstance<IUIApplication>(this);
        containerRegistry.RegisterInstance<EntityWorld>(EntityWorld);
    }

    protected virtual void RegisterServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IThemeManager>(ThemeManager);
        // Off-screen renderer for drag ghosts and brush bakes; an app may register its own before base.
        if (!containerRegistry.IsRegistered<IVisualRenderer>())
            containerRegistry.RegisterSingleton<IVisualRenderer, VisualRenderer>();

        // The window that follows the cursor during a drag.
        if (!containerRegistry.IsRegistered<IDragGhost>() && OperatingSystem.IsWindows())
            containerRegistry.RegisterSingleton<IDragGhost, Win32DragGhost>();

        // Drag-drop with other applications; unregistered elsewhere, where only in-app drag-drop runs.
        if (!containerRegistry.IsRegistered<INativeDragDrop>() && OperatingSystem.IsWindows())
            containerRegistry.RegisterSingleton<INativeDragDrop, WindowsDragDrop>();

        RegisterNavigationServices(containerRegistry);
    }

    // Each default is guarded, so an app that registers its own before calling base wins.
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
            // For the area itself: a PaneGroup is covered as a Selector, and an area adds the choice of place.
            mappings.Register<DockingArea>(new DockingAreaRegionAdapter(viewLocator));
            containerRegistry.RegisterInstance<RegionAdapterMappings>(mappings);
        }

        // Dialog hosts: inside the window by default, or in a window of their own.
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
            // Published before it starts: BeginDraw uses it to refuse recording on the render thread.
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
                WaitForWork();

                // Windows join here, on the loop thread, before anything walks them.
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

    /// <summary>How often the loop updates at most while something moves; the presented rate belongs to the render thread.</summary>
    public static uint UpdateRateHz { get; set; } = 120;

    // A safety net only: every source of work signals the loop, so this turns a forgotten one into a late frame.
    private const int IdleWakeMs = 250;

    private long _loopFrameStart;

    // Records snapshots on the loop thread and draws them on the render thread.
    private IVisualRenderer _visualRenderer;

    /// <summary>A window's swapchain lags the size of its surface, so the window is being resized right now. Set anew
    /// every frame by the render service.</summary>
    public static bool SwapchainTrailing { get; set; }

    // Paced to UpdateRateHz, then blocked on the loop signal when nothing is dirty or animating: the loop signals itself,
    // so the signal alone would spin it.
    private void WaitForWork()
    {
        // Uncapped while a window is resized: a frame shows the window as it was when the frame started.
        if (!SwapchainTrailing)
        {
            var target = 1000.0 / Math.Max(1, UpdateRateHz);
            var elapsed = Stopwatch.GetElapsedTime(_loopFrameStart).TotalMilliseconds;
            var remaining = target - elapsed;
            if (remaining >= 1.0) Thread.Sleep((int)remaining);
        }

        // Every stage counts, popups and adorners included.
        if (!RenderDirty.AnyHasWork && !AnimationManager.HasActiveAnimations)
            LoopSignal.Wait(IdleWakeMs, cancellationTokenSource.Token);

        _loopFrameStart = Stopwatch.GetTimestamp();
    }

    private void OnCycleFinishedInternal()
    {
        // Snapshots are recorded here, on the loop thread, once the frame has settled; they are drawn on the render thread.
        _visualRenderer?.RecordPendingSnapshots();

        CheckExitConditions();
        if (GraphicsDeviceService.DeviceUpdateNeeded)
        {
            RecreateDevicesAndServices();
        }
        CycleFinished?.Invoke(this, EventArgs.Empty);
    }

    // Records every window's render packet after the whole update and before any draw. The dirty set is cleared once,
    // after all windows, so the second window still sees all of it.
    private void RecordRenderFrame()
    {
        if (RenderThreadOptions.SingleThreaded || DisableRendering) return;
        _recordedThisFrame = false;

        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;

        // The render is MaxFramesInFlight frames behind: skip the record, not the update. The marks stay for the next one.
        if (threaded && Volatile.Read(ref _framesInFlight) >= MaxFramesInFlight) return;

        // Cleared only if every window recorded: one without a renderer yet records nothing, and its marks must stay.
        // A copy, since recording may open windows; not under _renderGate, which would lock the loop to the render thread.
        var recordedAll = true;
        var services = new WindowRenderService[windowToSystem.Count];
        windowToSystem.Values.CopyTo(services, 0);

        foreach (var service in services)
            recordedAll &= service.RecordFrame();
        _recordedThisFrame = recordedAll;

        // Cleared here, on the loop thread: the render thread must never touch RenderDirty.
        if (threaded && recordedAll) RenderDirty.Clear();
    }

    private bool _recordedThisFrame;

    // Returns whether the frame drew. Never clears RenderDirty: this may run on the render thread.
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
                RuntimeStats.PresentedFrames++;   // the real frame rate once rendering has its own thread
            }
        }

        // Snapshots draw on whichever thread owns the device this frame, so it is never touched by two at once.
        _visualRenderer?.DrawPendingSnapshots();

        return drew;
    }

    // Draws inline, or, with a render thread, only publishes the recorded frame and returns without waiting.
    private void DispatchRenderFrame(AppTime appTime)
    {
        var threaded = RenderThreadOptions.RenderThreadEnabled && renderThread != null;

        // Inline: cleared once, after every window has drawn, not per window.
        if (!threaded)
        {
            var drewInline = ExecuteDrawSequence(appTime);
            if (drewInline) RenderDirty.Clear();
            return;
        }

        // Threaded: the loop never draws. The device belongs to the render thread, and each packet carries its own projection.
        if (!_recordedThisFrame) return;
        _renderAppTime = appTime;
        Interlocked.Increment(ref _framesInFlight);
    }

    // Draws at its own pace with whatever the loop has published; with nothing new it replays the retained frame.
    private void RenderThread()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            // The gate keeps teardown and shutdown out of a frame; cancellation is re-checked inside it.
            lock (_renderGate)
            {
                if (cancellationTokenSource.IsCancellationRequested) break;
                Interlocked.Exchange(ref _framesInFlight, 0);   // the apply drains whatever was published
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
    /// Updates the time for each frame
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

    // Emptied into a local under the lock: registering runs application code, which may open another window.
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
        // Not before the first window, or the application closes before it opens.
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

    // Slower than ~20 fps is a stall - a build, JIT or wake-up hitch - not animation time.
    private const double StallFrameSeconds = 1.0 / 20.0;

    // How far animations move through a stall: almost a pause, yet still progress under a sustained low rate.
    private const double StallAnimationStep = 0.004;

    protected void Update(AppTime frameTime)
    {
        // Input first, before anything touches the visual tree.
        Threading.Dispatcher.CurrentDispatcher?.DrainPending();
        // A stall moves animations by a sliver only; otherwise they burn their duration unseen and appear to jump.
        var animationStep = frameTime.FrameTime < StallFrameSeconds ? frameTime.FrameTime : StallAnimationStep;
        AnimationManager.Tick(animationStep);

        EntityWorld.ServiceManager.Update(frameTime);

        // Teardowns are paid here, a bounded number per frame and never on a stall, not in the frame that caused them.
        if (frameTime.FrameTime < StallFrameSeconds) DiscardedVisuals.Drain(DiscardReleasesPerFrame);
    }

    // Clears a theme swap's thousand-odd parts within a second, without ever making a frame late.
    private const int DiscardReleasesPerFrame = 128;

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
        // May come from any thread, even twice.
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        ShuttingDown?.Invoke(this, EventArgs.Empty);

        // Cancel so no frame starts, then take the gate to wait out the one in flight; the render thread then exits by itself.
        cancellationTokenSource.Cancel();
        lock (_renderGate)
        {
            ContentUnloading?.Invoke(this, EventArgs.Empty);
            // Windows may still be open under OnExplicitShutDown; their services go before the device.
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