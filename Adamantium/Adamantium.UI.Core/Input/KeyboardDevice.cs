using Adamantium.UI.Core.Input.Raw;

namespace Adamantium.UI.Core.Input;

public class KeyboardDevice
{
   // Live key state comes from the platform (Keyboard.Platform); nothing here caches an OS-shaped state array.
   private readonly Dictionary<Key, KeyPressInfo> keyStates = new Dictionary<Key, KeyPressInfo>();

   private static KeyboardDevice currentDevice;

   public static KeyboardDevice CurrentDevice => currentDevice ??= new KeyboardDevice();
   

   public InputModifiers Modifiers
   {
      get
      {
         InputModifiers modifiers = InputModifiers.None;
         if (IsKeyDown(Key.LeftAlt))
         {
            modifiers|=InputModifiers.LeftAlt;
         }
         if (IsKeyDown(Key.RightAlt))
         {
            modifiers |= InputModifiers.RightAlt;
         }
         if (IsKeyDown(Key.LeftCtrl))
         {
            modifiers |= InputModifiers.LeftControl;
         }
         if (IsKeyDown(Key.RightCtrl))
         {
            modifiers |= InputModifiers.RightControl;
         }
         if (IsKeyDown(Key.LeftShift))
         {
            modifiers |= InputModifiers.LeftShift;
         }
         if (IsKeyDown(Key.RightShift))
         {
            modifiers |= InputModifiers.RightShift;
         }
         if (IsKeyDown(Key.LeftWin))
         {
            modifiers |= InputModifiers.LeftWindows;
         }
         if (IsKeyDown(Key.RightWin))
         {
            modifiers |= InputModifiers.RightWindows;
         }
         return modifiers;
      }
   }

   public IInputComponent FocusedComponent { get; private set; }

   public bool SetFocusedElement(IInputComponent component, NavigationMethod navigationMethod = NavigationMethod.Unspecified,
      InputModifiers modifiers = InputModifiers.None)
   {
      if (component == null)
      {
         ClearState();
      }
      if (component != FocusedComponent)
      {
         KeyboardFocusChangedEventArgs args = new KeyboardFocusChangedEventArgs(FocusedComponent, component);
         args.RoutedEvent = Keyboard.PreviewGotKeyboardFocusEvent;
         FocusedComponent?.RaiseEvent(args);

         FocusedComponent = component;

         KeyboardGotFocusEventArgs e = new KeyboardGotFocusEventArgs(FocusedComponent, component, navigationMethod, modifiers);
         e.RoutedEvent = Keyboard.GotKeyboardFocusEvent;
         FocusedComponent?.RaiseEvent(e);

         return true;
      }
      return false;
   }

   private void ClearState()
   {
      keyStates.Clear();
   }

   /// <summary>
   /// Checks is key generally pressed
   /// </summary>
   /// <param name="key"></param>
   /// <returns></returns>
   public bool IsKeyDown(Key key)
   {
      // Ask the OS when we can (the live, physical state), and fall back to what our own events have tracked - which is
      // right whenever messages are flowing, and the best we can do when no platform is registered.
      if (Keyboard.Platform is { } platform) return platform.IsKeyDown(key);
      return keyStates.TryGetValue(key, out var tracked) && tracked.CurrentState == KeyState.Down;
   }

   /// <summary>
   /// Checks is key generally up
   /// </summary>
   /// <param name="key"></param>
   /// <returns></returns>
   public bool IsKeyUp(Key key)
   {
      return !IsKeyDown(key);
   }

   /// <summary>
   /// Returns value indicating is current key is repeatedly pressed or just once
   /// </summary>
   /// <param name="key">Key to look for</param>
   /// <returns>Returns true if key is pressed not for the first time, otherwise value is false</returns>
   /// <remarks>If FocusedElement element is null, return value will be false</remarks>
   public bool IsRepeated(Key key)
   {
      if (keyStates.ContainsKey(key))
      {
         var parameters = keyStates[key];
         return parameters.IsRepeated;
      }
      return false;
   }

   /// <summary>
   /// Returns time in milliseconds between the current system uptime and last pressing time
   /// </summary>
   /// <param name="key">Key to look for</param>
   /// <returns>Return value is in milliseconds</returns>
   /// <remarks>If FocusedElement element is null, return value will be 0</remarks>
   public UInt64 GetPressTime(Key key)
   {
      if (keyStates.ContainsKey(key))
      {
         var parameters = keyStates[key];
         if (parameters.CurrentState == KeyState.Down)
         {
            // Both sides are milliseconds since boot, but the press time arrives 32-bit (that is what a raw event
            // carries), so subtract in 32-bit too - otherwise the answer goes wild once uptime passes the ~49-day wrap.
            return unchecked((uint)Environment.TickCount64 - parameters.PressTime);
         }
      }
      return 0;
   }

   public bool IsKeyToggled(Key key)
   {
      return Keyboard.Platform?.IsKeyToggled(key) ?? false;
   }

   /// <summary>Delivers a raw key/text event. <paramref name="fallback"/> is where a key goes when NOTHING is focused -
   /// the window itself: a window opens with no focused element, and dropping the key there left the keyboard dead until
   /// something had been clicked. The window is the root of the route, which is exactly where navigation listens, so the
   /// first Tab can step into the tree.</summary>
   public void ProcessEvent(RawInputEventArgs eventArgs, IInputComponent fallback = null)
   {
      var target = FocusedComponent ?? fallback;
      if (target != null)
      {
         if (eventArgs is RawKeyboardEventArgs e)
         {
            switch (e?.EventType)
            {
               case RawKeyboardEventType.KeyDown:
               case RawKeyboardEventType.KeyUp:
                  var parameters = e.Press;
                  parameters.PressTime = e.Timestamp;
                  KeyEventArgs args = new KeyEventArgs(this, e.ChangedKey, e.InputModifiers,
                     e.Timestamp);
                  if (e.EventType == RawKeyboardEventType.KeyDown)
                  {
                     parameters.CurrentState = KeyState.Down;
                     args.RoutedEvent = Keyboard.PreviewKeyDownEvent;
                  }
                  else if (e.EventType == RawKeyboardEventType.KeyUp)
                  {
                     parameters.CurrentState = KeyState.Up;
                     args.RoutedEvent = Keyboard.PreviewKeyUpEvent;
                  }
                  UpdateKeyData(e.ChangedKey, parameters);

                  target.RaiseEvent(args);

                  if (e.EventType == RawKeyboardEventType.KeyDown)
                  {
                     parameters.CurrentState = KeyState.Down;
                     args.RoutedEvent = Keyboard.KeyDownEvent;
                  }
                  else if (e.EventType == RawKeyboardEventType.KeyUp)
                  {
                     parameters.CurrentState = KeyState.Up;
                     args.RoutedEvent = Keyboard.KeyUpEvent;
                  }
                  target.RaiseEvent(args);
                  break;
            }
         }
         else if (eventArgs is RawTextInputEventArgs inputArgs && !string.IsNullOrEmpty(inputArgs.Text))
         {
            // Typed character(s) from the OS (WM_CHAR, already filtered to >= space in the Win32 worker). Deliver as the
            // tunnel PREVIEW first (so a container can pre-empt it), then the bubbling TextInput. A TextBox consumes this
            // to insert text; controls that don't handle it simply ignore it.
            var textArgs = new TextInputEventArgs(inputArgs.Text)
            {
               RoutedEvent = Keyboard.PreviewTextInputEvent
            };
            target.RaiseEvent(textArgs);

            textArgs.RoutedEvent = Keyboard.TextInputEvent;
            target.RaiseEvent(textArgs);
         }
      }
   }

   private void UpdateKeyData(Key key, KeyPressInfo parameters)
   {
      if (keyStates.ContainsKey(key))
      {
         keyStates[key] = parameters;
      }
      else
      {
         keyStates.Add(key, parameters);
      }
   }
}