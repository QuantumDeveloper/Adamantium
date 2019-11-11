using System.IO;
using Adamantium.Engine.Compiler.Effects;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        [Test]
        public void EffectParsingTest()
        {
            var path = Path.Combine("EffectsData", "UIEffect.fx");
            var result = EffectCompiler.CompileFromFile(path);
        }
    }
}