using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.MacOS;
using Adamantium.UI.Windows;

namespace Adamantium.UI
{
    public abstract class Application: DependencyComponent, IService
    {
        private ApplicationBase app;
        
        protected Application()
        {
            app = New();
            Current = this;
        }
        
        public static Application Current { get; private set; }
        
        public Uri StartupUri { get; set; }

        private static ApplicationBase New()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsApplication();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacOSApplication();
            }
            
            throw new ArgumentException($"current platform {RuntimeInformation.OSDescription} is not currently supported");
        }

        public IWindow MainWindow
        {
            get => app.MainWindow;
            set => app.MainWindow = value;
        }

        public bool IsRunning => app.IsRunning;
        public bool IsPaused => app.IsPaused;
        public void Run()
        {
            app.Run();
        }

        public void Run(IWindow window)
        {
            if (window == null) throw new ArgumentNullException($"{nameof(window)}");
            
            MainWindow = window;
            MainWindow.Show();
            Run();
        }

        public void Run(object context)
        {
            
        }

        public void ShutDown()
        {
            app.ShutDown();
        }

        public void Pause()
        {
            app.Pause();
        }

        public void Resume()
        {
            app.Resume();
        }

        public WindowCollection Windows => app.Windows;

        internal MouseDevice MouseDevice => app.MouseDevice;
        internal KeyboardDevice KeyboardDevice => app.KeyboardDevice;

        protected virtual void OnInitialized()
        {
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