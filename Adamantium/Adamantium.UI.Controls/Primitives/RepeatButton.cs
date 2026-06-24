using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// A button that raises <see cref="ButtonBase.Click"/> repeatedly while held: once immediately on press, then - after
/// <see cref="Delay"/> ms - every <see cref="Interval"/> ms until release. Used for the ScrollBar line/page buttons
/// (and spinners). Mirrors WPF's RepeatButton.
/// </summary>
public class RepeatButton : ButtonBase
{
    public static readonly AdamantiumProperty DelayProperty = AdamantiumProperty.Register(nameof(Delay),
        typeof(int), typeof(RepeatButton), new PropertyMetadata(500));

    public static readonly AdamantiumProperty IntervalProperty = AdamantiumProperty.Register(nameof(Interval),
        typeof(int), typeof(RepeatButton), new PropertyMetadata(33));

    private DispatcherTimer _timer;

    public RepeatButton()
    {
        // Press-and-hold: the first click fires immediately on press (ClickMode.Press), then the timer drives repeats.
        ClickMode = ClickMode.Press;
    }

    /// <summary>Milliseconds the button must be held before auto-repeat begins.</summary>
    public int Delay
    {
        get => GetValue<int>(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    /// <summary>Milliseconds between repeats once auto-repeat has begun.</summary>
    public int Interval
    {
        get => GetValue<int>(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);   // immediate click (ClickMode.Press) + capture + IsPressed
        if (IsPressed) StartTimer();
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        StopTimer();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);                     // re-arms IsPressed if the pointer returns while still held
        if (IsPressed) StartTimer();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);                     // disarms IsPressed (keeps capture) - pause repeating
        if (!IsPressed) StopTimer();
    }

    private void StartTimer()
    {
        _timer ??= CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, Interval));
        _timer.Start(TimeSpan.FromMilliseconds(Math.Max(1, Delay)));   // first repeat after Delay, then every Interval
    }

    private void StopTimer() => _timer?.Stop();

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += OnTick;
        return timer;
    }

    private void OnTick(object sender, EventArgs e)
    {
        // Repeat only while genuinely held and usable; otherwise stand down.
        if (IsPressed && IsEnabled) OnClick();
        else StopTimer();
    }
}
