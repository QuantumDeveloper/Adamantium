using System;

namespace Adamantium.Navigation;

/// <summary>Lifecycle a view model opts into to be shown as a floating in-window <c>OverlayWindow</c> (a window, NOT a
/// modal dialog - see <see cref="IOverlayAware"/> vs <see cref="IDialogAware"/>): it supplies a title, is told when it
/// opens, and closes itself (with a result) by raising <see cref="RequestClose"/>.</summary>
public interface IOverlayAware
{
    /// <summary>Shown on the overlay window's title bar. May change dynamically - raise PropertyChanged (INotifyPropertyChanged)
    /// and the bar updates live; get-only here because the host only reads it (the view model owns how it is stored).</summary>
    string Title { get; }

    /// <summary>Optional title-bar icon (any content the title bar's icon slot can present - a glyph, a Path, a small
    /// control). Null (default) shows no icon.</summary>
    object Icon => null;

    // Presentation options the overlay window is created with. Default interface members - a view model overrides only the
    // ones that differ from the default (a plain floating, movable, resizable, pinnable, closable, non-modal window).
    bool AllowMove => true;
    bool CanResize => true;
    bool CanPin => true;
    bool CanClose => true;
    bool IsModal => false;

    /// <summary>Where the window first appears. Default <see cref="OverlayStartupLocation.CenterOwner"/>; use
    /// <see cref="OverlayStartupLocation.Manual"/> to open it at <see cref="Left"/>/<see cref="Top"/>.</summary>
    OverlayStartupLocation StartupLocation => OverlayStartupLocation.CenterOwner;

    /// <summary>The window's absolute left/top, in the parent window's coordinates (like <c>Window.Left</c>/<c>Window.Top</c>).
    /// TWO-WAY bound to the window: assign it (raise PropertyChanged) and the window moves; drag the window and it updates.
    /// Used as the opening position when <see cref="StartupLocation"/> is Manual. Override with settable, notifying
    /// properties to use them (the defaults here are inert).</summary>
    double Left { get => 0; set { } }
    double Top { get => 0; set { } }

    void OnOverlayOpened(NavigationParameters parameters);

    /// <summary>Raise to close the overlay with a result. The x button / Escape also close it (with a null result).</summary>
    event Action<object> RequestClose;
}
