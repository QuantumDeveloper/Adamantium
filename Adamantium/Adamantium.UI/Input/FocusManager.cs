using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.UI.Input
{
   public static class FocusManager
   {
      public static readonly RoutedEvent GotFocusEvent = EventManager.RegisterRoutedEvent("GotFocus",
         RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FocusManager));

      public static readonly RoutedEvent LostFocusEvent = EventManager.RegisterRoutedEvent("LostFocus",
         RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FocusManager));

      public static IInputElement Focused { get; private set; }

      public static IInputElement Scope { get; private set; }

      private static Dictionary<IInputElement, IInputElement> focusScopes = new Dictionary<IInputElement, IInputElement>();

      static FocusManager()
      {
         Mouse.PreviewMouseDownEvent.RegisterClassHandler<IInputElement>(new MouseButtonEventHandler(OnPreviewMouseDown));
      }

      private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
      {
         if (e.OriginalSource == e.Source)
         {
            var element = e.OriginalSource as IInputElement;
            if (element != null && !CanFocus(element))
            {
               element = element.GetSelfAndVisualAncestors().OfType<IInputElement>().FirstOrDefault();
            }

            if (element != null && Focused != element)
            {
               RoutedEventArgs args = new RoutedEventArgs(LostFocusEvent);
               Focused?.RaiseEvent(args);

               Focus(element);

               args.RoutedEvent = GotFocusEvent;
               Focused.RaiseEvent(args);
            }
         }
      }

      internal static void SetFocusScope(IInputElement scope)
      {
         if (scope == null)
         {
            throw new ArgumentNullException(nameof(scope));
         }

         IInputElement inputElement = null;

         if (!focusScopes.ContainsKey(scope))
         {
            inputElement = FindFirstFocusableInScope(scope);
            focusScopes.Add(scope, inputElement);
         }
         else
         {
            Scope = scope;
            inputElement = focusScopes[scope];
         }
         
         Focus(inputElement);
      }

      private static IEnumerable<IInputElement> GetFocusScopeAncestors(IInputElement scope)
      {
         var inputList = scope.GetSelfAndVisualAncestors().OfType<IInputElement>();
         foreach (var inputElement in inputList)
         {
            if (CanFocus(inputElement))
            {
               yield return inputElement;
            }
         }
      }

      private static IInputElement FindFirstFocusableInScope(IInputElement scope)
      {
         var inputList = scope.GetSelfAndVisualAncestors().OfType<IInputElement>();
         foreach (var inputElement in inputList)
         {
            if (CanFocus(inputElement))
            {
               return inputElement;
            }
         }
         return null;
      }

      public static Boolean CanFocus(IInputElement inputElement)
      {
         return inputElement != null && inputElement.IsEnabled && inputElement.Visibility == Visibility.Visible &&
                inputElement.Focusable;
      }

      public static void ResetFocus()
      {
         Focused = null;
      }

      public static bool Focus(IInputElement element, NavigationMethod navigationMethod = NavigationMethod.Unspecified,
         InputModifiers modifiers = InputModifiers.None)
      {
         if (element != null)
         {
            var scope = GetFocusScopeAncestors(element).FirstOrDefault();
            Focused = element;
            lastFocued = element;
            if (scope != null)
            {
               Scope = scope;
               SetFocusedElement(element, scope, navigationMethod, modifiers);
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

      private static IInputElement lastFocued;

      public static bool TryRestoreFocus(IInputElement scope)
      {
         if (lastFocued != null)
         {
            Focus(lastFocued);
            return true;
         }
         else
         {
            SetFocusScope(scope);
            return false;
         }
      }

      public static void SetFocusedElement(IInputElement element, IInputElement scope,
         NavigationMethod navigationMethod = NavigationMethod.Unspecified,
         InputModifiers modifiers = InputModifiers.None)
      {
         if (scope == null)
         {
            throw new ArgumentNullException(nameof(scope));
         }

         focusScopes[scope] = element;

         if (Scope == scope)
         {
            Application.Current.KeyboardDevice.SetFocusedElement(element, navigationMethod, modifiers);
         }
      }
   }
}
