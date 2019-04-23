using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.Processors;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace Adamantium.UI
{
    public class Application : DependencyComponent, IRunningService
    {
        public ShutDownMode ShutDownMode;
        public Boolean IsApplicationRunning { get; private set; }
        public Window MainWindow { get; set; }
        public WindowCollection Windows { get; private set; }

        public static Application Current { get; internal set; }
        public Uri StartupUri { get; set; }


        private D3DGraphicsDevice GraphicsDevice;
        private EntityWorld _entityWorld;
        private Dictionary<Window, UIRenderProcessor> windowToSystem;

        protected ApplicationTime appTime;

        protected TimeSpan totalTime;

        protected PreciseTimer preciseTimer;

        protected Double fpsTime;
        protected Int32 fpsCounter;
        protected Boolean isPaused;
        private bool _isRunning;
        private bool _isPaused;

        internal ServiceStorage Services { get; set; }
        private SystemManager _systemManager;

        public Application()
        {
            ShutDownMode = ShutDownMode.OnMainWindowClosed;
            Current = this;
            _systemManager = new ApplicationSystemManager(this);
            windowToSystem = new Dictionary<Window, UIRenderProcessor>();
            Windows = new WindowCollection();
            Windows.WindowAdded += WindowAdded;
            Windows.WindowRemoved += WindowRemoved;
            appTime = new ApplicationTime();
            preciseTimer = new PreciseTimer();
            Services = new ServiceStorage();
            Services.Add(_systemManager);
            Services.Add<IRunningService>(Current);
            _entityWorld = new EntityWorld(Services);
            Initialize();
        }

        private void WindowAdded(object sender, WindowEventArgs e)
        {
            var transformProcessor = new UITransformProcessor(_entityWorld);
            var renderProcessor = new UIRenderProcessor(_entityWorld, GraphicsDevice);
            var entity = new Entity();
            entity.AddComponent(e.Window);
            _entityWorld.AddEntity(entity);
            _entityWorld.AddProcessor(transformProcessor);
            _entityWorld.AddProcessor(renderProcessor);

            InputDevice mouseDevice = new InputDevice();
            mouseDevice.WindowHandle = e.Window.WindowHandle;
            mouseDevice.UsagePage = HIDUsagePage.Generic;
            mouseDevice.UsageId = HIDUsageId.Mouse;
            mouseDevice.Flags = InputDeviceFlags.None;
            Interop.RegisterRawInputDevices(new[] { mouseDevice }, 1, Marshal.SizeOf(mouseDevice));

            windowToSystem.Add(e.Window, renderProcessor);
        }


        private void WindowRemoved(object sender, WindowEventArgs e)
        {
            if (!windowToSystem.ContainsKey(e.Window)) return;
            var processor = windowToSystem[e.Window];
            processor.UnloadContent();
            _entityWorld.RemoveProcessor(processor);
        }

        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;

        public void Run()
        {

            Win32.Message msg;
            IsApplicationRunning = true;

            while (IsApplicationRunning)
            {
                while (Messages.PeekMessage(out msg, IntPtr.Zero, 0, 0, PeekMessageFlag.Remove))
                {
                    Messages.TranslateMessage(ref msg);
                    Messages.DispatchMessage(ref msg);
                }

                try
                {
                    var frametime = preciseTimer.GetElapsedTime();
                    Update(appTime);
                    BeginScene();
                    Draw(appTime);
                    EndScene();

                    UpdateGameTime(frametime);
                    CalculateFps(frametime);
                }
                catch (Exception ex)
                {
                    UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex));
                }

                if (ShutDownMode != ShutDownMode.OnMainWindowClosed) continue;
                if (MainWindow != null && MainWindow.IsClosed)
                {
                    IsApplicationRunning = false;
                }
            }

        }

        public void Run(object context)
        {
            
        }

        protected void UpdateGameTime(double elapsed)
        {
            appTime.FrameTime = elapsed;

            TimeSpan frameTimeSpan = TimeSpan.FromSeconds(appTime.FrameTime);
            if (!isPaused)
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
                appTime.FpsCount = (fpsCounter) / (Single)fpsTime;
                fpsCounter = 0;
                fpsTime = 0;
            }
        }

        public event UnhandledExceptionEventHandler UnhandledException;

        public IGameTime Time { get; private set; }

        protected void BeginScene()
        {
        }

        protected void Update(IGameTime frameTime)
        {
            Time = frameTime;
            _systemManager.Update(frameTime);
        }

        protected void Draw(IGameTime frameTime)
        {
            Time = frameTime;
            _systemManager.Draw(frameTime);
        }

        protected void EndScene()
        {

        }

        protected void Initialize()
        {
            GraphicsDevice = D3DGraphicsDevice.Create(GraphicsAdapter.Default, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug, FeatureLevel.Level_11_0);
            GraphicsDevice.BlendState = GraphicsDevice.BlendStates.AlphaBlend;
            GraphicsDevice.RasterizerState = GraphicsDevice.RasterizerStates.CullNoneClipEnabled;
            GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
            Services.Add(GraphicsDevice);

            _isRunning = true;
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        public void ShutDown()
        {
            IsApplicationRunning = false;
            ContentUnloading?.Invoke(this, EventArgs.Empty);
            GraphicsDevice.Dispose();
        }

        /// <summary>
        /// Calling this method will pause running service
        /// </summary>
        public void Pause()
        {
            if (!IsPaused)
            {
                _isPaused = true;
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
                _isPaused = false;
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
