using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// ONE Vulkan device for every GPU fixture in this namespace, created before the first and disposed after the last.
/// Each fixture used to create and dispose its own <see cref="MainGraphicsDevice"/>; a second device in the same process
/// takes the test host down with a fatal error (the run then reports the tests it had already passed and silently drops
/// the rest - it looks like a shrinking suite, not a crash). NUnit runs a SetUpFixture's one-time setup/teardown around
/// every fixture in its namespace, which is exactly the device's lifetime.
/// </summary>
[SetUpFixture]
public class GpuTestDevice
{
    private static MainGraphicsDevice _main;

    /// <summary>The shared render device. Valid for the whole run of this namespace's fixtures.</summary>
    public static IGraphicsDevice Device { get; private set; }

    /// <summary>Wait the device idle and free everything the finished work retired. The engine frees a retired resource
    /// when a wrapper BEGINS another frame, which never happens once a test stops drawing - so without this every
    /// fixture's render targets stay alive for the whole run and the device eventually refuses to allocate
    /// (ErrorOutOfDeviceMemory, taking the fixtures that happen to run last down with it). Call it when a harness is
    /// done with its resources.</summary>
    public static void Reclaim()
    {
        if (Device == null) return;

        Device.DeviceWaitIdle();
        _main.FlushRetiredAfterIdle();
    }

    [OneTimeSetUp]
    public void Create()
    {
        _main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "UITests", true);
        Device = _main.CreateRenderDevice();
    }

    [OneTimeTearDown]
    public void Destroy()
    {
        _main?.Dispose();
        _main = null;
        Device = null;
    }
}
