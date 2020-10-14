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
            //Debugger.Launch();
            var item = JsonConvert.DeserializeObject<EngineItem>(Encoding.UTF8.GetString(Convert.FromBase64String(args[0])));
            
            EffectCompilerTool.CompileEffect(item);
        }
    }
}
