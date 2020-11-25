using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Exceptions;

namespace Adamantium.UI.Threading
{
    public sealed class Dispatcher : IDispatcher
    {
        private CancellationTokenSource cancellationTokenSource;
        private IApplicationPlatform appPlatform;
        private DispatcherOperationExecutor executor;
        private Thread uiThread;

        public static Dispatcher CurrentDispatcher { get; }
        
        static Dispatcher()
        {
            CurrentDispatcher = new Dispatcher(AdamantiumServiceLocator.Current.Resolve<IApplicationPlatform>());
        }

        public Dispatcher(IApplicationPlatform appPlatform)
        {
            cancellationTokenSource = new CancellationTokenSource();
            MainThread = Thread.CurrentThread;
            this.appPlatform = appPlatform;
            executor = new DispatcherOperationExecutor();
        }
        
        public Thread MainThread { get; }

        public Thread UIThread
        {
            get => uiThread;
            set => uiThread = value;
        }

        public bool IsRunning => !cancellationTokenSource.IsCancellationRequested;
        
        public void Run()
        {
            appPlatform.Run(cancellationTokenSource.Token);
        }

        public void Shutdown()
        {
            cancellationTokenSource.Cancel();
        }

        public bool CheckAccess()
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
    }
}