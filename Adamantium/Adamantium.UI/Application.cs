using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.EntityFramework;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.MacOS;
using Adamantium.UI.Processors;
using Adamantium.UI.Threading;
using Adamantium.UI.Windows;
using AdamantiumVulkan;
using AdamantiumVulkan.Core;
using UnhandledExceptionEventArgs = Adamantium.UI.RoutedEvents.UnhandledExceptionEventArgs;
using UnhandledExceptionEventHandler = Adamantium.UI.RoutedEvents.UnhandledExceptionEventHandler;

namespace Adamantium.UI;

public abstract class Application : AdamantiumComponent, IService
{
    //private Mutex mutex = new Mutex(false, "adamantiumMutex");
        
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

    private object applicationLocker = new object();

    public WindowCollection Windows { get; private set; }

    public ShutDownMode ShutDownMode { get; set; }

    protected MainGraphicsDevice MainGraphicsDevice;
    internal IDependencyResolver Services { get; set; }

    private EntityWorld entityWorld;
    private Dictionary<IWindow, WindowProcessor> windowToSystem;

    private ApplicationTime appTime;
    private TimeSpan totalTime;
    private PreciseTimer preciseTimer;
    private Double fpsTime;
    private Int32 fpsCounter;

    private SystemManager systemManager;
    private IWindow mainWindow;
    private List<IWindow> addedWindows;
    private List<IWindow> closedWindows;
    private bool firstWindowAdded;
    private Thread renderThread;
    private CancellationTokenSource cancellationTokenSource;

    private IEventAggregator eventAggregator;

    static Application()
    {
        WindowsPlatform.Initialize();
    }

    protected Application()
    {
        Current = this;
        var d = Dispatcher.CurrentDispatcher;
        VulkanDllMap.Register();
        ShutDownMode = ShutDownMode.OnMainWindowClosed;
        systemManager = new ApplicationSystemManager(this);
        windowToSystem = new Dictionary<IWindow, WindowProcessor>();
        addedWindows = new List<IWindow>();
        closedWindows = new List<IWindow>();
        Windows = new WindowCollection();
        Windows.WindowAdded += WindowAdded;
        Windows.WindowRemoved += WindowRemoved;
        appTime = new ApplicationTime();
        preciseTimer = new PreciseTimer();
        Services = AdamantiumServiceLocator.Current;
        Services.RegisterInstance<IService>(this);
        Services.RegisterInstance<SystemManager>(systemManager);
        entityWorld = new EntityWorld(Services);
        eventAggregator = Services.Resolve<IEventAggregator>();
        Initialize();
        renderThread = new Thread(RenderThread);
    }
        
    private void RenderThread()
    {
        Dispatcher.CurrentDispatcher.UIThread = Thread.CurrentThread;
        // Dispatcher.CurrentDispatcher.InvokeAsync(() =>
        // {
        //     var wnd = Window.New();
        //     wnd.Width = 1280;
        //     wnd.Height = 720;
        //     Windows.Add(wnd);
        //     wnd.Show();
        // });
            
        while(!cancellationTokenSource.IsCancellationRequested)
        {
            RunUpdateDrawBlock();
        }
    }
        
    public static Application Current { get; private set; }

    public bool IsRunning => cancellationTokenSource?.IsCancellationRequested != true;
    public bool IsPaused { get; private set; }

    protected void OnWindowAdded(IWindow window)
    {
        var renderProcessor = new WindowProcessor(entityWorld, window, MainGraphicsDevice);
        var entity = new Entity();
        entity.AddComponent(window);
        entityWorld.AddEntity(entity);
        entityWorld.AddProcessor(renderProcessor);

        windowToSystem.Add(window, renderProcessor);

        if (!firstWindowAdded)
        {
            firstWindowAdded = true;
        }
    }

    protected void OnWindowRemoved(IWindow window)
    {
        if (!windowToSystem.ContainsKey(window)) return;
        var processor = windowToSystem[window];
        processor.UnloadContent();
        entityWorld.RemoveProcessor(processor);
        windowToSystem.Remove(window);

        if (window == MainWindow)
        {
            MainWindow = null;
        }
    }

    internal MouseDevice MouseDevice => MouseDevice.CurrentDevice;

    internal KeyboardDevice KeyboardDevice => KeyboardDevice.CurrentDevice;

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        MainWindow.Closed -= MainWindow_Closed;
        MainWindow = null;
    }

    private void WindowAdded(object sender, WindowEventArgs e)
    {
        lock (applicationLocker)
        {
            addedWindows.Add(e.Window);
        }
    }

    private void WindowRemoved(object sender, WindowEventArgs e)
    {
        lock (applicationLocker)
        {
            closedWindows.Add(e.Window);
        }
    }

    public virtual void Run()
    {
        cancellationTokenSource = new CancellationTokenSource();
        renderThread.Start();
        Dispatcher.CurrentDispatcher.Run(cancellationTokenSource.Token);
    }
        
    public void Run(IWindow window)
    {
        if (cancellationTokenSource != null) return;

        MainWindow = window ?? throw new ArgumentNullException($"{nameof(window)}");
        MainWindow.Show();
        Windows.Add(window);
            
        Run();
    }
        
    protected void RunUpdateDrawBlock()
    {
        try
        {
            var frameTime = preciseTimer.GetElapsedTime();
            ProcessPendingWindows();
            //mutex.WaitOne();
            Update(appTime);
            BeginScene();
            Draw(appTime);
            EndScene();
            //mutex.ReleaseMutex();
            UpdateGameTime(frameTime);
            CalculateFps(frameTime);
                
            CheckExitConditions();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex));
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

        if (ShutDownMode == ShutDownMode.OnMainWindowClosed && MainWindow == null)
        {
            ShutDown();
        }
        else if (ShutDownMode == ShutDownMode.OnLastWindowClosed && Windows.Count == 0)
        {
            ShutDown();
        }
    }

    public void Run(object context)
    {

    }

    protected void UpdateGameTime(double elapsed)
    {
        appTime.FrameTime = elapsed;

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
        appTime.Update(totalTime);
    }

    protected void CalculateFps(double elapsed)
    {
        fpsCounter++;
        fpsTime += elapsed;
        if (fpsTime >= 1.0d)
        {
            Console.WriteLine($"FPS = {fpsCounter}");
            appTime.FpsCount = (fpsCounter) / (Single)fpsTime;
            fpsCounter = 0;
            fpsTime = 0;
        }
    }

    public event UnhandledExceptionEventHandler UnhandledException;

    public IGameTime Time { get; private set; }

    protected virtual void BeginScene()
    {
    }

    protected void Update(IGameTime frameTime)
    {
        Time = frameTime;
        systemManager.Update(frameTime);
    }

    protected void Draw(IGameTime frameTime)
    {
        Time = frameTime;
        systemManager.Draw(frameTime);
    }

    protected void EndScene()
    {
    }

    protected void Initialize()
    {
        MainGraphicsDevice = MainGraphicsDevice.Create("Adamantium Engine", true);
        Services.RegisterInstance<GraphicsDevice>(MainGraphicsDevice);

        eventAggregator.GetEvent<WindowAddedEvent>().Subscribe(OnWindowAdded, ThreadOption.UIThread);
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    public void ShutDown()
    {
        cancellationTokenSource.Cancel();
        ContentUnloading?.Invoke(this, EventArgs.Empty);

        MainGraphicsDevice.DeviceWaitIdle();
        MainGraphicsDevice?.Dispose();
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
    public event EventHandler<EventArgs> Initialized;
    public event EventHandler<EventArgs> ContentLoading;
    public event EventHandler<EventArgs> ContentUnloading;
        
}