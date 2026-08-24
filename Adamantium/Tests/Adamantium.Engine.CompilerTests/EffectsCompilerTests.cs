using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.EffectsCompiler;
using MessagePack;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        /// <summary>A compile that FAILS must say so with the compiler's own messages. Dereferencing EffectData without
        /// asking whether the compile succeeded turned every shader error into a bare NullReferenceException with the
        /// reason thrown away - which is why this sat red without anyone being able to tell what it was complaining about.
        /// </summary>
        private static EffectData CompileOrFail(string path)
        {
            var result = EffectCompiler.CompileFromFile(path);
            var messages = string.Join(Environment.NewLine, result.Logger.Messages);
            Assert.That(result.HasErrors, Is.False, $"{path} failed to compile:{Environment.NewLine}{messages}");
            Assert.That(result.EffectData, Is.Not.Null, $"{path} compiled without errors but produced no EffectData");
            return result.EffectData;
        }

        [Test]
        public void UIEffectParsingTest()
        {
            var path = Path.Combine("CompilerEffects", "UIEffect.fx");
            if (!File.Exists(path)) Assert.Ignore($"missing test asset: {path}");

            var effectData = CompileOrFail(path);
            effectData.Save("UIEffect.fx.compiled");
            Assert.That(EffectData.Load("UIEffect.fx.compiled"), Is.Not.Null, "the saved effect did not load back");
        }
        
        [Test]
        public void BasicEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("CompilerEffects", "BasicEffect.fx");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var result = EffectCompiler.CompileFromFile(path);
                    
                    var effectParameters = new List<string>();
                    var parametersIdentity = new HashSet<string>();
                    foreach (var shader in result.EffectData.Shaders)
                    {
                        foreach (var constantBuffer in shader.ConstantBuffers)
                        {
                            foreach (var parameter in constantBuffer.Parameters)
                            {
                                if (parametersIdentity.Contains(parameter.Name)) continue;

                                parametersIdentity.Add(parameter.Name);
                                var name = char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
                                effectParameters.Add(name);
                                //textGenerator.WriteLine($"{name} = Parameters[\"{parameter.Name}\"];");
                            }
                        }

                        foreach (var resource in shader.ResourceParameters)
                        {
                            if (parametersIdentity.Contains(resource.Name) || resource.Name == "type.$Globals") continue;

                            parametersIdentity.Add(resource.Name);
                            var name = char.ToUpper(resource.Name[0]) + resource.Name.Substring(1);
                            effectParameters.Add(name);
                            //textGenerator.WriteLine($"{name} = Parameters[\"{resource.Name}\"];");
                        }
                    }
            
                    //result.EffectData.Save("BasicEffect.fx.compiled");
                    //var restored = EffectData.Load("BasicEffect.fx.compiled");
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }

        [Test]
        public void EffectSerializationTest()
        {
            var path = Path.Combine("CompilerEffects", "UIEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);

                var memoryStream = new MemoryStream();
                //MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
                MessagePackSerializer.Serialize(memoryStream, result.EffectData);
                memoryStream.Position = 0;
                var results2 = MessagePackSerializer.Deserialize<EffectData>(memoryStream);
                Assert.NotNull(results2);
            }
        }
        
        [Test]
        public void EffectDeserializationTest()
        {
            var path = Path.Combine("CompilerEffects", "BasicEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);
                result.EffectData.Save("BasicEffect1");
                var restored = EffectData.Load("BasicEffect1");
            }
        }
    }
}