using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests
{
    // Line-rendering Step A3b: drives the full compute path on the GPU end to end. A compute shader writes a known
    // pattern (index+1) to a buffer via a BDA device address; a CPU readback verifies it. Compute is recorded in
    // BeginDraw's beforeRenderPass hook - outside the render pass, on the graphics queue - the only valid place to
    // dispatch (the render pass hasn't begun yet there).
    [TestFixture]
    public class ComputeTests
    {
        [Test]
        public void ComputeDispatch_WritesPatternViaBda()
        {
            const int count = 256;

            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();

            var gd = (GraphicsDevice)device;   // Dispatch/DrawIndirect/BufferBarrier live on the concrete device
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "ComputeSmoke.fx"), device);
            var pass = effect.Techniques[0].Passes[0];

            var output = Adamantium.Graphics.Buffer.New(gd, (ulong)(count * sizeof(uint)),
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);

            effect.Parameters["OutputAddress"].SetValue(output.GetDeviceAddress());
            effect.Parameters["Count"].SetValue((uint)count);

            var prms = new PresentationParameters(PresenterType.RenderTarget, 16, 16, IntPtr.Zero);
            using var presenter = GraphicsPresenter.Create(device, prms, "compute_smoke");
            device.SetRenderTargets(presenter.RenderTarget);
            device.SetDepthBuffer(presenter.DepthBuffer);
            device.MSAALevel = presenter.MSAALevel;
            device.Presenter = presenter;

            uint groups = (count + 63) / 64;
            Assert.That(device.BeginDraw(beforeRenderPass: cmd =>
            {
                pass.Apply();
                gd.Dispatch(groups);
                gd.BufferBarrier(output,
                    PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                    PipelineStageFlagBits2.HostBit, AccessFlagBits2.HostReadBit);
            }), Is.True);
            device.EndDraw();
            device.Submit();
            presenter.Present();
            device.FrameEnded();
            device.DeviceWaitIdle();

            var data = new int[count];
            var ptr = output.MapMemory();
            Marshal.Copy((IntPtr)(nint)ptr, data, 0, count);
            output.UnmapMemory();

            for (int i = 0; i < count; i++)
                Assert.That(data[i], Is.EqualTo(i + 1), $"output[{i}] (BDA compute write)");

            main.Dispose();
        }
    }
}
