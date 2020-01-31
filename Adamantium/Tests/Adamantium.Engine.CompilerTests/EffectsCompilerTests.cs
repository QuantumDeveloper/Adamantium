using System;
using System.IO;
using Adamantium.Engine.Compiler.Effects;
using Adamantium.Engine.Core.Effects;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        [Test]
        public void EffectParsingTest()
        {
            try
            {
                var path = Path.Combine("EffectsData", "UIEffect.fx");
                var result = EffectCompiler.CompileFromFile(path);
            
                result.EffectData.Save("UIEffect.fx.compiled");
                var restored = EffectData.Load("UIEffect.fx.compiled");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}