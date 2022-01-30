using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Input;

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
            element = element.GetSelfAndVisualAncestors().OfType<IInputComponent>().FirstOrDefault();
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
         lastFocued = component;
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

   private static IInputComponent lastFocued;

   public static bool TryRestoreFocus(IInputComponent scope)
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