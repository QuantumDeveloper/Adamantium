using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Adamantium.Engine.CompilerTask
{
    [Serializable]
    public class CompilerDependencyTask : Task
    {
        [Serializable]
        protected class EngineItem
        {
            public string Name;

            public string LinkName;

            public string InputFilePath;

            public string BinaryOutputFilePath;

            public string CompiledOutputFilePath;

            public string OutputLink;

            public bool GenerateBinary;

            public string OutputBinaryFile;

            public bool DynamicCompiling;

            public bool OutputCs;

            public string OutputCsFile;

            public string OutputNamespace;

            public string OutputClassName;

            public string OutputFieldName;

            public ITaskItem ParentTaskItem;

            public TaskItem ToTaskItem()
            {
                var item = new TaskItem(BinaryOutputFilePath);

                // For fx.compiled we output Link used by <Content> Item
                item.SetMetadata("CopyToOutputDirectory", "PreserveNewest");
                item.SetMetadata("Link", OutputLink);

                return item;
            }

            public override string ToString()
            {
                return
                   $"Name: {Name}, DynamicCompiling: {DynamicCompiling}, LinkName: {LinkName}, " +
                   $"InputFilePath: {InputFilePath}, OutputFilePath: {BinaryOutputFilePath}, OutputCsFile: {OutputCsFile}, " +
                   $"OutputNamespace: {OutputNamespace}, OutputClassName: {OutputClassName}, OutputFieldName: {OutputFieldName}, OutputCs: {OutputCs}";
            }
        }

        [Required]
        public ITaskItem ProjectDirectory { get; set; }

        [Required]
        public ITaskItem IntermediateDirectory { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] ContentFiles { get; set; }

        [Output]
        public ITaskItem[] CompiledFiles { get; set; }

        public bool GenerateBinary { get; set; }

        public bool Debug { get; set; }

        public string RootNamespace { get; set; }

        public bool CompileInCS { get; set; }

        public bool DynamicCompiling { get; set; }

        protected string currentExtension = String.Empty;

        protected bool parseLogMessages;

        protected virtual void Initialize() { }

        public sealed override bool Execute()
        {
            bool hasErrors = false;

            try
            {
                Debugger.Launch();
                Initialize();

                var contentFiles = new List<ITaskItem>();
                var compileFiles = new List<ITaskItem>();
                Stopwatch timer = Stopwatch.StartNew();
                foreach (ITaskItem file in Files)
                {
                    var item = GetEngineItem(file);
                    Log.LogMessage(MessageImportance.Low, "Process item {0}", item);

                    if (!ProcessItem(item))
                    {
                        hasErrors = true;
                    }

                    var returnedItem = item.ToTaskItem();

                    //CS files are copied to obj as compiled
                    if (item.OutputCs)
                    {
                        compileFiles.Add(returnedItem);
                    }
                    // Fx.compiled and all other are copied to output
                    else
                    {
                        contentFiles.Add(returnedItem);
                    }
                }
                timer.Stop();
                var elapsed = timer.ElapsedMilliseconds;
                ContentFiles = contentFiles.ToArray();
                CompiledFiles = compileFiles.ToArray();
            }
            catch (Exception exception)
            {
                Log.LogError("Unexpected exception: {0}", exception);
                hasErrors = true;
            }

            return !hasErrors;
        }

        private EngineItem GetEngineItem(ITaskItem item)
        {
            var data = new EngineItem
            {
                Name = item.ItemSpec,
                LinkName = item.GetMetadata("Link", item.ItemSpec),
                OutputNamespace = item.GetMetadata("OutputNamespace", RootNamespace),
                OutputFieldName = item.GetMetadata("OutputFieldName", "bytecode"),
                GenerateBinary = item.GetMetadata("GenerateBinary", GenerateBinary),
                DynamicCompiling = item.GetMetadata("DynamicCompiling", DynamicCompiling),
                OutputCs = item.GetMetadata("OutputCs", CompileInCS),
                ParentTaskItem = item,
            };

            data.OutputClassName = item.GetMetadata("OutputClassName", Path.GetFileNameWithoutExtension(data.LinkName));
            data.OutputCsFile = item.GetMetadata("OutputCsFileName", Path.GetFileNameWithoutExtension(data.LinkName) + ".Generated.cs");
            data.OutputBinaryFile = item.GetMetadata("OutputBinaryFileName",
               Path.GetFileNameWithoutExtension(data.LinkName) + currentExtension);
            var outputCsFile = item.GetMetadata<string>("LastGenOutput", null);

            if (outputCsFile != null && outputCsFile.EndsWith(".cs", StringComparison.InvariantCultureIgnoreCase))
            {
                data.OutputCsFile = outputCsFile;
            }

            // Full path to the generated Output FilePath either 
            // For fx.compiled: $(ProjectDir)/obj/Debug/XXX/YYY.fx.compiled 
            // For cs: $(ProjectDir)/XXX/YYY.cs
            if (data.OutputCs)
            {
                data.CompiledOutputFilePath = Path.Combine(IntermediateDirectory.ItemSpec, Path.Combine(Path.GetDirectoryName(data.Name) ?? string.Empty, data.OutputCsFile));
            }

            //Generate binary fx file in all cases
            data.OutputLink = Path.ChangeExtension(data.LinkName, currentExtension);
            data.BinaryOutputFilePath = Path.Combine(IntermediateDirectory.ItemSpec, data.OutputLink);

            // Full path to the input file
            data.InputFilePath = Path.Combine(ProjectDirectory.ItemSpec, data.Name);

            return data;
        }

        protected virtual bool ProcessItem(EngineItem item)
        {
            return true;
        }
    }
}
