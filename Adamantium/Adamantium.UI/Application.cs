using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.OSX;
using Adamantium.UI.Processors;
using Adamantium.UI.Windows;
using AdamantiumVulkan;
using AdamantiumVulkan.Core;

namespace Adamantium.UI
{
    public abstract class Application : DependencyComponent, IRunningService
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

        public static Application Current { get; internal set; }
        public Uri StartupUri { get; set; }

        public ShutDownMode ShutDownMode { get; set; }

        protected GraphicsDevice GraphicsDevice;
        internal ServiceStorage Services { get; set; }

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

        protected Application()
        {
            VulkanDllMap.Register();
            ShutDownMode = ShutDownMode.OnMainWindowClosed;
            Current = this;
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
            Services = new ServiceStorage();
            Services.Add(systemManager);
            Services.Add<IRunningService>(Current);
            entityWorld = new EntityWorld(Services);
            Initialize();
        }

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        protected void OnWindowAdded(IWindow window)
        {
            var @params = new PresentationParameters(PresenterType.Swapchain)
            {
                Width = (uint)window.ClientWidth,
                Height = (uint)window.ClientHeight,
                BuffersCount = 3,
                MSAALevel = MSAALevel.X4,
                HInstanceHandle = Process.GetCurrentProcess().Handle
            };
            @params.OutputHandle = window.SurfaceHandle;
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

        internal abstract MouseDevice MouseDevice { get; }

        internal abstract KeyboardDevice KeyboardDevice { get; }

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

        public abstract void Run();

        protected void RunUpdateDrawBlock()
        {
            try
            {
                var frameTime = preciseTimer.GetElapsedTime();
                Update(appTime);
                BeginScene();
                Draw(appTime);
                EndScene();

                UpdateGameTime(frameTime);
                CalculateFps(frameTime);

                ProcessPendingWindows();
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
                IsRunning = false;
            }
            else if (ShutDownMode == ShutDownMode.OnLastWindowClosed && Windows.Count == 0)
            {
                IsRunning = false;
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
            Services.Add(GraphicsDevice);

            IsRunning = true;
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        public void ShutDown()
        {
            IsRunning = false;
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

        public static Application New()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsApplication();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new OSXApplication();
            }
            
            throw new ArgumentException($"current platform {RuntimeInformation.OSDescription} is not currently supported");
        }
    }
}
