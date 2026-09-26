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
         var previous = FocusedComponent;
         KeyboardFocusChangedEventArgs args = new KeyboardFocusChangedEventArgs(FocusedComponent, component);
         args.RoutedEvent = Keyboard.PreviewGotKeyboardFocusEvent;
         FocusedComponent?.RaiseEvent(args);

         FocusedComponent = component;

         if (previous != null)
         {
            var lost = new KeyboardFocusChangedEventArgs(previous, component);
            lost.RoutedEvent = Keyboard.LostKeyboardFocusEvent;
            previous.RaiseEvent(lost);
         }

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
   /// Whether the key is down.
   /// </summary>
   public bool IsKeyDown(Key key)
   {
      // The OS's live state when there is a platform, else what our own events tracked.
      if (Keyboard.Platform is { } platform) return platform.IsKeyDown(key);
      return keyStates.TryGetValue(key, out var tracked) && tracked.CurrentState == KeyState.Down;
   }

   /// <summary>
   /// Whether the key is up.
   /// </summary>
   public bool IsKeyUp(Key key)
   {
      return !IsKeyDown(key);
   }

   /// <summary>
   /// Whether the key is held and repeating, not just pressed. False when nothing is focused.
   /// </summary>
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
   /// Milliseconds since the key was pressed. 0 when nothing is focused.
   /// </summary>
   public UInt64 GetPressTime(Key key)
   {
      if (keyStates.ContainsKey(key))
      {
         var parameters = keyStates[key];
         if (parameters.CurrentState == KeyState.Down)
         {
            // In 32 bits, like the raw event's press time, or it goes wild past the ~49-day wrap.
            return unchecked((uint)Environment.TickCount64 - parameters.PressTime);
         }
      }
      return 0;
   }

   public bool IsKeyToggled(Key key)
   {
      return Keyboard.Platform?.IsKeyToggled(key) ?? false;
   }

   /// <summary>Delivers a raw key/text event. <paramref name="fallback"/> - the window - takes a key when nothing is
   /// focused, so the first Tab can step into the tree.</summary>
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
                     e.Timestamp, parameters.PhysicalKey);
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
            // Preview first, so a container can pre-empt the text, then the bubbling TextInput.
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