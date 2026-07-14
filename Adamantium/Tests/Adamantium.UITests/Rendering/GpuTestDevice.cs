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
