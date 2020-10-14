using System;
using Microsoft.Build.Utilities;

namespace Adamantium.Engine.CompilerTask
{
    [Serializable]
    public class EngineItem
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

        //public ITaskItem ParentTaskItem;

        public TaskItem ToTaskItem()
        {
            var item = new TaskItem(BinaryOutputFilePath);

            // For fx.compiled we output Link used by <Content> Item
            //item.SetMetadata("CopyToOutputDirectory", "PreserveNewest");
            //item.SetMetadata("Link", OutputLink);

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
}
