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
            if (dialog.Show(Win32Interop.GetActiveWindow()) != 0) return null;
            if (dialog.GetResult(out var item) != 0) return null;

            return PathOf(item);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
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

        var types = Filters(request.FileTypes);
        if (types.Length > 0) dialog.SetFileTypes((uint)types.Length, types);
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
