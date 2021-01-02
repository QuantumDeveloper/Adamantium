﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.MacOS;
using Adamantium.UI.Processors;
using Adamantium.UI.Threading;
using Adamantium.UI.Windows;
using AdamantiumVulkan;
using AdamantiumVulkan.Core;

namespace Adamantium.UI
{
    public abstract class Application : AdamantiumComponent, IService
    {
        public IWindow MainWindow
        {
            get => mainWindow;
            set
            {
                if (mainWindow != null)
                {
                    mainWindow.Closed -= MainWindow_Closed;
                    mainWindow.Closed += MainWindow_Closed;
                }
                mainWindow = value;
            }
        }

        private object applicationLocker = new object();

        public WindowCollection Windows { get; private set; }

        public ShutDownMode ShutDownMode { get; set; }

        protected GraphicsDevice GraphicsDevice;
        internal IDependencyResolver Services { get; set; }

        private EntityWorld entityWorld;
        private Dictionary<IWindow, UIRenderProcessor> windowToSystem;
        private Dictionary<IWindow, GraphicsDevice> windowToDevices;

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

        static Application()
        {
            WindowsPlatform.Initialize();
        }

        protected Application()
        {
            Current = this;
            
            VulkanDllMap.Register();
            ShutDownMode = ShutDownMode.OnMainWindowClosed;
            systemManager = new ApplicationSystemManager(this);
            windowToSystem = new Dictionary<IWindow, UIRenderProcessor>();
            windowToDevices = new Dictionary<IWindow, GraphicsDevice>();
            addedWindows = new List<IWindow>();
            closedWindows = new List<IWindow>();
            Windows = new WindowCollection();
            Windows.WindowAdded += WindowAdded;
            Windows.WindowRemoved += WindowRemoved;
            appTime = new ApplicationTime();
            preciseTimer = new PreciseTimer();
            Services = new AdamantiumServiceLocator();
            Services.RegisterInstance<IService>(this);
            Services.RegisterInstance<SystemManager>(systemManager);
            entityWorld = new EntityWorld(Services);
            Initialize();
            renderThread = new Thread(RenderThread);
            
            //InputDevice mouseDevice = new InputDevice();
            //mouseDevice.WindowHandle = e.Window.Handle;
            //mouseDevice.UsagePage = HIDUsagePage.Generic;
            //mouseDevice.UsageId = HIDUsageId.Mouse;
            //mouseDevice.Flags = InputDeviceFlags.None;
            //Interop.RegisterRawInputDevices(new[] { mouseDevice }, 1, Marshal.SizeOf(mouseDevice));
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

        public bool IsRunning => !(cancellationTokenSource?.IsCancellationRequested == true);
        public bool IsPaused { get; private set; }

        protected void OnWindowAdded(IWindow window)
        {
            var @params = new PresentationParameters(
                PresenterType.Swapchain,
                (uint)window.ClientWidth,
                (uint)window.ClientHeight,
                window.SurfaceHandle,
                MSAALevel.X4
                )
            {
                HInstanceHandle = Process.GetCurrentProcess().Handle
            };
            
            var device = GraphicsDevice.CreateRenderDevice(@params);
            device.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);

            windowToDevices[window] = device;

            var transformProcessor = new UITransformProcessor(entityWorld);
            var renderProcessor = new UIRenderProcessor(entityWorld, device);
            var entity = new Entity();
            entity.AddComponent(window);
            entityWorld.AddEntity(entity);
            entityWorld.AddProcessor(transformProcessor);
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
            var device = windowToDevices[window];
            device?.Dispose();
            windowToDevices.Remove(window);

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

            if (window == null) throw new ArgumentNullException($"{nameof(window)}");

            MainWindow = window;
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
                Update(appTime);
                BeginScene();
                Draw(appTime);
                EndScene();

                UpdateGameTime(frameTime);
                CalculateFps(frameTime);
                
                CheckExitConditions();
            }
            catch (Exception ex)
            {
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
            var vulkanInstance = VulkanInstance.Create("Adamantium Engine", true);
            GraphicsDevice = GraphicsDevice.Create(vulkanInstance, vulkanInstance.CurrentDevice);
            //GraphicsDevice.BlendState = GraphicsDevice.BlendStates.AlphaBlend;
            //GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipEnabled;
            //GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
            Services.RegisterInstance<GraphicsDevice>(GraphicsDevice);
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        public void ShutDown()
        {
            cancellationTokenSource.Cancel();
            ContentUnloading?.Invoke(this, EventArgs.Empty);

            GraphicsDevice.DeviceWaitIdle();
            GraphicsDevice?.Dispose();
        }

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

        public event EventHandler<EventArgs> Started;
        public event EventHandler<EventArgs> ShuttingDown;
        public event EventHandler<EventArgs> Stopped;
        public event EventHandler Paused;
        public event EventHandler Resumed;
        public event EventHandler<EventArgs> Initialized;
        public event EventHandler<EventArgs> ContentLoading;
        public event EventHandler<EventArgs> ContentUnloading;
        
    }
}
