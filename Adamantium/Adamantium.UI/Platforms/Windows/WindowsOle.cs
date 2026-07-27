using System;
using System.Threading;
using Adamantium.Win32;
using Serilog;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Brings OLE up on the UI (message-pump) thread - the precondition for every OS-level drag-drop call
/// (<c>RegisterDragDrop</c>, <c>DoDragDrop</c>). OLE demands a single-threaded apartment, so the application's entry
/// point should carry <c>[STAThread]</c>; we try to set it here for an app that forgot, and if the thread is already an
/// MTA we log and stay OUT of OLE entirely rather than initializing it behind the runtime's back.
/// <para>Everything in-app keeps working without OLE - only exchanging payloads with OTHER applications is off.</para>
/// </summary>
internal static class WindowsOle
{
    private static bool _initialized;

    /// <summary>True when OLE is up on this process's UI thread and the OS drag-drop bridge can be used.</summary>
    public static bool IsAvailable { get; private set; }

    /// <summary>Call once, on the UI thread, before any window exists.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Best-effort promotion, and only that: a .NET main thread STARTED without [STAThread] is already fixed as an
        // MTA and refuses this (measured - TrySetApartmentState returns false and OleInitialize then answers
        // RPC_E_CHANGED_MODE). It helps only when the engine is hosted on a thread whose apartment is still unset.
        var thread = Thread.CurrentThread;
        if (thread.GetApartmentState() != ApartmentState.STA)
        {
            thread.TrySetApartmentState(ApartmentState.STA);
        }

        if (thread.GetApartmentState() != ApartmentState.STA)
        {
            Log.Logger.Warning("OS drag-drop is disabled: the UI thread is not an STA. Mark the application entry point " +
                               "with [STAThread] to exchange drags with other applications.");
            return;
        }

        var result = Win32Interop.OleInitialize(IntPtr.Zero);
        IsAvailable = result >= 0;
        if (!IsAvailable)
        {
            Log.Logger.Warning("OS drag-drop is disabled: OleInitialize failed with 0x{Result:X8}.", result);
        }
    }
}
