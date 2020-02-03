using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Compiler.Effects;
using Logger = Adamantium.Engine.Core.Logger;

namespace Adamantium.Engine.CompilerTask
{
    public class EffectCompilerTask : ContentCompilerTask
    {
        public string CompilerFlags { get; set; }

        protected override void Initialize()
        {
            parseLogMessages = true;
            currentExtension = "." + EffectData.CompiledExtension;
            base.Initialize();
        }

        protected override Logger ProcessFileAndGetLogResults(string inputFilePath, string dependencyFilePath, EngineItem item)
        {
            //Generate binary fx file in all cases
            item.OutputLink = Path.ChangeExtension(item.LinkName, EffectData.CompiledExtension);
            item.BinaryOutputFilePath = Path.Combine(IntermediateDirectory.ItemSpec, item.OutputLink);

            // Full path to the input file
            item.InputFilePath = Path.Combine(ProjectDirectory.ItemSpec, item.Name);

            var compilerFlags = Debug ? EffectCompilerFlags.Debug : EffectCompilerFlags.None;
            compilerFlags |= EffectCompilerFlags.OptimizationLevel3;
            if (!string.IsNullOrEmpty(CompilerFlags))
            {
                compilerFlags |= (EffectCompilerFlags)Enum.Parse(typeof(EffectCompilerFlags), CompilerFlags);
            }

            var compilerResult = EffectCompiler.CompileFromFile(inputFilePath,
                                                              compilerFlags,
                                                              null,
                                                              null,
                                                              item.DynamicCompiling,
                                                              dependencyFilePath);
            if (!compilerResult.HasErrors && compilerResult.EffectData != null)
            {
                if (item.OutputCs)
                {
                    var outputPath = item.CompiledOutputFilePath;
                    CreateDirectoryIfNotExists(outputPath);
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
                    CreateDirectoryIfNotExists(outputPath);
                    compilerResult.EffectData.Save(outputPath);
                }
            }

            return compilerResult.Logger;
        }
    }
}
