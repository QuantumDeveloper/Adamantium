using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Adamantium.Win32;

namespace Adamantium.Engine
{
    
   /// <summary>
   /// Provides a hook to WndProc of an existing window handle using <see cref="IMessageFilter"/>.
   /// </summary>
   public class MessageFilterHook
   {
      private static readonly Dictionary<IntPtr, MessageFilterHook> RegisteredHooks = new Dictionary<IntPtr, MessageFilterHook>(EqualityComparer.DefaultIntPtr);

      private readonly IntPtr defaultWndProc;

      private readonly IntPtr hwnd;

      private readonly Interop.WndProc newWndProc;

      private readonly IntPtr newWndProcPtr;

      private List<IMessageFilter> currentFilters;

      private bool isDisposed;

      /// <summary>
      /// Initializes a new instance of the <see cref="MessageFilterHook" /> class.
      /// </summary>
      /// <param name="hwnd">The HWND.</param>
      private MessageFilterHook(IntPtr hwnd)
      {
         currentFilters = new List<IMessageFilter>();
         this.hwnd = hwnd;

         // Retrieve the previous WndProc associated with this window handle
         defaultWndProc = Interop.GetWindowLong(hwnd, WindowLongType.WndProc);

         // Create a pointer to the new WndProc
         newWndProc = WndProc;
         newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);

         // Set our own private wndproc in order to catch NCDestroy message
         Interop.SetWindowLong(hwnd, WindowLongType.WndProc, newWndProcPtr);
      }

      /// <summary>
      /// Adds a message filter to a window.
      /// </summary>
      /// <param name="hwnd">The handle of the window.</param>
      /// <param name="messageFilter">The message filter.</param>
      public static void AddMessageFilter(IntPtr hwnd, IMessageFilter messageFilter)
      {
         lock (RegisteredHooks)
         {
            hwnd = GetSafeWindowHandle(hwnd);
            MessageFilterHook hook;
            if (!RegisteredHooks.TryGetValue(hwnd, out hook))
            {
               hook = new MessageFilterHook(hwnd);
               RegisteredHooks.Add(hwnd, hook);
            }
            hook.AddMessageFilter(messageFilter);
         }
      }

      /// <summary>
      /// Removes a message filter associated with a window.
      /// </summary>
      /// <param name="hwnd">The handle of the window.</param>
      /// <param name="messageFilter">The message filter.</param>
      public static void RemoveMessageFilter(IntPtr hwnd, IMessageFilter messageFilter)
      {
         lock (RegisteredHooks)
         {
            hwnd = GetSafeWindowHandle(hwnd);
            MessageFilterHook hook;
            if (RegisteredHooks.TryGetValue(hwnd, out hook))
            {
               hook.RemoveMessageFilter(messageFilter);

               if (hook.isDisposed)
               {
                  RegisteredHooks.Remove(hwnd);
                  hook.RestoreWndProc();
               }
            }
         }
      }

      private void AddMessageFilter(IMessageFilter filter)
      {
         // Make a copy of the filters in order to support a lightweight threadsafe
         var filters = new List<IMessageFilter>(currentFilters);
         if (!filters.Contains(filter))
            filters.Add(filter);
         currentFilters = filters;
      }

      private void RemoveMessageFilter(IMessageFilter filter)
      {
         // Make a copy of the filters in order to support a lightweight threadsafe
         var filters = new List<IMessageFilter>(this.currentFilters);
         filters.Remove(filter);

         // If there are no more filters, then we can remove the hook
         if (filters.Count == 0)
         {
            isDisposed = true;
            RestoreWndProc();
         }
         currentFilters = filters;
      }

      private void RestoreWndProc()
      {
         var currentProc = Interop.GetWindowLong(hwnd, WindowLongType.WndProc);
         if (currentProc == newWndProcPtr)
         {
            // Restore back default WndProc only if the previous callback is owned by this message filter hook
            Interop.SetWindowLong(hwnd, WindowLongType.WndProc, defaultWndProc);
         }
      }

      private IntPtr WndProc(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam)
      {
         if (isDisposed)
            RestoreWndProc();
         else
         {
            var message = new System.Windows.Forms.Message() { HWnd = hwnd, LParam = lParam, Msg = (int)msg, WParam = wParam };
            foreach (var messageFilter in currentFilters)
            {
               if (messageFilter.PreFilterMessage(ref message))
                  return message.Result;
            }
         }

         var result = Interop.CallWindowProc(defaultWndProc, hWnd, msg, wParam, lParam);
         return result;
      }

      private static IntPtr GetSafeWindowHandle(IntPtr hwnd)
      {
         return hwnd == IntPtr.Zero ? Process.GetCurrentProcess().MainWindowHandle : hwnd;
      }

   }
   


   /// <summary>
   /// Provides <see cref="IEqualityComparer{T}"/> for default value types.
   /// </summary>
   internal static class EqualityComparer
   {
      /// <summary>
      /// A default <see cref="IEqualityComparer{T}"/> for <see cref="System.IntPtr"/>.
      /// </summary>
      public static readonly IEqualityComparer<IntPtr> DefaultIntPtr = new IntPtrComparer();

      internal class IntPtrComparer : EqualityComparer<IntPtr>
      {
         public override bool Equals(IntPtr x, IntPtr y)
         {
            return x == y;
         }

         public override int GetHashCode(IntPtr obj)
         {
            return obj.GetHashCode();
         }
      }
   }
}
