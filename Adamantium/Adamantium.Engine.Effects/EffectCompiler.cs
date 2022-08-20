using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Adamantium.DXC;
using AdamantiumVulkan.Spirv.Reflection;

namespace Adamantium.Engine.Effects
{
   /// <summary>
   /// Main class used to compile a Toolkit FX file.
   /// </summary>
   public static class EffectCompiler
   {
      public static string GetDependencyFileNameFromSourcePath(string pathToFxFile)
      {
         return FileDependencyList.GetDependencyFileNameFromSourcePath(pathToFxFile);
      }

      public static List<string> LoadDependency(string dependencyFilePath)
      {
         return FileDependencyList.FromFileRaw(dependencyFilePath);
      }

      /// <summary>
      /// Checks for changes from a dependency file.
      /// </summary>
      /// <param name="dependencyFilePath">The dependency file path.</param>
      /// <returns><c>true</c> if a file has been updated, <c>false</c> otherwise</returns>
      public static bool CheckForChanges(string dependencyFilePath)
      {
         return FileDependencyList.CheckForChanges(dependencyFilePath);
      }

      /// <summary>
      /// Compiles an effect from file.
      /// </summary>
      /// <param name="filePath">The file path.</param>
      /// <param name="flags">The flags.</param>
      /// <param name="macros">The macrosArgs.</param>
      /// <param name="includeDirectoryList">The include directory list.</param>
      /// <param name="allowDynamicCompiling">Whether or not to allow dynamic compilation.</param>
      /// <param name="dependencyFilePath">Path to dependency files.</param>
      /// <returns>The result of compilation.</returns>
      public static EffectCompilerResult CompileFromFile(string filePath, EffectCompilerFlags flags = EffectCompilerFlags.None, List<EffectData.ShaderMacro> macros = null, List<string> includeDirectoryList = null, bool allowDynamicCompiling = false, string dependencyFilePath = null)
      {
         return Compile(File.ReadAllText(filePath, Encoding.UTF8), filePath, flags, macros, includeDirectoryList, allowDynamicCompiling, dependencyFilePath);
      }

      /// <summary>
      /// Compiles an effect from the specified source code and file path.
      /// </summary>
      /// <param name="sourceCode">The source code.</param>
      /// <param name="filePath">The file path.</param>
      /// <param name="flags">The flags.</param>
      /// <param name="macrosArgs">The macrosArgs.</param>
      /// <param name="includeDirectoryList">The include directory list.</param>
      /// <param name="allowDynamicCompiling">Whether or not to allow dynamic compilation.</param>
      /// <param name="dependencyFilePath">Path to dependency files.</param>
      /// <returns>The result of compilation.</returns>
      public static EffectCompilerResult Compile(string sourceCode, string filePath, EffectCompilerFlags flags = EffectCompilerFlags.None, List<EffectData.ShaderMacro> macrosArgs = null, List<string> includeDirectoryList = null, bool allowDynamicCompiling = false, string dependencyFilePath = null)
      {
         var compiler = new EffectCompilerInternal();
         return compiler.Compile(sourceCode, filePath, flags, macrosArgs, includeDirectoryList, allowDynamicCompiling, dependencyFilePath);
      }
      
      public static EffectCompilerResult Compile(string sourceCode, string filePath, ImmutableArray<ShaderFileInfo> includes, List<EffectData.ShaderMacro> macrosArgs = null, List<string> includeDirectoryList = null)
      {
         var compiler = new EffectCompilerInternal();
         return compiler.Compile(sourceCode, filePath, includes, macrosArgs, includeDirectoryList);
      }

      /// <summary>
      /// Disassembles a shader HLSL bytecode to asm code.
      /// </summary>
      /// <param name="shader">The shader.</param>
      /// <returns>A string containing asm code decoded from HLSL bytecode.</returns>
      public static SpirvReflectionResult DisassembleShader(EffectData.Shader shader)
      {
         var compiler = new EffectCompilerInternal();
         return compiler.DisassembleShader(shader);
      }

      /// <summary>
      /// Builds effect data from the provided bytecode.
      /// </summary>
      /// <param name="shaderSource">The bytecode list to for the provided effect.</param>
      /// <returns>Built effect data.</returns>
      public static EffectData Compile(params DxcCompilationResult[] shaderSource)
      {
         var compiler = new EffectCompilerInternal();
         return compiler.Build(shaderSource);
      }
   }
}
