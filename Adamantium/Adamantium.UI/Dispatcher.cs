using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.MacOS;
using Adamantium.UI.Exceptions;
using Adamantium.UI.Windows;
using Adamantium.Win32;

namespace Adamantium.UI
{
    public abstract class Dispatcher
    {
        private static Dictionary<Thread, Dispatcher> dispatchersMap;
        
        private static object globalLockObject = new object();

        static Dispatcher()
        {
            dispatchersMap = new Dictionary<Thread, Dispatcher>();
        }

        private Thread dispatcherThread;

        protected Dispatcher()
        {
            dispatcherThread = Thread.CurrentThread;
            IsRunning = true;
            AddDispatcher();
        }

        public static Dispatcher CurrentDispatcher => GetOrCreateDispatcher();

        public static void Run()
        {
            var dispatcher = GetOrCreateDispatcher();
            
            dispatcher.RunInternal();
        }

        public virtual void Shutdown()
        {
            IsRunning = false;
        }

        public bool CheckAccess()
        {
            return Thread == Thread.CurrentThread;
        }

        public void VerifyAccess()
        {
            if (!CheckAccess()) 
                throw new DispatcherException($"You have no access to the object in this thread");
        }

        internal bool IsRunning { get; set; }

        public Thread Thread => dispatcherThread;

        protected abstract void RunInternal();

        protected void AddDispatcher()
        {
            lock (globalLockObject)
            {
                var thread = Thread.CurrentThread;
                dispatchersMap[thread] = this;
            }
        }

        private static Dispatcher GetOrCreateDispatcher()
        {
            var thread = Thread.CurrentThread;

            lock (globalLockObject)
            {
                if (!dispatchersMap.TryGetValue(thread, out var dispatcher))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        dispatcher = new WindowsDispatcher();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        dispatcher = new MacOSDispatcher();
                    }
                }

                return dispatcher;
            }
        }
    }

    public sealed class WindowsDispatcher : Dispatcher
    {
        private DispatcherWin32NativeSourceWrapper window;
        
        internal WindowsDispatcher()
        {
            window = new DispatcherWin32NativeSourceWrapper();
            window.AddHook(WndProc);
        }
        
        protected override void RunInternal()
        {
            while (IsRunning)
            {
                while (Messages.PeekMessage(out var msg, IntPtr.Zero, 0, 0, PeekMessageFlag.Remove))
                {
                    Messages.TranslateMessage(ref msg);
                    Messages.DispatchMessage(ref msg);
                }
            }
        }
        
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            WindowMessages message = (WindowMessages) msg;
            if (message == WindowMessages.Destroy)
            {
                Shutdown();
            }
            
            return IntPtr.Zero;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            window.Dispose();
        }
    }


    public sealed class MacOSDispatcher : Dispatcher
    {
        private IntPtr appDelegate;
        private IntPtr app;
        
        internal MacOSDispatcher()
        {
            appDelegate = MacOSInterop.CreateApplicationDelegate();
            app = MacOSInterop.CreateApplication(appDelegate);
        }
        
        protected override void RunInternal()
        {
            MacOSInterop.RunApplication(app);
        }
    }

}