using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// Takes a tab dragged off its strip and gives it a window of its own. This lives in the APPLICATION, not in
/// TabControl, because what a torn-off tab deserves - chrome, size, whether it keeps a tab strip - is the
/// application's answer, and a docking host will answer it differently. The control only reports the gesture.
/// </summary>
public class TabTearOffBehavior : Behavior<TabControl>
{
    private TabControl _tabs;

    protected override void OnAttached(TabControl tabs)
    {
        _tabs = tabs;
        tabs.TabTornOff += OnTornOff;
    }

    protected override void OnDetached(TabControl tabs)
    {
        tabs.TabTornOff -= OnTornOff;
        _tabs = null;
    }
    private void OnTornOff(object sender, TabTearOffEventArgs e)
    {
        var items = _tabs?.ItemsSource as System.Collections.IList;
        if (items == null || !items.Contains(e.Item)) return;

        items.Remove(e.Item);

        var window = new Window
        {
            Title = (e.Item as ViewModels.TabPageViewModel)?.Header ?? "Tab",
            ClientWidth = 640,
            ClientHeight = 480,
            Left = e.ScreenPosition.X - 60,
            Top = e.ScreenPosition.Y - 20
        };

        // Showing a window belongs to the UI thread (Window.Show verifies it) while the tear-off is detected on the LOOP
        // thread, where input and the visual tree live - so the window is asked for across that seam. InvokeAsync, not
        // Invoke: a blocking hop into the pump would stall the frame the gesture is still running in.
        // Show first (it attaches the window and creates the OS one), THEN the content, or the subtree never joins a
        // live, themed window. A Window IS a ContentControl, so handing it the ITEM plus the strip's own selector makes
        // it render exactly what the tab body rendered - nothing is built by hand.
        UIAppContext.Current.Dispatcher.InvokeAsync(() =>
        {
            window.ContentTemplate = _tabs.ContentTemplate;
            window.ContentTemplateSelector = _tabs.ContentTemplateSelector;
            window.Content = e.Item;
            window.Show();

            // Put the CAPTION under the cursor, not the window's corner: the pointer holds the window by its title bar,
            // so it belongs halfway down it. The window states its own caption height, so no template part is measured
            // here and a restyle cannot drift from it.
            //
            // The cursor is read HERE, live. e.ScreenPosition is where it was when the threshold was crossed, and this
            // runs later - on the UI thread, after the window was built - by which time the pointer has moved on (~27px
            // in a measured run). Aiming at the old position is what put the cursor below the caption instead of on it.
            var cursor = Adamantium.UI.Core.Input.Mouse.ScreenCoordinates;
            window.Left = cursor.X - window.ClientWidth / 2;
            window.Top = cursor.Y - window.TitleBarHeight / 2;

            // Hand the still-held mouse button to the OS window-move loop: the window rides under the cursor until the
            // button comes up, WITH Aero Snap and edge snapping, because it is the platform's own move - not a position
            // we recompute per mouse event. This is what makes the tear-off one continuous gesture.
            window.DragMove();
        });

        // The strip keeps following the pointer for it: the window rides under the cursor until the button comes up.
        e.TornWindow = window;

        e.Handled = true;   // taken - the strip must not put it back
    }
}
