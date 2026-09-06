using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Adamantium.EffectsCompiler;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering
{
    /// <summary>Writes a shipped effect's SPIR-V to disk, one file per entry point, so it can be disassembled.
    /// <para>A device loss that the validation layers do not report is a fault inside shader EXECUTION, and no amount of
    /// reading the .fx answers what was actually emitted. Ignored unless ADAM_DUMP_EFFECT names an effect, so it never
    /// runs in an ordinary pass.</para></summary>
    [TestFixture]
    public class DumpEffectSpirvTests
    {
        [Test]
        public void DumpsTheEffectNamedByEnvironment()
        {
            var name = Environment.GetEnvironmentVariable("ADAM_DUMP_EFFECT");
            if (string.IsNullOrEmpty(name)) Assert.Ignore("ADAM_DUMP_EFFECT not set");

            var outDir = Environment.GetEnvironmentVariable("ADAM_DUMP_DIR") ?? Path.GetTempPath();
            Directory.CreateDirectory(outDir);

            var typeName = $"Adamantium.UI.Effects.Generated.{name}";
            var effect = Type.GetType($"{typeName}, Adamantium.UI.FX")
                         ?? AppDomain.CurrentDomain.GetAssemblies()
                             .Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
            Assert.That(effect, Is.Not.Null, $"{typeName} is not in this build");

            var field = effect.GetField("bytecode", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, $"{typeName} no longer carries its compiled bytecode");

            var data = (EffectData)field.GetValue(null);
            var written = 0;
            foreach (var shader in data.Shaders)
            {
                var file = Path.Combine(outDir, $"{name}.{shader.Name}.{written:D3}.spv");
                File.WriteAllBytes(file, shader.Bytecode);
                TestContext.WriteLine($"{shader.Name} -> {file} ({shader.Bytecode.Length} bytes)");
                written++;
            }

            Assert.That(written, Is.GreaterThan(0), "the effect carries no shaders");
        }
    }
}
