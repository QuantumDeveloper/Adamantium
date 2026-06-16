using System.Runtime.InteropServices;

namespace Adamantium.Core;

/// <summary>
/// P/Invoke into the C standard library for POSIX calls used on Linux and macOS. The library name "libc" is
/// resolved by the .NET runtime on both: <c>libc.so.6</c> on Linux and <c>libc.dylib</c> (libSystem) on macOS.
/// Windows code paths must not call these.
/// </summary>
public static class PosixInterop
{
    private const string LibC = "libc";

    /// <summary>POSIX <c>close(2)</c>: closes a file descriptor (e.g. an exported Vulkan memory/semaphore fd).</summary>
    [DllImport(LibC, EntryPoint = "close", SetLastError = true)]
    public static extern int Close(int fd);
}
