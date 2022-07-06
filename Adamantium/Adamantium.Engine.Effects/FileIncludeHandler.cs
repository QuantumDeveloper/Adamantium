using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AdamantiumVulkan.Common;
using AdamantiumVulkan.Shaders.Interop;
using ShadercIncludeResult = AdamantiumVulkan.Shaders.ShadercIncludeResult;

namespace Adamantium.Engine.Effects
{
    internal class FileIncludeHandler
    {
        public readonly Stack<string> CurrentDirectory;

        public readonly Dictionary<string, FileItem> FileResolved;
        public readonly List<string> IncludeDirectories;
        public ShadercIncludeResolveFn IncludeResolverCallback { get; }
        public ShadercIncludeResultReleaseFn IncludeReleaseCallback { get; }
        public EffectCompilerLogger Logger;
        private IntPtr resultPtr;

        public FileIncludeHandler()
        {
            IncludeDirectories = new List<string>();
            CurrentDirectory = new Stack<string>();
            FileResolved = new Dictionary<string, FileItem>();
            IncludeResolverCallback = IncludeResolver;
            IncludeReleaseCallback = IncludeReleaser;
        }

        #region Include Members

        private IntPtr IncludeResolver(IntPtr userData, string requestedSource, int type, string requestingSource, ulong includeDepth)
        {
            var currentDirectory = CurrentDirectory.Peek();
            var finalPath = Path.Combine(currentDirectory, requestedSource);
            if (!File.Exists(finalPath))
            {
                Logger.Error($"Unable to find file [{finalPath}]");
            }

            var content = File.ReadAllText(finalPath);

            var shaderResult = new ShadercIncludeResult();
            shaderResult.Content = content;
            shaderResult.Content_length = (ulong)shaderResult.Content.Length;
            shaderResult.Source_name = requestedSource;
            shaderResult.Source_name_length = (ulong)shaderResult.Source_name.Length;
            var innerStruct = shaderResult.ToInternal();

            resultPtr = MarshalUtils.MarshalStructToPtr(innerStruct);
            return resultPtr;
        }
        ///<summary>
        /// An includer callback type for destroying an include result.
        ///</summary>
        private void IncludeReleaser(IntPtr userData, AdamantiumVulkan.Shaders.Interop.ShadercIncludeResult includeResult)
        {
            Marshal.FreeHGlobal(resultPtr);
        }

        #endregion

        #region Nested type: Tuple

        internal class FileItem
        {
            public FileItem(string fileName, string filePath, DateTime modifiedTime)
            {
                FileName = fileName;
                FilePath = filePath;
                ModifiedTime = modifiedTime;
            }

            public string FileName;

            public string FilePath;

            public DateTime ModifiedTime;
        }

        #endregion
    }
}
