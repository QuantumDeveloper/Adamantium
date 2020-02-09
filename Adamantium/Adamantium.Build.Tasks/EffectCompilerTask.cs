using System.Diagnostics;
using System.IO;
using Logger = Adamantium.Core.Logger;
using Adamantium.Build.Core;
using Newtonsoft.Json;

namespace Adamantium.Build.Tasks
{
    public class EffectCompilerTask : ContentCompilerTask
    {
        public string CompilerFlags { get; set; }

        protected override void Initialize()
        {
            parseLogMessages = true;
            //currentExtension = "." + EffectData.CompiledExtension;
            base.Initialize();
        }

        protected override Logger ProcessFileAndGetLogResults(string inputFilePath, string dependencyFilePath, EngineItem item)
        {
            //Generate binary fx file in all cases
            item.OutputLink = Path.ChangeExtension(item.LinkName, "fx.compiled");
            item.BinaryOutputFilePath = Path.Combine(IntermediateDirectory.ItemSpec, item.OutputLink);

            // Full path to the input file
            item.InputFilePath = Path.Combine(ProjectDirectory.ItemSpec, item.Name);

            var startInfo = new ProcessStartInfo();
            startInfo.RedirectStandardOutput = true;
            startInfo.FileName = ToolPath;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = JsonConvert.SerializeObject(item);

            var process = Process.Start(startInfo);
            process.WaitForExit();

            var output = process.StandardOutput.ReadToEnd();

            var logger = JsonConvert.DeserializeObject<Logger>(output);
            return logger;
        }
    }
}
