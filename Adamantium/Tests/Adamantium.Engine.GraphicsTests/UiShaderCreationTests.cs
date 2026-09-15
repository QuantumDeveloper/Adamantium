using System.IO;
using Adamantium.Graphics.Core.EffectsFramework;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests
{
    /// <summary>Whether THIS DRIVER will create the UI's shader objects at all.
    /// <para>A question nothing else asks. Compiling a shader is done at build time and says only that the language was
    /// understood; creating one is <c>vkCreateShadersEXT</c>, it happens the first time the pass is used, and this
    /// driver's shader-object compiler has taken an access violation INSIDE that call more than once - on a nested loop
    /// in a fragment stage, and on a branch that returns from one. An access violation cannot be caught, so a pass it
    /// refuses does not degrade: the application dies, on whatever frame first drew through it.</para>
    /// <para>So it is asked HERE, of the files the application actually ships, where the answer costs a test run rather
    /// than somebody's work.</para></summary>
    [TestFixture]
    public class UiShaderCreationTests
    {
        [TearDown]
        public void ReleaseDevices() => GpuFixture.ReleaseRenderDevices();

        // The ARROW pass: a whole arrow - shaft and both heads - decided per pixel, with no geometry anywhere. Flat and
        // branchless on purpose; this is what says the driver agrees.
        // The ARROW pass, and the ones it stands beside - which are also what a change to a shared include would break,
        // with nothing to say so until an application died.
        [TestCase("ArrowEffect.fx")]
        [TestCase("InkEffect.fx")]
        [TestCase("GridEffect.fx")]
        [TestCase("BatchEffect.fx")]
        [TestCase("BrushEffect.fx")]
        public void TheUiPassesCreateOnThisDevice(string file)
        {
            var device = GpuFixture.CreateRenderDevice();

            // Compiled HERE rather than through Effect.CompileFromFile, which answers a failure with null: what is
            // wrong with a shader is exactly what a test about shaders has to say, and "expected not null" says none of
            // it.
            // WRITTEN DOWN BEFORE THE ATTEMPT, because the attempt can take the whole test host with it: an access
            // violation inside the driver leaves no result, no name and no stack for the case that caused it. The last
            // line in this file is the pass that was being created when everything stopped.
            File.AppendAllText("ui-shader-creation.log", file + " ...\r\n");

            var compiled = Adamantium.EffectsCompiler.EffectCompiler.CompileFromFile(
                Path.Combine("EffectsData", "UIFX", file));

            Assert.That(compiled.HasErrors, Is.False, Said(compiled));

            // ...and CREATING it is the other half, and the half that has killed an application: the driver's
            // shader-object compiler takes an access violation on control flow it will not have, and that happens here.
            var effect = new Effect(device, compiled.EffectData);

            File.AppendAllText("ui-shader-creation.log", file + " created\r\n");

            Assert.That(effect.Techniques.Count, Is.GreaterThan(0));
        }

        private static string Said(Adamantium.EffectsCompiler.EffectCompilerResult compiled)
        {
            var said = new System.Text.StringBuilder(compiled.ToString());

            foreach (var message in compiled.Logger.Messages) said.AppendLine().Append(message);

            return said.ToString();
        }
    }
}
