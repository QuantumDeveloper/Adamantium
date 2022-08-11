using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Effects;
using MessagePack;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        [Test]
        public void UIEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("EffectsData", "UIEffect.fx");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var result = EffectCompiler.Compile(text, path);
            
                    result.EffectData.Save("UIEffect.fx.compiled");
                    var restored = EffectData.Load("UIEffect.fx.compiled");
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
        
        [Test]
        public void BasicEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("EffectsData", "BasicEffect.fx");
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
            var path = Path.Combine("EffectsData", "UIEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);

                var memoryStream = new MemoryStream();
                MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
                MessagePackSerializer.Serialize(memoryStream, result.EffectData);
                memoryStream.Position = 0;
                var results2 = MessagePackSerializer.Deserialize<EffectData>(memoryStream);
                Assert.NotNull(results2);
            }
        }
        
        [Test]
        public void EffectDeserializationTest()
        {
            var path = Path.Combine("EffectsData", "BasicEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);
                result.EffectData.Save("BasicEffect1");
                var restored = EffectData.Load("BasicEffect1");
            }
        }
    }
}