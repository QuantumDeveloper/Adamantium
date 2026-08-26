using System.Runtime.CompilerServices;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

public static class FocusManager
{
   public static readonly RoutedEvent GotFocusEvent = EventManager.RegisterRoutedEvent("GotFocus",
      RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FocusManager));

   public static readonly RoutedEvent LostFocusEvent = EventManager.RegisterRoutedEvent("LostFocus",
      RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FocusManager));

   public static IInputComponent Focused { get; private set; }

   public static IInputComponent Scope { get; private set; }

   private static Dictionary<IInputComponent, IInputComponent> focusScopes = new Dictionary<IInputComponent, IInputComponent>();

   static FocusManager()
   {
      Mouse.PreviewMouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(OnPreviewMouseDown));
   }

   private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
   {
      if (e.OriginalSource == e.Source)
      {
         var element = e.OriginalSource as IInputComponent;
         if (element != null && !CanFocus(element))
         {
            // Walk up to the first FOCUSABLE self-or-ancestor (skip non-focusable input parts like a TextBox's inner
            // TextPresenter). FirstOrDefault() without the predicate returns `element` itself (self is first), which is
            // exactly the non-focusable part we're trying to skip - so focus never reached the real control.
            element = element.GetSelfAndVisualAncestors().OfType<IInputComponent>().FirstOrDefault(CanFocus);
         }

         // A CLICK, so the move is announced by Focus itself (see AnnounceFocusMove) - including that this one must not
         // light a focus ring: the click already said where you are.
         if (element != null) Focus(element, NavigationMethod.Mouse);
      }
   }

   private static IEnumerable<IInputComponent> GetFocusScopeAncestors(IInputComponent scope)
   {
      var inputList = scope.GetSelfAndVisualAncestors().OfType<IInputComponent>();
      foreach (var inputElement in inputList)
      {
         if (CanFocus(inputElement))
         {
            yield return inputElement;
         }
      }
   }

   public static Boolean CanFocus(IInputComponent inputComponent)
   {
      return inputComponent != null && inputComponent.IsEnabled && inputComponent.Visibility == Visibility.Visible &&
             inputComponent.Focusable;
   }

   public static void ResetFocus()
   {
      Focused = null;
   }

   /// <summary>Gives up the focus if it is on <paramref name="element"/>, announcing the move so the ring goes out with
   /// it. What a control calls when it LEAVES the visual tree: the focus cannot stay on something that is no longer on
   /// screen - the keyboard would go on walking a tree nobody can see (measured: after a tab swap, Tab kept moving
   /// through the page that had just been replaced).</summary>
   public static void Release(IInputComponent element)
   {
      if (element == null)
         return;

      // THREE places remember the focus, and this used to clear one. The keyboard device keeps a pointer of its own,
      // assigned in SetFocusedElement and never once assigned null, on a SINGLETON that lives as long as the
      // application - so an element that left the tree stayed reachable for good, and with it every element its subtree
      // could reach through shared theme brushes and view models. Measured on the stand: one such DropDown retained
      // 128 MB of a 150 MB heap, which is the whole of the +20 MB a theme swap never gave back.
      var keyboard = KeyboardDevice.CurrentDevice;
      if (keyboard != null && ReferenceEquals(keyboard.FocusedComponent, element))
      {
         keyboard.SetFocusedElement(null);
      }

      // ...and the scope map, which is written on every focus move and was never read back out. Strong on BOTH sides,
      // so a departed element sat there as a key AND as somebody else's remembered element.
      DropFromScopes(element);

      if (!ReferenceEquals(Focused, element))
         return;

      var previous = Focused;
      Focused = null;
      AnnounceFocusMove(previous, null, NavigationMethod.Unspecified);
   }

   private static void DropFromScopes(IInputComponent element)
   {
      List<IInputComponent> stale = null;
      foreach (var pair in focusScopes)
      {
         if (ReferenceEquals(pair.Key, element) || ReferenceEquals(pair.Value, element))
            (stale ??= new List<IInputComponent>()).Add(pair.Key);
      }

      if (stale == null) return;
      foreach (var scope in stale) focusScopes.Remove(scope);
   }

   public static bool Focus(IInputComponent component, NavigationMethod navigationMethod = NavigationMethod.Unspecified,
      InputModifiers modifiers = InputModifiers.None)
   {
      if (component != null)
      {
         // Focusable is a REFUSAL, and it has to hold on every path into here - not just the mouse one, which was the
         // only caller that asked. Keyboard navigation, a control focusing itself and any programmatic Focus() could all
         // put the focus on an element that says it cannot take it; the element then reports IsFocused, and a template
         // that draws its focus ring on IsFocused drew one around, say, a caption button - a frame left sitting in the
         // window chrome after a click, on a control that is not a keyboard destination at all.
         if (!CanFocus(component)) return false;

         if (ReferenceEquals(Focused, component)) return true;

         var scope = GetFocusScopeAncestors(component).FirstOrDefault();
         var previous = Focused;
         Focused = component;
         Remember(component);   // per WINDOW, so switching away and back comes back HERE - see FocusByRoot

         // Announce the move HERE, not only on the mouse path. Focus set programmatically - which is every keyboard
         // move, and every control that focuses itself - used to change nothing visible at all, because the events that
         // drive IsFocused were raised by the mouse handler alone.
         AnnounceFocusMove(previous, component, navigationMethod);

         if (scope != null)
         {
            Scope = scope;
            SetFocusedElement(component, scope, navigationMethod, modifiers);
            return true;
         }
      }
      else if (Focused != null)
      {
         // If control is null, set focus to the topmost focus scope.
         foreach (var scope in GetFocusScopeAncestors(Focused).Reverse().ToList())
         {
            if (focusScopes.ContainsKey(scope))
            {
               Focus(focusScopes[scope], navigationMethod, modifiers);
            }
         }
      }
      return false;
   }

   /// <summary>True while the focus was last moved BY THE KEYBOARD. What a focus visual keys off: a ring that also
   /// appeared on every click is noise, since a click already says where you are.</summary>
   public static bool IsFocusVisible { get; private set; }

   // One place that tells everyone the focus moved: the two elements themselves, and every element that gained or lost
   // "the focus is somewhere inside me" (the same ancestor-chain machinery IsMouseOver runs on - a composite control has
   // to know the focus is in its editor without being the focused element itself).
   private static void AnnounceFocusMove(IInputComponent previous, IInputComponent current, NavigationMethod method)
   {
      IsFocusVisible = method is NavigationMethod.Tab or NavigationMethod.Directional;

      // A fresh args per raise: RaiseEvent only fills Source/OriginalSource when they are null, so one shared object
      // would carry the LOST element into the GOT event - and IsFocused is derived from exactly that.
      previous?.RaiseEvent(new RoutedEventArgs(LostFocusEvent));
      current?.RaiseEvent(new RoutedEventArgs(GotFocusEvent));

      AncestorState.Transition(previous, current,
         Keyboard.GotKeyboardFocusWithinEvent, Keyboard.LostKeyboardFocusWithinEvent,
         evt => new RoutedEventArgs(evt));
   }

   /// <summary>Where the focus was in each window. There is ONE focused element in the application - the same thing the
   /// OS means by focus - but "where it was" is a question each window answers for itself, and the answer has to survive
   /// the window being switched away from. A single global "last focused" answered it for the whole application, so
   /// activating a second window restored the FIRST window's element: the new window came up with the keyboard pointing
   /// somewhere else entirely, and navigation, which checks that the key arrived in the tree the focus is in, then
   /// ignored every keystroke in it. Weak keys: remembering a place must not keep a closed window's tree alive.</summary>
   private static readonly ConditionalWeakTable<IUIComponent, IInputComponent> FocusByRoot = new();

   private static IUIComponent RootOf(IUIComponent node)
   {
      while (node?.VisualParent is { } parent) node = parent;
      return node;
   }

   private static void Remember(IInputComponent element)
   {
      if (RootOf(element) is not { } root) return;
      FocusByRoot.Remove(root);
      FocusByRoot.Add(root, element);
   }

   /// <summary>Puts the focus back where it was in <paramref name="root"/> - what a window does when it is activated.
   /// False when this window has no place to go back to (it has never been focused, or what it remembers is gone), which
   /// is how the caller knows to enter it at its first stop instead.</summary>
   public static bool TryRestoreFocus(IInputComponent root)
   {
      if (root == null || !FocusByRoot.TryGetValue(root, out var remembered) || !CanFocus(remembered))
         return false;

      // It has to still be IN this window: the remembered element may have been removed with a closed tab, or moved to
      // another window entirely by a tear-off.
      if (!ReferenceEquals(RootOf(remembered), root))
         return false;

      return Focus(remembered);
   }

   /// <summary>The window is no longer active: remember where the focus was in it and let it go. Keeping it would leave
   /// the ring lit in a window the keyboard has left, and leave the ONE focused element pointing into a window that no
   /// longer receives keys - which is what stopped the newly activated window from responding at all.</summary>
   public static void LeaveWindow(IUIComponent root)
   {
      if (Focused == null || !ReferenceEquals(RootOf(Focused), root))
         return;

      Remember(Focused);
      Release(Focused);
   }

   public static void SetFocusedElement(IInputComponent component, IInputComponent scope,
      NavigationMethod navigationMethod = NavigationMethod.Unspecified,
      InputModifiers modifiers = InputModifiers.None)
   {
      if (scope == null)
      {
         throw new ArgumentNullException(nameof(scope));
      }

      focusScopes[scope] = component;

      if (Scope == scope)
      {
         KeyboardDevice.CurrentDevice.SetFocusedElement(component, navigationMethod, modifiers);
      }
   }
}