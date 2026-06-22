using NUnit.Framework;
using System.IO;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class EffectTests
    {
        [Test]
        public void EffectLoadingTest()
        {
            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "FontEffect.fx"), device);
        }

        // Line-rendering Step A3a (cheap de-risk before the full dispatch harness): just compiling+loading the
        // compute effect exercises the two biggest unknowns - that Slang compiles the BDA compute (uint* from a
        // uint64 device address) and that the driver creates a COMPUTE shader-object (vkCreateShadersEXT) on this GPU.
        // If this throws, the dispatch harness isn't worth building yet; if it passes, the rest is plumbing.
        [Test]
        public void ComputeShaderCompilesAndCreates()
        {
            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "ComputeSmoke.fx"), device);

            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.Techniques.Count, Is.GreaterThan(0), "compute technique should be present");
        }
    }
}
