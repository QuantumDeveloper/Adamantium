using System.Diagnostics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Input;

public static class Keyboard
{
   /// <summary>The platform that answers live key-state queries, registered once at startup. Null before it is - key
   /// state then comes from what our own events have tracked (see <see cref="KeyboardDevice.IsKeyDown"/>).</summary>
   public static INativeKeyboard Platform { get; set; }

   public static readonly RoutedEvent KeyDownEvent = EventManager.RegisterRoutedEvent("KeyDown",
      RoutingStrategy.Bubble, typeof(KeyEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent KeyUpEvent = EventManager.RegisterRoutedEvent("KeyUp",
      RoutingStrategy.Bubble, typeof(KeyEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent GotKeyboardFocusEvent = EventManager.RegisterRoutedEvent("GotKeyboardFocus",
      RoutingStrategy.Bubble, typeof(KeyboardGotFocusEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent LostKeyboardFocusEvent = EventManager.RegisterRoutedEvent("LostKeyboardFocus",
      RoutingStrategy.Bubble, typeof(KeyboardFocusChangedEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent PreviewKeyDownEvent = EventManager.RegisterRoutedEvent("PreviewKeyDown",
      RoutingStrategy.Tunnel, typeof(KeyEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent PreviewKeyUpEvent = EventManager.RegisterRoutedEvent("PreviewKeyUp",
      RoutingStrategy.Tunnel, typeof(KeyEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent PreviewLostKeyboardFocusEvent = EventManager.RegisterRoutedEvent("PreviewLostKeyboardFocus",
      RoutingStrategy.Tunnel, typeof(KeyboardFocusChangedEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent PreviewGotKeyboardFocusEvent = EventManager.RegisterRoutedEvent("PreviewGotKeyboardFocus",
      RoutingStrategy.Tunnel, typeof(KeyboardGotFocusEventHandler), typeof(Keyboard));

   // Typed-character input (from the OS WM_CHAR pipeline, raised by KeyboardDevice on the focused element). Lives in Core
   // alongside the key events so the Core input device can raise it; InputUIComponent (Controls) exposes the CLR-event
   // wrappers. Tunnel preview first, then bubble.
   public static readonly RoutedEvent TextInputEvent = EventManager.RegisterRoutedEvent("TextInput",
      RoutingStrategy.Bubble, typeof(TextInputEventHandler), typeof(Keyboard));

   public static readonly RoutedEvent PreviewTextInputEvent = EventManager.RegisterRoutedEvent("PreviewTextInput",
      RoutingStrategy.Tunnel, typeof(TextInputEventHandler), typeof(Keyboard));


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