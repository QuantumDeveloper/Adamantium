using System.Linq;
using System.Runtime.CompilerServices;
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

    /// <summary>Where this element comes in the tab order among its siblings. Lower goes first; equal indices keep the
    /// order they stand in - which is why the default is the LARGEST value there is: numbering two fields puts exactly
    /// those two first and leaves everything else where it was, instead of forcing a number onto every control on the
    /// form. Scoped to the container, as the traversal is: a panel's own index orders the panel among ITS siblings.</summary>
    public static readonly AdamantiumProperty TabIndexProperty = AdamantiumProperty.RegisterAttached(
        "TabIndex", typeof(int), typeof(AdamantiumComponent), new PropertyMetadata(int.MaxValue));

    public static int GetTabIndex(IAdamantiumComponent element) => element.GetValue<int>(TabIndexProperty);

    public static void SetTabIndex(IAdamantiumComponent element, int value) => element.SetValue(TabIndexProperty, value);

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

    /// <summary>Declares this element a focus AREA: a whole region of the interface Ctrl+Tab steps between, the way Tab
    /// steps between controls. Coming back to an area comes back to the place in it the keyboard left, so a Ctrl+Tab out
    /// and back is not a way to lose your place. An overlay window declares itself one, and so does a window's content -
    /// which is what lets the keyboard leave a non-modal overlay for the page behind it and return.</summary>
    public static readonly AdamantiumProperty IsFocusAreaProperty = AdamantiumProperty.RegisterAttached(
        "IsFocusArea", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false, OnIsFocusAreaChanged));

    public static bool GetIsFocusArea(IAdamantiumComponent element) => element.GetValue<bool>(IsFocusAreaProperty);

    public static void SetIsFocusArea(IAdamantiumComponent element, bool value) =>
        element.SetValue(IsFocusAreaProperty, value);

    // The declared areas, in the order they were declared. A LIST, not a tree walk: an overlay window is hosted on the
    // popup layer, detached from the window's visual tree, so there is no one tree to walk that has them all. Declaration
    // order is open order for overlays (the stack) and creation order for anything in a layout - the reading order in
    // both cases. Weak, so declaring an area never keeps a closed window alive.
    private static readonly List<WeakReference<IUIComponent>> Areas = [];

    private static void OnIsFocusAreaChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not IUIComponent area) return;

        lock (Areas)
        {
            Areas.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, area));
            if (e.NewValue is true) Areas.Add(new WeakReference<IUIComponent>(area));
        }
    }

    private static bool _registered;

    /// <summary>Hooks the key handler. A static class registers nothing until something touches it, so this is called
    /// from the UI element base's static constructor - the one thing guaranteed to run before any element exists.</summary>
    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        Keyboard.KeyDownEvent.RegisterClassHandler<IInputComponent>(new KeyEventHandler(OnKeyDown));
        FocusManager.GotFocusEvent.RegisterClassHandler<IInputComponent>(new RoutedEventHandler(OnGotFocus));
    }

    /// <summary>Where the focus was inside each container that Tab enters ONCE. A list is one stop in the order, so
    /// coming back to it has to come back to the ROW you left - landing on the last row instead throws away the place
    /// you were keeping, and in a long list that place is the whole point. Weak keys: remembering a row must not keep a
    /// closed dialog's tree alive.</summary>
    private static readonly ConditionalWeakTable<IUIComponent, IInputComponent> LastEntered = new();

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        // A bubbling event runs this at every node on the way up; the element that GOT the focus is the original source.
        if (!ReferenceEquals(sender, e.OriginalSource) || e.OriginalSource is not IInputComponent focused)
            return;

        // Areas remember their place for the same reason lists do - see LastEntered - so Ctrl+Tab away and back returns
        // to where the keyboard was rather than to the area's first control.
        for (IUIComponent node = focused.VisualParent; node != null; node = node.VisualParent)
        {
            if (GetTabNavigation(node) != KeyboardNavigationMode.Once && !GetIsFocusArea(node))
                continue;

            LastEntered.Remove(node);
            LastEntered.Add(node, focused);
        }
    }

    /// <summary>The place inside <paramref name="container"/> the focus left, when it can still be gone back to.</summary>
    private static IInputComponent RememberedStop(IUIComponent container, FocusNavigationDirection direction)
    {
        if (!IsTabbing(direction) || GetTabNavigation(container) != KeyboardNavigationMode.Once)
            return null;

        if (!LastEntered.TryGetValue(container, out var last) || !IsStop(last, direction))
            return null;

        // Still inside it: a virtualized row can be detached, or moved to another list entirely, since it was left.
        for (IUIComponent node = last; node != null; node = node.VisualParent)
        {
            if (ReferenceEquals(node, container))
                return last;

            if (node.Visibility != Visibility.Visible)
                return null;
        }

        return null;
    }

    /// <summary>Moves the focus one step. Public because navigation is also something an app asks for directly (a
    /// wizard's Next button moving into the first field of the page it just showed).</summary>
    public static bool Move(FocusNavigationDirection direction)
    {
        if (FocusManager.Focused is not { } current)
            return false;

        var target = Find(current, direction);
        if (target == null || ReferenceEquals(target, current))
            return false;

        FocusManager.Focus(target, IsTabbing(direction) ? NavigationMethod.Tab : NavigationMethod.Directional);
        return true;
    }

    /// <summary>Steps the focus to the next declared focus AREA (Ctrl+Tab), or the previous one (Ctrl+Shift+Tab). It
    /// lands on the place the keyboard left in that area, or its first stop if it has never been in it. False when there
    /// is nowhere to go: fewer than two areas can take the focus, or the keyboard is held inside a modal.</summary>
    public static bool MoveToArea(bool backwards)
    {
        if (FocusManager.Focused is not { } current)
            return false;

        // A modal keeps the keyboard, and it says so the same way it says it to Tab. Everything outside it is dimmed and
        // unclickable, so an area step out of it would put the focus where the mouse cannot follow.
        for (IUIComponent node = current; node != null; node = node.VisualParent)
        {
            if (GetTabNavigation(node) == KeyboardNavigationMode.Cycle)
                return false;
        }

        var areas = LiveAreas(current);
        if (areas.Count < 2)
            return false;

        var here = AreaOf(current);
        var from = here == null ? (backwards ? 0 : -1) : areas.IndexOf(here);
        var step = backwards ? -1 : 1;

        // Past an area with nothing to focus in it, rather than stopping there - an empty one is not a destination.
        for (var i = 1; i <= areas.Count; i++)
        {
            var area = areas[((from + i * step) % areas.Count + areas.Count) % areas.Count];
            if (ReferenceEquals(area, here))
                continue;

            var stop = LastEntered.TryGetValue(area, out var last) && IsStop(last, FocusNavigationDirection.Next)
                ? last
                : FirstStop(area, FocusNavigationDirection.Next);
            if (stop == null)
                continue;

            FocusManager.Focus(stop, NavigationMethod.Tab);
            return true;
        }

        return false;
    }

    /// <summary>How to find the window an element belongs to when it is hosted DETACHED - an overlay's content has no
    /// visual path back to the window it is shown in. Only the popup layer knows the way, so the control layer installs
    /// this once; without it an area is placed by its visual root alone.</summary>
    public static Func<IUIComponent, object> HostOf { get; set; }

    // Which window an element belongs to: the one that hosts it if it is on an overlay, otherwise its own visual root.
    private static object WindowOf(IUIComponent node) => HostOf?.Invoke(node) ?? RootOf(node);

    /// <summary>The declared areas that can be stepped to from <paramref name="from"/>, in declaration order: on screen,
    /// and belonging to the SAME window. A window keeps its areas until they are collected, and a closed one's are as
    /// declared as any other - stepping into those would send the keyboard to a window nobody is looking at.</summary>
    private static List<IUIComponent> LiveAreas(IUIComponent from)
    {
        var window = WindowOf(from);
        var live = new List<IUIComponent>();
        lock (Areas)
        {
            Areas.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var weak in Areas)
            {
                if (!weak.TryGetTarget(out var area) || area.Visibility != Visibility.Visible) continue;
                if (Equals(WindowOf(area), window)) live.Add(area);
            }
        }

        return live;
    }

    /// <summary>The innermost area <paramref name="node"/> sits in, or null when it is in none.</summary>
    private static IUIComponent AreaOf(IUIComponent node)
    {
        for (; node != null; node = node.VisualParent)
        {
            if (GetIsFocusArea(node)) return node;
        }

        return null;
    }

    /// <summary>Steps the focus INTO <paramref name="container"/> - onto the first place inside it the focus can land.
    /// False when there is nowhere: a container whose content has not been built yet answers exactly that, which is how
    /// a caller knows to ask again once it has.
    /// <para>What "open this" means for a container that is not part of the tab order on its own - a tab's page, a
    /// wizard's next step - where the step in is a decision, not a Tab away.</para></summary>
    public static bool MoveInto(IUIComponent container, NavigationMethod method = NavigationMethod.Tab)
    {
        if (FirstStop(container, FocusNavigationDirection.Next) is not { } stop)
            return false;

        FocusManager.Focus(stop, method);
        return true;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        // At the END of the bubble, never on the focused element. A class handler runs BEFORE that element's own
        // handlers, so deciding early would take the arrow key away from the caret or the slider value that was about to
        // use it. By the time the event reaches the root, everything in the path has had its say - and navigation is
        // what is left over.
        if (e.Handled || sender is not IObservableComponent { ObservableParent: null })
            return;

        // Ctrl+Tab steps between AREAS, not between controls - so it is answered before the ordinary tab order, and it is
        // answered even when the focus is in another tree entirely (an overlay window is hosted detached, so the root
        // check below would refuse it).
        if (e.Key == Key.Tab && IsControlDown)
        {
            if (MoveToArea(IsShiftDown)) e.Handled = true;
            return;
        }

        var direction = DirectionOf(e.Key);
        if (direction == null)
            return;

        // Nothing focused yet: the first Tab ENTERS the window, at its first stop (its last, going backwards). Without
        // this the keyboard does nothing at all until something has been clicked - which is not a keyboard-operable
        // application, and it is the state every window starts in.
        if (FocusManager.Focused == null)
        {
            if (!IsTabbing(direction.Value) || sender is not IUIComponent root)
                return;

            if (FirstStop(root, direction.Value) is not { } first)
                return;

            FocusManager.Focus(first, NavigationMethod.Tab);
            e.Handled = true;
            return;
        }

        // The focus has to be in the tree this key arrived in. It is one global focus and one handler per route root, so
        // without this a key delivered to one window would move the focus inside another - and a control that owns its
        // arrows loses them to a navigation step it never saw coming.
        if (!ReferenceEquals(RootOf(FocusManager.Focused), sender))
            return;

        if (Move(direction.Value))
            e.Handled = true;
    }

    private static IUIComponent RootOf(IUIComponent component)
    {
        while (component?.VisualParent is { } parent)
            component = parent;

        return component;
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

    private static bool IsControlDown =>
        (Keyboard.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;

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
            if (node is INavigablePanel panel)
            {
                // A candidate with nothing focusable inside is not a dead end - keep asking the same panel for the one
                // after it, or an empty container would stop the Tab where a user sees only a gap.
                for (var candidate = panel.Navigate(child, direction);
                     candidate != null;
                     candidate = panel.Navigate(candidate, direction))
                {
                    if (FirstStop(candidate, direction) is { } stop)
                        return stop;
                }

                // An arrow does not climb out of a container that keeps them: at the edge of a field of tiles the key
                // does nothing at all, instead of throwing the focus onto whatever happens to sit beside the panel. One
                // arrow too many is otherwise enough to lose your place, and getting back in takes a mouse.
                if (!IsTabbing(direction) && GetDirectionalNavigation(node) == KeyboardNavigationMode.Contained)
                    return null;
            }

            // Nor does TAB climb out of a cycle - a modal dialog or an overlay. Past its last stop the walk comes round
            // to its first, so the keyboard stays where the mouse is confined too. Asked of EVERY node on the way out,
            // not only of panels: what carries the cycle is usually the modal's chrome (a Border, a card), not a panel.
            if (IsTabbing(direction) && GetTabNavigation(node) == KeyboardNavigationMode.Cycle)
                return FirstStop(node, direction);
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
            if (GetTabNavigation(node) == KeyboardNavigationMode.Once)
                outermost = node;
        }

        return outermost;
    }

    private static IInputComponent Wrap(IInputComponent from, FocusNavigationDirection direction)
    {
        // Round again inside the innermost CYCLE this element sits in - a modal dialog or an overlay - and only failing
        // that around the whole tree. Wrapping to the tree root regardless would walk a modal's Tab straight out into
        // the page behind it, which is the one place the keyboard must not go.
        IUIComponent root = from;
        for (IUIComponent node = from; node != null; node = node.VisualParent)
        {
            root = node;
            if (GetTabNavigation(node) == KeyboardNavigationMode.Cycle) break;
        }

        return FirstStop(root, direction);
    }

    /// <summary>The first place the focus can land in this subtree - the last one when moving backwards, so Shift+Tab
    /// enters a container from its end rather than its start.</summary>
    private static IInputComponent FirstStop(IUIComponent node, FocusNavigationDirection direction)
    {
        if (node == null || node.Visibility != Visibility.Visible)
            return null;

        if (RememberedStop(node, direction) is { } remembered)
            return remembered;

        var backwards = IsBackwards(direction);
        if (!backwards && IsStop(node, direction))
            return (IInputComponent)node;

        foreach (var candidate in EntryOrder(node, direction))
        {
            if (FirstStop(candidate, direction) is { } stop)
                return stop;
        }

        return backwards && IsStop(node, direction) ? (IInputComponent)node : null;
    }

    /// <summary>The children of <paramref name="node"/> in the order the way IN should try them: the panel's own TAB
    /// order when it has one, and the order they stand in otherwise (which is what the arrows want - they are a question
    /// about the layout, not about an order someone numbered).
    /// <para>Without this the step INTO a container landed on the first child in the tree while every step after it
    /// followed the numbering - so a form numbered 3 1 4 2 was entered at 3 and then walked 1, 4, 2. The way in has to
    /// be where Tab would have taken you first.</para></summary>
    private static IEnumerable<IUIComponent> EntryOrder(IUIComponent node, FocusNavigationDirection direction)
    {
        var children = node.VisualChildren;
        if (children == null)
            return [];

        var backwards = IsBackwards(direction);
        if (node is not INavigablePanel panel || !IsTabbing(direction))
            return backwards ? children.Reverse() : children;

        // Walk to the end the search comes FROM by asking the panel for the step BACK, then read its whole order off by
        // asking for the step forward - the panel stays the one that knows what its order is.
        IUIComponent first = null;
        foreach (var child in children)
        {
            first = child;
            if (!backwards)
                break;
        }

        if (first == null)
            return [];

        var back = backwards ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous;
        var forward = backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;

        for (var guard = children.Count; guard > 0 && panel.Navigate(first, back) is { } previous; guard--)
            first = previous;

        var ordered = new List<IUIComponent>(children.Count);
        for (var c = first; c != null && ordered.Count < children.Count; c = panel.Navigate(c, forward))
            ordered.Add(c);

        return ordered;
    }

    private static bool IsStop(IUIComponent node, FocusNavigationDirection direction) =>
        node is IInputComponent input && FocusManager.CanFocus(input) &&
        (!IsTabbing(direction) || GetIsTabStop(input));
}
