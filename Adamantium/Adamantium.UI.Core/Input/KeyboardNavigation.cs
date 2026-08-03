using System.Linq;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// Moves the focus with the keyboard: Tab/Shift+Tab through the tab order, the arrow keys through a panel's own layout.
/// The route is not computed here - it is ASKED of the panels (<see cref="INavigablePanel"/>), and this class only walks
/// outward until one of them answers.
/// </summary>
public static class KeyboardNavigation
{
    /// <summary>Whether Tab stops on this element. Attached, so it can be turned off on a template part without that
    /// part's control knowing anything about navigation (the arrow keys ignore it - they are a layout move, not an
    /// order, and WPF draws the same line).</summary>
    public static readonly AdamantiumProperty IsTabStopProperty = AdamantiumProperty.RegisterAttached(
        "IsTabStop", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(true));

    public static bool GetIsTabStop(IAdamantiumComponent element) => element.GetValue<bool>(IsTabStopProperty);

    public static void SetIsTabStop(IAdamantiumComponent element, bool value) => element.SetValue(IsTabStopProperty, value);

    /// <summary>How Tab treats the inside of this element - see <see cref="KeyboardNavigationMode"/>.</summary>
    public static readonly AdamantiumProperty TabNavigationProperty = AdamantiumProperty.RegisterAttached(
        "TabNavigation", typeof(KeyboardNavigationMode), typeof(AdamantiumComponent),
        new PropertyMetadata(KeyboardNavigationMode.Continue));

    public static KeyboardNavigationMode GetTabNavigation(IAdamantiumComponent element) =>
        element.GetValue<KeyboardNavigationMode>(TabNavigationProperty);

    public static void SetTabNavigation(IAdamantiumComponent element, KeyboardNavigationMode value) =>
        element.SetValue(TabNavigationProperty, value);

    /// <summary>How the ARROW keys treat this element's edge. <see cref="KeyboardNavigationMode.Contained"/> keeps them
    /// inside it - see the mode's own note for why a field of tiles wants that.</summary>
    public static readonly AdamantiumProperty DirectionalNavigationProperty = AdamantiumProperty.RegisterAttached(
        "DirectionalNavigation", typeof(KeyboardNavigationMode), typeof(AdamantiumComponent),
        new PropertyMetadata(KeyboardNavigationMode.Continue));

    public static KeyboardNavigationMode GetDirectionalNavigation(IAdamantiumComponent element) =>
        element.GetValue<KeyboardNavigationMode>(DirectionalNavigationProperty);

    public static void SetDirectionalNavigation(IAdamantiumComponent element, KeyboardNavigationMode value) =>
        element.SetValue(DirectionalNavigationProperty, value);

    private static bool _registered;

    /// <summary>Hooks the key handler. A static class registers nothing until something touches it, so this is called
    /// from the UI element base's static constructor - the one thing guaranteed to run before any element exists.</summary>
    public static void Register()
    {
        if (_registered) return;

        _registered = true;
        Keyboard.KeyDownEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(OnKeyDown));
    }

    /// <summary>Moves the focus one step. Public because navigation is also something an app asks for directly (a
    /// wizard's Next button moving into the first field of the page it just showed).</summary>
    public static bool Move(FocusNavigationDirection direction)
    {
        if (FocusManager.Focused is not { } current) return false;

        var target = Find(current, direction);
        if (target == null || ReferenceEquals(target, current)) return false;

        FocusManager.Focus(target, IsTabbing(direction) ? NavigationMethod.Tab : NavigationMethod.Directional);
        return true;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        // At the END of the bubble, never on the focused element. A class handler runs BEFORE that element's own
        // handlers, so deciding early would take the arrow key away from the caret or the slider value that was about to
        // use it. By the time the event reaches the root, everything in the path has had its say - and navigation is
        // what is left over.
        if (e.Handled || sender is not IObservableComponent { ObservableParent: null }) return;

        var direction = DirectionOf(e.Key);
        if (direction == null) return;
        if (Move(direction.Value)) e.Handled = true;
    }

    private static FocusNavigationDirection? DirectionOf(Key key) => key switch
    {
        Key.Tab => IsShiftDown ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next,
        Key.LeftArrow => FocusNavigationDirection.Left,
        Key.RightArrow => FocusNavigationDirection.Right,
        Key.UpArrow => FocusNavigationDirection.Up,
        Key.DownArrow => FocusNavigationDirection.Down,
        _ => null
    };

    private static bool IsShiftDown =>
        (Keyboard.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;

    private static bool IsTabbing(FocusNavigationDirection direction) =>
        direction is FocusNavigationDirection.Next or FocusNavigationDirection.Previous;

    private static bool IsBackwards(FocusNavigationDirection direction) =>
        direction is FocusNavigationDirection.Previous or FocusNavigationDirection.Up or FocusNavigationDirection.Left;

    // Walk OUTWARD, asking each panel on the way about the child we came out of. A panel that answers null has simply
    // run out of room in that direction, so the question passes to the panel above - which is what carries a Tab off the
    // end of one stack into the next one without either stack knowing the other exists.
    private static IInputComponent Find(IInputComponent from, FocusNavigationDirection direction)
    {
        // Tab LEAVES a "once" container whole: start the outward walk from the container itself, not from the item
        // inside it, so the next stop is what follows the list rather than its second row. The arrows are unaffected -
        // moving between the items is exactly what they are for.
        IUIComponent child = IsTabbing(direction) ? OutermostSingleEntry(from) : from;
        for (var node = child.VisualParent; node != null; child = node, node = node.VisualParent)
        {
            if (node is not INavigablePanel panel) continue;

            // A candidate with nothing focusable inside is not a dead end - keep asking the same panel for the one
            // after it, or an empty container would stop the Tab where a user sees only a gap.
            for (var candidate = panel.Navigate(child, direction);
                 candidate != null;
                 candidate = panel.Navigate(candidate, direction))
            {
                if (FirstStop(candidate, direction) is { } stop) return stop;
            }

            // An arrow does not climb out of a container that keeps them: at the edge of a field of tiles the key does
            // nothing at all, instead of throwing the focus onto whatever happens to sit beside the panel. One arrow
            // too many is otherwise enough to lose your place, and getting back in takes a mouse.
            if (!IsTabbing(direction) && GetDirectionalNavigation(node) == KeyboardNavigationMode.Contained) return null;
        }

        // Off the end of the tree: Tab comes round again, an arrow key stops at the edge.
        return IsTabbing(direction) ? Wrap(from, direction) : null;
    }

    /// <summary>The outermost ancestor that Tab enters only once (a list), or the element itself when there is none.</summary>
    private static IUIComponent OutermostSingleEntry(IUIComponent from)
    {
        var outermost = from;
        for (IUIComponent node = from.VisualParent; node != null; node = node.VisualParent)
        {
            if (GetTabNavigation(node) == KeyboardNavigationMode.Once) outermost = node;
        }

        return outermost;
    }

    private static IInputComponent Wrap(IInputComponent from, FocusNavigationDirection direction)
    {
        IUIComponent root = from;
        while (root.VisualParent is { } parent) root = parent;
        return FirstStop(root, direction);
    }

    /// <summary>The first place the focus can land in this subtree - the last one when moving backwards, so Shift+Tab
    /// enters a container from its end rather than its start.</summary>
    private static IInputComponent FirstStop(IUIComponent node, FocusNavigationDirection direction)
    {
        if (node == null || node.Visibility != Visibility.Visible) return null;

        var backwards = IsBackwards(direction);
        if (!backwards && IsStop(node, direction)) return (IInputComponent)node;

        var children = node.VisualChildren;
        if (children != null)
        {
            var ordered = backwards ? children.Reverse() : children;
            foreach (var candidate in ordered)
            {
                if (FirstStop(candidate, direction) is { } stop) return stop;
            }
        }

        return backwards && IsStop(node, direction) ? (IInputComponent)node : null;
    }

    private static bool IsStop(IUIComponent node, FocusNavigationDirection direction) =>
        node is IInputComponent input && FocusManager.CanFocus(input) &&
        (!IsTabbing(direction) || GetIsTabStop(input));
}
