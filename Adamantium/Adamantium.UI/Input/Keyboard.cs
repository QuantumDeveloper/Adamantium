using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Input
{
   public static class Keyboard
   {
      public static readonly RoutedEvent KeyDownEvent = EventManager.RegisterRoutedEvent("KeyDown",
         RoutingStrategy.Bubble, typeof(KeyEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent KeyUpEvent = EventManager.RegisterRoutedEvent("KeyUp",
         RoutingStrategy.Bubble, typeof(KeyEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent GotKeyboardFocusEvent = EventManager.RegisterRoutedEvent("GotKeyboardFocus",
         RoutingStrategy.Bubble, typeof(KeyboardGotFocusEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent LostKeyboardFocusEvent = EventManager.RegisterRoutedEvent("LostKeyboardFocus",
         RoutingStrategy.Bubble, typeof(KeyboardFocusChangedEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewKeyDownEvent = EventManager.RegisterRoutedEvent("PreviewKeyDown",
         RoutingStrategy.Tunnel, typeof(KeyEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewKeyUpEvent = EventManager.RegisterRoutedEvent("PreviewKeyUp",
         RoutingStrategy.Tunnel, typeof(KeyEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewLostKeyboardFocusEvent = EventManager.RegisterRoutedEvent("PreviewLostKeyboardFocus",
         RoutingStrategy.Tunnel, typeof(KeyboardFocusChangedEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewGotKeyboardFocusEvent = EventManager.RegisterRoutedEvent("PreviewGotKeyboardFocus",
         RoutingStrategy.Tunnel, typeof(KeyboardGotFocusEventHandler), typeof(UIComponent));


      private static Stopwatch timer;
      private static Dictionary<Key, ButtonState> KeyStates = new Dictionary<Key, ButtonState>();

      public static KeyboardDevice PrimaryDevice { get; }

      static Keyboard()
      {
         PrimaryDevice = KeyboardDevice.CurrentDevice;
         timer = new Stopwatch();
         timer.Start();
      }

      internal static void AddKeyState(Key key)
      {
         lock (KeyStates)
         {
            if (!KeyStates.ContainsKey(key))
            {
               KeyStates.Add(key, new ButtonState(TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds)));
            }
         }
      }

      internal static void RemoveKeyState(Key key)
      {
         lock (KeyStates)
         {
            if (KeyStates.ContainsKey(key))
            {
               KeyStates.Remove(key);
            }
         }
      }

      public static Boolean IsKeyDown(Key key)
      {
         return PrimaryDevice.IsKeyDown(key);
      }

      public static Boolean IsKeyUp(Key key)
      {
         return PrimaryDevice.IsKeyUp(key);
      }

      public static Boolean IsKeyToggled(Key key)
      {
         return PrimaryDevice.IsKeyToggled(key);
      }

      //Only first check
      public static Boolean IsKeyPressed(Key key)
      {
         lock (KeyStates)
         {
            if (KeyStates.ContainsKey(key))
            {
               if (!KeyStates[key].IsKeyAlreadyChecked)
               {
                  var state = KeyStates[key];
                  state.IsKeyAlreadyChecked = true;
                  KeyStates[key] = state;
                  return true;
               }
            }
            return false;
         }
      }

      public static InputModifiers Modifiers
      {
         get
         {
            //UpdateKeyStates();
            InputModifiers result = 0;

            if (IsKeyDown(Key.LeftAlt))
            {
               result |= InputModifiers.LeftAlt;
            }

            if (IsKeyDown(Key.RightAlt))
            {
               result |= InputModifiers.RightAlt;
            }

            if (IsKeyDown(Key.LeftCtrl))
            {
               result |= InputModifiers.LeftControl;
            }

            if (IsKeyDown(Key.RightCtrl))
            {
               result |= InputModifiers.RightControl;
            }

            if (IsKeyDown(Key.LeftShift))
            {
               result |= InputModifiers.LeftShift;
            }

            if (IsKeyDown(Key.RightShift))
            {
               result |= InputModifiers.RightShift;
            }

            if (IsKeyDown(Key.LeftWin))
            {
               result |= InputModifiers.LeftWindows;
            }

            if (IsKeyDown(Key.RightWin))
            {
               result |= InputModifiers.RightWindows;
            }

            return result;
         }
      }
   }

   

   
}
