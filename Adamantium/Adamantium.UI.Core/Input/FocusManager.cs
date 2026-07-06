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

         if (element != null && Focused != element)
         {
            // Use a FRESH args per event: RaiseEvent only sets Source/OriginalSource when they are null (`??= this`), so
            // reusing one args object across LostFocus then GotFocus made GotFocus carry the LOST element as its
            // OriginalSource. OnGotFocus derives IsFocused from `OriginalSource == this`, so the newly-focused element
            // never lit up (no caret, no focus visual). One args each -> each carries its own raiser.
            Focused?.RaiseEvent(new RoutedEventArgs(LostFocusEvent));

            Focus(element);

            Focused.RaiseEvent(new RoutedEventArgs(GotFocusEvent));
         }
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

   public static bool Focus(IInputComponent component, NavigationMethod navigationMethod = NavigationMethod.Unspecified,
      InputModifiers modifiers = InputModifiers.None)
   {
      if (component != null)
      {
         var scope = GetFocusScopeAncestors(component).FirstOrDefault();
         Focused = component;
         lastFocused = component;
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