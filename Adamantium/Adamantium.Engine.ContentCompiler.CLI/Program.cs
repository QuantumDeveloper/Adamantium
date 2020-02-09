using Adamantium.Engine.Compiler.Effects;
using Adamantium.Engine.Core.Effects;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Adamantium.Build.Core;
using System.Diagnostics;

namespace Adamantium.Engine.ContentCompiler.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Debugger.Launch();
            var item = JsonConvert.DeserializeObject<EngineItem>(args[0]);

            //item.OutputLink = Path.ChangeExtension(item.LinkName, EffectData.CompiledExtension);
            //item.BinaryOutputFilePath = Path.Combine(IntermediateDirectory.ItemSpec, item.OutputLink);

            //// Full path to the input file
            //item.InputFilePath = Path.Combine(ProjectDirectory.ItemSpec, item.Name);

            //var compilerFlags = Debug ? EffectCompilerFlags.Debug : EffectCompilerFlags.None;
            //compilerFlags |= EffectCompilerFlags.OptimizationLevel3;
            //if (!string.IsNullOrEmpty(CompilerFlags))
            //{
            //    compilerFlags |= (EffectCompilerFlags)Enum.Parse(typeof(EffectCompilerFlags), CompilerFlags);
            //}
            
            EffectCompilerTool.CompileEffect(item);
        }
    }
}
