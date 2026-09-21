using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Exceptions;
using Adamantium.UI.Platforms;

namespace Adamantium.UI.Threading;

public sealed class Dispatcher : IDispatcher
{
    private CancellationToken cancellationToken;
    private IApplicationPlatform appPlatform;
    private DispatcherOperationExecutor executor;
    private Thread uiThread;

    private static object collectionLocker = new object();
    private static Dictionary<DispatcherContext, Dispatcher> dispatchers;
        
    public static Dispatcher CurrentDispatcher { get; private set; }
    
    public static bool Initialized { get; private set; }
        
    internal static void Initialize(IUIContext uiContext)
    {
        if (Initialized) return;
        
        dispatchers = new Dictionary<DispatcherContext, Dispatcher>();
        CurrentDispatcher = new Dispatcher(uiContext.Resolve<IApplicationPlatform>());
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));
        
        Initialized = true;
    }

    private void SetContext()
    {
        Context = CreateContext();
    }

    private DispatcherContext CreateContext()
    {
        return new(appPlatform, MainThread);
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
        return MainThread == Thread.CurrentThread;
    }

    public void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new DispatcherException($"You are accessing object from non-ui or main thread");
        }
    }

    public void Invoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        executor.Invoke(action, priority);
    }

    public void Invoke(Delegate action, object args)
    {
        action?.DynamicInvoke(args);
    }

    public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        return executor.InvokeAsync(action, priority);
    }

    public Task InvokeAsync(Delegate action, object args)
    {
        return executor.InvokeAsync(action, args, 1);
    }

    public static void Attach(Dispatcher dispatcher)
    {
        AddToDict(dispatcher);
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI loop thread (drained at the start of the next Update, before
    /// layout). Window input arrives on the OS message thread; routing it through here keeps the event handling - and
    /// the tree mutations it triggers - on the same thread as layout/render, instead of racing them. Called already on
    /// the loop thread it runs inline.
    ///
    /// The queue is <see cref="Core.LoopSignal"/> - the same pipe the loop sleeps on. So every mouse move, key, and window
    /// event is BOTH the work and the wake: posting it is what pulls the loop out of an idle sleep.
    /// </summary>
    public void Post(Action action)
    {
        if (action == null) return;
        var loop = uiThread;
        if (loop == null || Thread.CurrentThread == loop)
        {
            action();
            return;
        }
        Core.LoopSignal.Post(action);
    }

    /// <summary>Runs every queued action in order. MUST be called only from the UI loop thread (start of Update).</summary>
    public void DrainPending() => Core.LoopSignal.Drain();
}