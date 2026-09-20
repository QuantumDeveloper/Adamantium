using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.UI.Core;
using Adamantium.Win32;
using Adamantium.Win32.Shell;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>The shell's Save-As dialog, registered on <see cref="FileDialog.Platform"/> by the Windows platform. Runs
/// on the UI (message-pump) thread and blocks it, which is what modal means: the dialog pumps its own messages, so the
/// application stays alive and repaints behind it.</summary>
internal sealed class WindowsFileDialog : IFileDialogPlatform
{
    public string Save(SaveFileRequest request)
    {
        var clsid = ShellDialog.ClsidFileSaveDialog;
        var iid = ShellDialog.IidFileSaveDialog;

        if (Win32Interop.CoCreateInstance(ref clsid, IntPtr.Zero, Win32Interop.ClsCtxInprocServer, ref iid,
                out var instance) != 0 || instance is not IFileSaveDialog dialog)
            return null;

        try
        {
            Prepare(dialog, request);

            // Every non-zero answer means no file, and cancelling is the usual one. Nothing to report either way: the
            // user closing a dialog is not a failure, and neither is a shell that refused to open one.
            if (dialog.Show(Owner(request?.Owner ?? IntPtr.Zero)) != 0) return null;
            if (dialog.GetResult(out var item) != 0) return null;

            return PathOf(item);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    public string Open(OpenFileRequest request)
    {
        var clsid = ShellDialog.ClsidFileOpenDialog;
        var iid = ShellDialog.IidFileOpenDialog;

        if (Win32Interop.CoCreateInstance(ref clsid, IntPtr.Zero, Win32Interop.ClsCtxInprocServer, ref iid,
                out var instance) != 0 || instance is not IFileOpenDialog dialog)
            return null;

        try
        {
            Prepare(dialog, request);

            if (dialog.Show(Owner(request?.Owner ?? IntPtr.Zero)) != 0) return null;
            if (dialog.GetResult(out var item) != 0) return null;

            return PathOf(item);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private static void Prepare(IFileOpenDialog dialog, OpenFileRequest request)
    {
        // A file being OPENED has to be there - both the file and the folder it is named in. Added to the dialog's own
        // flags for the same reason the save side adds to them.
        if (dialog.GetOptions(out var options) == 0)
        {
            dialog.SetOptions(options | ShellDialog.ForceFileSystem | ShellDialog.FileMustExist |
                              ShellDialog.PathMustExist);
        }

        if (request == null) return;

        if (!string.IsNullOrEmpty(request.Title)) dialog.SetTitle(request.Title);

        if (Named(request.Key) is { } id) dialog.SetClientGuid(ref id);

        var types = Filters(request.FileTypes);
        if (types.Length > 0) dialog.SetFileTypes((uint)types.Length, types);
    }

    private static void Prepare(IFileSaveDialog dialog, SaveFileRequest request)
    {
        // Read the flags and ADD to them. Setting them outright would drop the ones a save dialog carries by default -
        // among them the prompt before replacing an existing file.
        if (dialog.GetOptions(out var options) == 0)
            dialog.SetOptions(options | ShellDialog.OverwritePrompt | ShellDialog.ForceFileSystem);

        if (request == null) return;

        if (!string.IsNullOrEmpty(request.Title)) dialog.SetTitle(request.Title);
        if (!string.IsNullOrEmpty(request.FileName)) dialog.SetFileName(request.FileName);
        if (!string.IsNullOrEmpty(request.DefaultExtension)) dialog.SetDefaultExtension(request.DefaultExtension);

        if (Named(request.Key) is { } id) dialog.SetClientGuid(ref id);

        var types = Filters(request.FileTypes);
        if (types.Length > 0) dialog.SetFileTypes((uint)types.Length, types);
    }

    // WHOSE DIALOG THIS IS. A dialog is placed over its owner and opens on the screen the owner is on; with no owner it
    // opens on the main one, which on a second monitor puts it away from the work it is about.
    //
    // What was asked for first, then the window in front, and the application's main window last. The active window is
    // not enough on its own: it is the active window OF THIS THREAD, and a dialog opened from anywhere but the pump
    // thread - a command run off a timer, say - would find none at all.
    private static IntPtr Owner(IntPtr asked)
    {
        if (asked != IntPtr.Zero) return asked;

        var active = Win32Interop.GetActiveWindow();

        return active != IntPtr.Zero ? active : UIApplication.Current?.MainWindow?.Handle ?? IntPtr.Zero;
    }

    // A NAME TURNED INTO THE IDENTIFIER THE SHELL WANTS, and the same name always gives the same one - which is the
    // whole point: the size and place a person dragged a dialog into come back because it is recognised as the same
    // dialog. Built from the name's own bytes rather than kept in a table, so a name coined in an application nobody
    // here knows about works exactly as well as one of ours.
    private static Guid? Named(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key.Trim()));
        var bytes = new byte[16];

        Array.Copy(hash, bytes, 16);

        return new Guid(bytes);
    }

    private static FilterSpec[] Filters(IReadOnlyList<FileType> types)
    {
        if (types == null || types.Count == 0) return Array.Empty<FilterSpec>();

        var specs = new List<FilterSpec>(types.Count);
        foreach (var type in types)
        {
            if (type == null || type.Extensions.Count == 0) continue;

            var patterns = new string[type.Extensions.Count];
            for (var i = 0; i < patterns.Length; i++) patterns[i] = "*." + type.Extensions[i];

            specs.Add(new FilterSpec { Name = type.Name, Patterns = string.Join(";", patterns) });
        }

        return specs.ToArray();
    }

    private static string PathOf(IShellItem item)
    {
        if (item == null) return null;

        try
        {
            if (item.GetDisplayName(ShellDialog.SigdnFileSysPath, out var name) != 0) return null;

            try { return Marshal.PtrToStringUni(name); }
            finally { Marshal.FreeCoTaskMem(name); }
        }
        finally
        {
            Marshal.ReleaseComObject(item);
        }
    }
}
