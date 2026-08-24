using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests
{
    /// <summary>
    /// ONE Vulkan instance and logical device for the whole assembly.
    /// <para>Every GPU test used to create and destroy its own <see cref="MainGraphicsDevice"/> - i.e. its own
    /// VkInstance. After a handful of those cycles the loader starts rejecting the next instance
    /// (<c>vkGetInstanceProcAddr: Invalid instance</c>) and the test host dies mid-run, so the suite reported "aborted"
    /// instead of results and every failure anywhere else became impossible to tell apart from this one. Creating the
    /// instance once is also what the engine itself does: an application has one.</para>
    /// <para>Per-test isolation is kept where it matters - each test still gets its OWN render device, released in the
    /// fixture's TearDown, so no test inherits another's render targets, presenter or MSAA level.</para>
    /// </summary>
    [SetUpFixture]
    public class GpuFixture
    {
        public static MainGraphicsDevice Main { get; private set; }

        private static readonly List<IGraphicsDevice> PerTest = new();

        [OneTimeSetUp]
        public void CreateSharedDevice()
        {
            Main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
        }

        [OneTimeTearDown]
        public void DisposeSharedDevice()
        {
            ReleaseRenderDevices();
            Main?.Dispose();
            Main = null;
        }

        /// <summary>A render device for the current test, tracked so the fixture can release it however the test ends -
        /// including on a failed assertion, which throws before any cleanup line the test itself might carry.</summary>
        public static IGraphicsDevice CreateRenderDevice()
        {
            var device = Main.CreateRenderDevice();
            PerTest.Add(device);
            return device;
        }

        /// <summary>Called from each fixture's TearDown. RemoveDevice both unregisters and disposes, so the shared main
        /// device is not left holding a device the next test knows nothing about.</summary>
        public static void ReleaseRenderDevices()
        {
            if (Main == null) { PerTest.Clear(); return; }

            Main.DeviceWaitIdle();
            foreach (var device in PerTest) Main.RemoveDevice(device);
            PerTest.Clear();
        }
    }
}
