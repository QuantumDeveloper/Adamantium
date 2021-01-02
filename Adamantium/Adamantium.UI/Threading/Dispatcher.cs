using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Exceptions;

namespace Adamantium.UI.Threading
{
    public sealed class Dispatcher : IDispatcher
    {
        private CancellationToken cancellationToken;
        private IApplicationPlatform appPlatform;
        private DispatcherOperationExecutor executor;
        private Thread uiThread;
        
        private static object collectionLocker = new object();
        private static Dictionary<DispatcherContext, Dispatcher> dispatchers;
        
        public static Dispatcher CurrentDispatcher { get; }
        
        static Dispatcher()
        {
            dispatchers = new Dictionary<DispatcherContext, Dispatcher>();
            CurrentDispatcher = new Dispatcher(AdamantiumServiceLocator.Current.Resolve<IApplicationPlatform>());
        }

        private void SetContext()
        {
            Context = CreateContext();
        }

        private DispatcherContext CreateContext()
        {
            return new DispatcherContext(appPlatform, MainThread);
        }

        private static void AddToDict(Dispatcher dispatcher)
        {
            lock (collectionLocker)
            {
                dispatchers[dispatcher.Context] = dispatcher;
            }
        }
        
        public DispatcherContext Context { get; private set; }

        public Dispatcher(IApplicationPlatform appPlatform)
        {
            MainThread = Thread.CurrentThread;
            this.appPlatform = appPlatform;
            executor = new DispatcherOperationExecutor(appPlatform);
            appPlatform.Signaled += OnPlatformSignaled;
            SetContext();
            AddToDict(this);
        }

        private void OnPlatformSignaled()
        {
            executor.Execute();
        }

        public Thread MainThread { get; }

        public Thread UIThread
        {
            get => uiThread;
            set => uiThread = value;
        }

        public bool IsRunning => !cancellationToken.IsCancellationRequested;
        
        public void Run(CancellationToken token)
        {
            appPlatform.Run(token);
        }

        public bool CheckAccess()
        {
            lock (collectionLocker)
            {
                if (dispatchers.Count == 1)
                {
                    return CheckAccessInternal();
                }
                
                bool access = false;
                foreach (var dispatcher in dispatchers)
                {
                    access = dispatcher.Value.CheckAccessInternal();
                    if (access) break;
                }

                return access;
            }
        }

        private bool CheckAccessInternal()
        {
            return MainThread == Thread.CurrentThread || UIThread == Thread.CurrentThread;
        }

        public void VerifyAccess()
        {
            if (!CheckAccess())
            {
                throw new DispatcherException($"You are accessing object from non-ui or main thread");
            }
        }

        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            return executor.InvokeAsync(action, priority);
        }

        public static void Attach(Dispatcher dispatcher)
        {
            AddToDict(dispatcher);
        }
    }
}