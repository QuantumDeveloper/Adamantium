using System;
using System.Threading;

namespace Adamantium.UI.Core.Dispatcher;

/// <summary>
/// A timer whose <see cref="Tick"/> is raised back on the thread that created it (the UI thread), like WPF's
/// DispatcherTimer. It wraps a thread-pool <see cref="System.Threading.Timer"/> and marshals each tick through the
/// <see cref="SynchronizationContext"/> captured at construction (the UI thread installs a dispatcher-backed context,
/// whose Post queues onto the dispatcher), so handlers may safely touch thread-affine UI state.
///
/// Lives in Core (not the UI assembly) so controls - e.g. <c>RepeatButton</c> and the ScrollBar parts built on it -
/// can use it. With no captured context (a headless test), the tick is raised inline on the timer thread.
/// </summary>
public sealed class DispatcherTimer : IDisposable
{
    private readonly SynchronizationContext _syncContext;
    private readonly Timer _timer;
    private readonly SendOrPostCallback _post;
    private TimeSpan _interval = TimeSpan.Zero;
    private bool _isEnabled;

    public DispatcherTimer()
    {
        _syncContext = SynchronizationContext.Current;
        _post = _ => RaiseTick();
        _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler Tick;

    /// <summary>Arbitrary state, like WPF's DispatcherTimer.Tag (e.g. the control that owns the timer).</summary>
    public object Tag { get; set; }

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (_isEnabled) Schedule(_interval);   // running: apply the new cadence at once
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (value) Start(); else Stop(); }
    }

    /// <summary>Starts ticking after <see cref="Interval"/>, then every <see cref="Interval"/>.</summary>
    public void Start() => Start(_interval);

    /// <summary>Starts with a custom first delay, then ticks every <see cref="Interval"/> (the press-then-repeat pattern).</summary>
    public void Start(TimeSpan firstDelay)
    {
        _isEnabled = true;
        Schedule(firstDelay);
    }

    public void Stop()
    {
        _isEnabled = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Schedule(TimeSpan firstDelay)
    {
        var due = firstDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : firstDelay;
        var period = _interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : _interval;
        _timer.Change(due, period);
    }

    private void OnTimerTick(object state)
    {
        if (!_isEnabled) return;
        if (_syncContext != null && _syncContext != SynchronizationContext.Current)
            _syncContext.Post(_post, null);
        else
            RaiseTick();
    }

    private void RaiseTick()
    {
        if (_isEnabled) Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _timer.Dispose();
}
