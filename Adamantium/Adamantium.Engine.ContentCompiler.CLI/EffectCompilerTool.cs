using Adamantium.Build.Core;
using Adamantium.Core;
using Adamantium.Engine.Compiler.Effects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Adamantium.Engine.ContentCompiler.CLI
{
    internal class EffectCompilerTool
    {
        public static void CompileEffect(EngineItem item)
        {
            var compilerResult = EffectCompiler.CompileFromFile(item.InputFilePath);
            if (!compilerResult.HasErrors && compilerResult.EffectData != null)
            {
                if (item.OutputCs)
                {
                    var outputPath = item.CompiledOutputFilePath;
                    DirectoryHelper.CreateDirectoryFromFile(outputPath);
                    var codeWriter = new EffectDataCodeWriter
                    {
                        Namespace = item.OutputNamespace,
                        ClassName = item.OutputClassName,
                        FieldName = item.OutputFieldName,
                    };

                    using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Write))
                        codeWriter.Write(compilerResult.EffectData, new StreamWriter(stream, Encoding.UTF8));
                }

                if (item.GenerateBinary)
                {
                    var outputPath = item.BinaryOutputFilePath;
                    DirectoryHelper.CreateDirectoryFromFile(outputPath);
                    compilerResult.EffectData.Save(outputPath);
                }
            }

            Console.WriteLine(JsonConvert.SerializeObject(compilerResult.Logger));
        }
    }
    
    internal class DirectoryHelper 
    {
        public static void CreateDirectoryFromFile(string filePath)
        {
            var dependencyDirectoryPath = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(dependencyDirectoryPath);
        }
    }

}
