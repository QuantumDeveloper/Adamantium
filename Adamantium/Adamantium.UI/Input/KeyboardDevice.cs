﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Adamantium.UI.Input.Raw;
using Adamantium.Win32;

namespace Adamantium.UI.Input;

public abstract class KeyboardDevice
{
   private const int KEY_PRESSED = 0x8;
   private const int KEY_TOGGLED = 0x1;

   private byte[] keyCodes = new byte[256];
   private Dictionary<Key, KeyParameters> keyStates = new Dictionary<Key, KeyParameters>();

   private static KeyboardDevice currentDevice;

   public static KeyboardDevice CurrentDevice
   {
      get
      {
         if (currentDevice == null)
         {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
               currentDevice = WindowsKeyboardDevice.Instance;
            }
         }

         return currentDevice;
      }
   }

   public InputModifiers Modifiers
   {
      get
      {
         UpdateKeyStates();
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
      Array.Clear(keyCodes, 0, keyCodes.Length);
      keyStates.Clear();
   }

   private void UpdateKeyStates()
   {
      if (!Win32Interop.GetKeyboardState(keyCodes))
      {
         int err = Marshal.GetLastWin32Error();
         throw new Win32Exception(err);
      }
   }

   /// <summary>
   /// Checks is key generally pressed
   /// </summary>
   /// <param name="key"></param>
   /// <returns></returns>
   public bool IsKeyDown(Key key)
   {
      return Convert.ToBoolean(Win32Interop.GetKeyState((uint)key) & KEY_PRESSED);
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
         if (parameters.CurrentState == KeyStates.Down)
         {
            return Win32Interop.GetTickCount64() - parameters.PressTime;
         }
      }
      return 0;
   }

   public bool IsKeyToggled(Key key)
   {
      return Convert.ToBoolean(Win32Interop.GetKeyState((uint)key) & KEY_TOGGLED);
   }

   public void ProcessEvent(RawInputEventArgs eventArgs)
   {
      if (FocusedComponent != null)
      {
         UpdateKeyStates();
         if (eventArgs is RawKeyboardEventArgs e)
         {
            switch (e?.EventType)
            {
               case RawKeyboardEventType.KeyDown:
               case RawKeyboardEventType.KeyUp:
                  var parameters = Messages.GetKeyParameters(e.LParam);
                  parameters.PressTime = e.Timestamp;
                  KeyEventArgs args = new KeyEventArgs(this, e.ChangedKey, e.InputModifiers,
                     e.Timestamp);
                  if (e.EventType == RawKeyboardEventType.KeyDown)
                  {
                     parameters.CurrentState = KeyStates.Down;
                     args.RoutedEvent = Keyboard.PreviewKeyDownEvent;
                  }
                  else if (e.EventType == RawKeyboardEventType.KeyUp)
                  {
                     parameters.CurrentState = KeyStates.Up;
                     args.RoutedEvent = Keyboard.PreviewKeyUpEvent;
                  }
                  UpdateKeyData(e.ChangedKey, parameters);

                  FocusedComponent.RaiseEvent(args);

                  if (e.EventType == RawKeyboardEventType.KeyDown)
                  {
                     parameters.CurrentState = KeyStates.Down;
                     args.RoutedEvent = Keyboard.KeyDownEvent;
                  }
                  else if (e.EventType == RawKeyboardEventType.KeyUp)
                  {
                     parameters.CurrentState = KeyStates.Up;
                     args.RoutedEvent = Keyboard.KeyUpEvent;
                  }
                  FocusedComponent.RaiseEvent(args);
                  break;
            }
         }
         else
         {
            var inputArgs = eventArgs as RawTextInputEventArgs;
            TextInputEventArgs textArgs = new TextInputEventArgs(inputArgs?.Text);
            textArgs.RoutedEvent = UIComponent.PreviewTextInputEvent;
            FocusedComponent.RaiseEvent(textArgs);

            textArgs.RoutedEvent = UIComponent.TextInputEvent;
            FocusedComponent.RaiseEvent(textArgs);
         }
      }
   }

   private void UpdateKeyData(Key key, KeyParameters parameters)
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