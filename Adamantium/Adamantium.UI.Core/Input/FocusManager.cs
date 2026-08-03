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

   internal static void SetFocusScope(IInputComponent scope)
   {
      if (scope == null)
      {
         throw new ArgumentNullException(nameof(scope));
      }

      IInputComponent inputComponent = null;

      if (!focusScopes.ContainsKey(scope))
      {
         inputComponent = FindFirstFocusableInScope(scope);
         focusScopes.Add(scope, inputComponent);
      }
      else
      {
         Scope = scope;
         inputComponent = focusScopes[scope];
      }
         
      Focus(inputComponent);
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

   private static IInputComponent FindFirstFocusableInScope(IInputComponent scope)
   {
      var inputList = scope.GetSelfAndVisualAncestors().OfType<IInputComponent>();
      foreach (var inputElement in inputList)
      {
         if (CanFocus(inputElement))
         {
            return inputElement;
         }
      }
      return null;
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
      if (element == null || !ReferenceEquals(Focused, element))
         return;

      var previous = Focused;
      Focused = null;
      AnnounceFocusMove(previous, null, NavigationMethod.Unspecified);
   }

   public static bool Focus(IInputComponent component, NavigationMethod navigationMethod = NavigationMethod.Unspecified,
      InputModifiers modifiers = InputModifiers.None)
   {
      if (component != null)
      {
         if (ReferenceEquals(Focused, component)) return true;

         var scope = GetFocusScopeAncestors(component).FirstOrDefault();
         var previous = Focused;
         Focused = component;
         lastFocused = component;

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

   private static IInputComponent lastFocused;

   public static bool TryRestoreFocus(IInputComponent scope)
   {
      if (lastFocused != null)
      {
         Focus(lastFocused);
         return true;
      }
      else
      {
         SetFocusScope(scope);
         return false;
      }
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