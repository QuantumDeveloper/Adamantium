using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// The in-window window manager: one per parent window (a <see cref="IPopupHost"/>). Each <see cref="OverlayWindow"/> is
/// hosted as its OWN card-sized popup - centred on the window and offset for the cascade/drag - like a menu or tooltip, so
/// a click outside a window falls through to the content behind (a single full-window host popup would absorb every click).
/// New windows cascade so they don't open exactly on top of the last, interacting with one raises it to the front, and a
/// pinned window stays above the unpinned ones. A modal window additionally gets a full-window dim scrim beneath it.
/// </summary>
public sealed class OverlayWindowManager
{
    private static readonly ConditionalWeakTable<IPopupHost, OverlayWindowManager> Managers = new();

    /// <summary>The manager for a parent window, created on first use.</summary>
    public static OverlayWindowManager GetFor(IPopupHost host) => Managers.GetValue(host, h => new OverlayWindowManager(h));

    private readonly IPopupHost _host;
    private readonly List<OverlayWindow> _windows = [];   // order = back-to-front; pinned windows always sort to the end (top)
    private bool _keyHooked;

    private OverlayWindowManager(IPopupHost host) => _host = host;

    /// <summary>Open windows, back-to-front. The last one is the active (front-most) window.</summary>
    public IReadOnlyList<OverlayWindow> Windows => _windows;

    /// <summary>The front-most window, or null when none are open.</summary>
    public OverlayWindow ActiveWindow => _windows.Count > 0 ? _windows[^1] : null;

    /// <summary>Shows a window on the overlay and completes when it closes, with its <see cref="OverlayWindow.Result"/>.</summary>
    public Task<object> ShowAsync(OverlayWindow window)
    {
        // Where the window opens: Manual -> its explicit Left/Top (Relative anchors the card's top-left at the offset);
        // CenterOwner (default) -> centred, cascaded so several don't stack exactly. After opening, any Left/Top change
        // (a drag or a bound view model) re-places the card via OverlayWindow.OnPositionChanged.
        var manual = window.StartupLocation == OverlayStartupLocation.Manual;
        var placement = manual ? PlacementMode.Relative : PlacementMode.Center;
        var offset = manual ? new Vector2((float)window.Left, (float)window.Top) : CascadeOffset(_windows.Count);

        // A modal window gets a full-window dim scrim BENEATH it (which absorbs + dims the content behind); a normal window
        // gets none, so the window behind stays usable.
        if (window.IsModal)
        {
            var scrimBrush = window.OverlayBrush ?? new SolidColorBrush(Color.FromArgb(0x99000000));
            var scrimSurface = new Border { Background = scrimBrush };
            scrimSurface.MouseLeftButtonDown += (_, _) => { if (window.CloseOnOverlay && window.CanClose) window.Close(); };
            window.ScrimPopup = new Popup { FillWindow = true, Child = scrimSurface };
            _host.PopupLayer.Add(window.ScrimPopup);
        }

        // The card popup: centred on the parent window, then offset for the cascade (and, later, the drag).
        window.HostPopup = new Popup
        {
            PlacementTarget = (UIComponent)_host,
            Placement = placement,
            HorizontalOffset = offset.X,
            VerticalOffset = offset.Y,
            Child = window
        };

        _windows.Add(window);
        _host.PopupLayer.Add(window.HostPopup);
        Normalize();
        KeepPinnedOnTop(window);
        EnsureKeyHook();

        var tcs = new TaskCompletionSource<object>();
        void OnClosed(object sender, EventArgs e)
        {
            window.Closed -= OnClosed;
            _host.PopupLayer.Remove(window.HostPopup);
            if (window.ScrimPopup != null) _host.PopupLayer.Remove(window.ScrimPopup);
            window.HostPopup = null;
            window.ScrimPopup = null;
            _windows.Remove(window);
            if (_windows.Count == 0) RemoveKeyHook();
            tcs.TrySetResult(window.Result);
        }
        window.Closed += OnClosed;

        window.NotifyShown(this);
        return tcs.Task;
    }

    /// <summary>Shows a window without awaiting its result.</summary>
    public void Show(OverlayWindow window) => _ = ShowAsync(window);

    /// <summary>Raises a window above the others, keeping pinned windows on top.</summary>
    internal void BringToFront(OverlayWindow window)
    {
        if (!_windows.Contains(window)) 
            return;   // already the front-most
        
        _windows.Remove(window);
        _windows.Add(window);
        Normalize();
        RaiseInLayer(window);
        KeepPinnedOnTop(window);
    }

    /// <summary>Re-sorts the layer after a window's pin state changed (raise it to the top of its new group).</summary>
    internal void OnPinnedChanged(OverlayWindow window) => BringToFront(window);

    // A freshly-raised NON-pinned window sits above the pinned ones; re-raise the pinned windows so they stay on top.
    private void KeepPinnedOnTop(OverlayWindow justRaised)
    {
        if (justRaised.IsPinned) return;
        foreach (var w in _windows)
            if (w.IsPinned) RaiseInLayer(w);
    }

    private void RaiseInLayer(OverlayWindow window)
    {
        if (window.HostPopup == null) return;
        if (window.ScrimPopup != null)
        {
            _host.PopupLayer.Remove(window.ScrimPopup);
            _host.PopupLayer.Add(window.ScrimPopup);
        }
        _host.PopupLayer.Remove(window.HostPopup);
        _host.PopupLayer.Add(window.HostPopup);
    }

    // Stable partition: unpinned windows first, pinned last (pinned always on top), each group keeping its relative order.
    private void Normalize()
    {
        var reordered = new List<OverlayWindow>(_windows.Count);
        foreach (var w in _windows) if (!w.IsPinned) reordered.Add(w);
        foreach (var w in _windows) if (w.IsPinned) reordered.Add(w);
        _windows.Clear();
        _windows.AddRange(reordered);
    }

    // Each new window steps down-right from the last so it doesn't open exactly on top; wrapped so a long run stays on
    // screen (ComputePosition clamps every window inside the parent anyway).
    private static Vector2 CascadeOffset(int index)
    {
        const float step = 32f;
        var i = index % 6;
        return new Vector2(i * step, i * step);
    }

    private void EnsureKeyHook()
    {
        if (_keyHooked || _host is not InputUIComponent input) return;
        input.KeyDown += OnHostKeyDown;
        _keyHooked = true;
    }

    private void RemoveKeyHook()
    {
        if (!_keyHooked || _host is not InputUIComponent input) return;
        input.KeyDown -= OnHostKeyDown;
        _keyHooked = false;
    }

    private void OnHostKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        var active = ActiveWindow;
        if (active is { CloseByEscape: true, CanClose: true })
        {
            e.Handled = true;
            active.Close();
        }
    }
}
