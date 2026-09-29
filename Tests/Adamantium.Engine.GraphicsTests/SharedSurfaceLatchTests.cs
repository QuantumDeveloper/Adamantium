using Adamantium.Graphics;
using Adamantium.Imaging;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests;

/// <summary>
/// The consumer side of a shared surface: a frame is latched once, however many draws sample it. Latched twice, it was
/// signalled on the consume timeline twice with the same value, which Vulkan forbids.
/// </summary>
[TestFixture]
public class SharedSurfaceLatchTests
{
    [TearDown]
    public void ReleaseDevices() => GpuFixture.ReleaseRenderDevices();

    [Test]
    public void ANewFrame_IsHandedOutOnce_WhoeverAsks()
    {
        var device = (GraphicsDevice)GpuFixture.CreateRenderDevice();
        using var surface = SharedSurface.CreateExportable(device, 4, 4, SurfaceFormat.B8G8R8A8.UNorm);

        Assert.That(surface.LatchNewFrame(), Is.Zero, "nothing produced yet");

        Produce(device, surface, 1);
        Assert.That(surface.LatchNewFrame(), Is.EqualTo(1));
        Assert.That(surface.LatchNewFrame(), Is.Zero, "the same frame, asked for again");

        Produce(device, surface, 2);
        Assert.That(surface.LatchNewFrame(), Is.EqualTo(2));
    }

    private static void Produce(GraphicsDevice device, SharedSurface surface, ulong frame)
    {
        var result = device.LogicalDevice.SignalSemaphore(new SemaphoreSignalInfo
        {
            Semaphore = surface.ProduceSemaphore,
            Value = frame
        });
        Assert.That(result, Is.EqualTo(Result.Success));
    }
}
