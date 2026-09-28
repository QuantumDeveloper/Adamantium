using NUnit.Framework;
using System.IO;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class EffectTests
    {
        [TearDown]
        public void ReleaseDevices() => GpuFixture.ReleaseRenderDevices();

        [Test]
        public void EffectLoadingTest()
        {
            var main = GpuFixture.Main;
            var device = GpuFixture.CreateRenderDevice();
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "FontEffect.fx"), device);
        }

        // Line-rendering Step A3a (cheap de-risk before the full dispatch harness): just compiling+loading the
        // compute effect exercises the two biggest unknowns - that Slang compiles the BDA compute (uint* from a
        // uint64 device address) and that the driver creates a COMPUTE shader-object (vkCreateShadersEXT) on this GPU.
        // If this throws, the dispatch harness isn't worth building yet; if it passes, the rest is plumbing.
        [Test]
        public void ComputeShaderCompilesAndCreates()
        {
            var main = GpuFixture.Main;
            var device = GpuFixture.CreateRenderDevice();
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "ComputeSmoke.fx"), device);

            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.Techniques.Count, Is.GreaterThan(0), "compute technique should be present");
        }

        // The pool keeps one copy of a stage two effects share word for word. It used to refuse the second effect: every
        // shader was named after its effect, and one shader under two names read as a clash.
        [Test]
        public void TwoEffectsSharingAStage_ShareOneShader()
        {
            const string source = @"
float4 VS(float4 p : POSITION) : SV_Position { return p; }
float4 PS(float4 p : SV_Position) : SV_Target0 { return p; }
technique T { pass P { VertexShader = VS; PixelShader = PS; } }
";
            var first = EffectCompiler.Compile(source, "First.fx");
            var second = EffectCompiler.Compile(source.Replace("SV_Target0 { return p; }", "SV_Target0 { return p * 0.5; }"), "Second.fx");
            Assert.That(first.HasErrors || second.HasErrors, Is.False);
            var device = GpuFixture.CreateRenderDevice();
            using var firstEffect = new Effect(device, first.EffectData);
            var registered = device.DefaultEffectPool.RegisteredShaders.Count;

            using var secondEffect = new Effect(device, second.EffectData);

            Assert.That(device.DefaultEffectPool.RegisteredShaders.Count - registered, Is.EqualTo(1), "only the new pixel shader");
        }
    }
}
