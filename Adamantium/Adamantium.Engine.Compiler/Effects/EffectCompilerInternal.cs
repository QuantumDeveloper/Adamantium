using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Effects;
using Adamantium.Mathematics;
using AdamantiumVulkan.Shaders;
using AdamantiumVulkan.SPIRV.Cross;
using AdamantiumVulkan.SPIRV.Reflection;

namespace Adamantium.Engine.Compiler.Effects
{
    internal class EffectCompilerInternal
    {
        private static readonly Regex MatchVariableArray = new Regex(@"(.*)\[(\d+)\]");
        private static readonly Regex splitSODeclartionRegex = new Regex(@"\s*;\s*");
        private static readonly Regex soDeclarationItemRegex = new Regex(@"^\s*(\d+)?\s*:?\s*([A-Za-z][A-Za-z0-9_]*)(\.[xyzw]+|\.[rgba]+)?$");
        private static readonly Regex soSemanticIndex = new Regex(@"^([A-Za-z][A-Za-z0-9_]*?)([0-9]*)?$");
        private static readonly Regex replaceBackSlash = new Regex(@"\\+");
        private static readonly List<char> xyzwrgbaComponents = new List<char>() { 'x', 'y', 'z', 'w', 'r', 'g', 'b', 'a' };

        private static readonly Dictionary<string, ValueConverter> ValueConverters =
            new Dictionary<string, ValueConverter>()
              {
                {"float4", new ValueConverter("float4", 4, ToFloat, (compiler, parameters) => new Vector4F((float)parameters[0], (float)parameters[1], (float)parameters[2], (float)parameters[3]))},
                {"float3", new ValueConverter("float3", 3, ToFloat, (compiler, parameters) => new Vector3F((float)parameters[0], (float)parameters[1], (float)parameters[2]))},
                {"float2", new ValueConverter("float2", 2, ToFloat, (compiler, parameters) => new Vector2F((float)parameters[0], (float)parameters[1]))},
                {"float", new ValueConverter("float", 1, ToFloat, (compiler, parameters) => (float)parameters[0])},
              };

        private readonly List<string> currentExports = new List<string>();
        private EffectCompilerFlags compilerFlags;
        private EffectData effectData;

        private EffectData.Effect effect;

        private List<string> includeDirectoryList;
        public IncludeFileDelegate IncludeFileDelegate;
        //private Include includeHandler;
        private EffectCompilerLogger logger;
        private List<EffectData.ShaderMacro> macros;
        private EffectParserResult parserResult;
        private EffectData.Pass pass;
        private string preprocessorText;
        private EffectData.Technique technique;
        private int nextSubPassCount;
        private int profile;

        //private StreamOutputElement[] currentStreamOutputElements;

        private FileDependencyList dependencyList;

        private AdamantiumVulkan.Shaders.Interop.ShadercIncludeResult ShadercIncludeResolveFn(System.IntPtr user_data, int type, IntPtr requested_source, ulong include_depth, IntPtr requesting_source)
        {
            IntPtr source = (IntPtr)type;
            IntPtr ssource = (IntPtr)include_depth;

            var includePath = Marshal.PtrToStringAnsi(user_data);
            //var source = Marshal.PtrToStringAnsi(requested_source);
            //var r_source = Marshal.PtrToStringAnsi(requesting_source);
            var finalPath = Path.Combine("shaders\\TerrainGenShaders", includePath);
            var text = File.ReadAllText(finalPath);
            var result = new AdamantiumVulkan.Shaders.ShadercIncludeResult();
            result.User_data = user_data;
            result.Content = text;
            result.Content_length = (ulong)text.Length;
            result.Source_name = Marshal.PtrToStringAnsi(requested_source);
            result.Source_name_length = (ulong)result.Source_name.Length;
            return result.ToInternal();
        }
        

        private IntPtr IncludeResolver(IntPtr userData, string requestedSource, int type, string requesting_source, ulong includeDepth)
        {
            return IntPtr.Zero;
        }

        private void IncludeResultRelease(IntPtr userData, AdamantiumVulkan.Shaders.Interop.ShadercIncludeResult includeResult)
        {
            
        }

        public EffectCompilerInternal()
        {
        }

        /// <summary>
        /// Checks for changes from a dependency file.
        /// </summary>
        /// <param name="dependencyFilePath">The dependency file path.</param>
        /// <returns><c>true</c> if a file has been updated, <c>false</c> otherwise</returns>
        public bool CheckForChanges(string dependencyFilePath)
        {
            return FileDependencyList.FromFile(dependencyFilePath).CheckForChanges();
        }

        /// <summary>
        /// Compiles an effect from file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="flags">The flags.</param>
        /// <param name="macros">The macrosArgs.</param>
        /// <param name="includeDirectoryList">The include directory list.</param>
        /// <param name="allowDynamicCompiling">if set to <c>true</c> [allo dynamic compiling].</param>
        /// <param name="dependencyFilePath">The dependency file path.</param>
        /// <returns>The result of compilation.</returns>
        public EffectCompilerResult CompileFromFile(string filePath, EffectCompilerFlags flags = EffectCompilerFlags.None, List<EffectData.ShaderMacro> macros = null, List<string> includeDirectoryList = null, bool allowDynamicCompiling = false, string dependencyFilePath = null)
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
        /// <param name="allowDynamicCompiling">if set to <c>true</c> [allow dynamic compiling].</param>
        /// <param name="dependencyFilePath">The dependency file path.</param>
        /// <returns>The result of compilation.</returns>
        public EffectCompilerResult Compile(string sourceCode, string filePath, EffectCompilerFlags flags = EffectCompilerFlags.None, List<EffectData.ShaderMacro> macrosArgs = null, List<string> includeDirectoryList = null, bool allowDynamicCompiling = false, string dependencyFilePath = null)
        {
            var nativeMacros = new List<EffectData.ShaderMacro>();
            if (macrosArgs != null)
            {
                foreach (var macro in macrosArgs)
                {
                    nativeMacros.Add(new EffectData.ShaderMacro(macro.Name, macro.Value));
                }
            }

            compilerFlags = flags;
            this.macros = nativeMacros;
            this.includeDirectoryList = includeDirectoryList ?? new List<string>();

            InternalCompile(sourceCode, filePath);

            var result = new EffectCompilerResult(dependencyFilePath, effectData, logger);

            // Handle Dependency FilePath
            if (!result.HasErrors && dependencyFilePath != null && dependencyList != null)
            {
                dependencyList.Save(dependencyFilePath);

                // If dynamic compiling, store the parameters used to compile this effect directly in the bytecode
                if (allowDynamicCompiling && result.EffectData != null)
                {
                    var compilerArguments = new EffectData.CompilerArguments { FilePath = filePath, DependencyFilePath = dependencyFilePath, Macros = new List<EffectData.ShaderMacro>(), IncludeDirectoryList = new List<string>() };
                    if (macrosArgs != null)
                    {
                        compilerArguments.Macros.AddRange(macrosArgs);
                    }

                    if (includeDirectoryList != null)
                    {
                        compilerArguments.IncludeDirectoryList.AddRange(includeDirectoryList);
                    }

                    result.EffectData.Description.Arguments = compilerArguments;
                }
            }

            return result;
        }

        /// <summary>
        /// Disassembles a shader HLSL bytecode to asm code.
        /// </summary>
        /// <param name="shader">The shader.</param>
        /// <returns>A string containing asm code decoded from HLSL bytecode.</returns>
        public SpirvReflectionResult DisassembleShader(EffectData.Shader shader)
        {
            return new SpirvReflection(shader.Bytecode).Disassemble();
        }

        public EffectData Build(params CompilationResult[] bytecodes)
        {
            if (bytecodes == null || bytecodes.Length == 0)
                throw new ArgumentException("Expected at least one bytecode", "bytecodes");

            SetupEffectData(Guid.NewGuid().ToString("N"));

            effectData.Description.ShareConstantBuffers = true;

            SetupTechnique("Technique0", new SourceSpan());
            SetupPass("Pass0", new SourceSpan());

            for (var i = 0; i < bytecodes.Length; i++)
                ProcessShader(bytecodes[i]);

            CheckPassConsistency(new SourceSpan());

            return effectData;
        }

        private void ProcessShader(CompilationResult shaderBytecode)
        {
//            var shaderProfile = shaderBytecode.GetVersion();
//            var shaderVersionText = shaderProfile.ToString();
//            var typePrefix = shaderProfile.GetTypePrefix();

            var shaderType = VersionToShaderType(shaderBytecode.ShaderStage);

            var shader = CreateEffectShader(shaderType, shaderBytecode.Name, shaderBytecode);
            ProcessShaderData(shaderType, shaderBytecode, shader);
        }

        private void InternalCompile(string sourceCode, string fileName)
        {
            effectData = null;

            fileName = fileName.Replace(@"\\", @"\");

            logger = new EffectCompilerLogger();
            var parser = new EffectParser { Logger = logger, IncludeFileCallback = IncludeFileDelegate };
            parser.Macros.AddRange(macros);
            parser.IncludeDirectoryList.AddRange(includeDirectoryList);

            parserResult = parser.Parse(sourceCode, fileName);
            dependencyList = parserResult.DependencyList;

            if (logger.HasErrors) return;

            // Get back include handler.
            //includeHandler = parserResult.IncludeHandler;

            SetupEffectData(Path.GetFileNameWithoutExtension(fileName));

            if (parserResult.Shader != null)
            {
                foreach (var techniqueAst in parserResult.Shader.Techniques)
                {
                    HandleTechnique(techniqueAst);
                }
            }
        }

        private void SetupEffectData(string name)
        {
            effectData = new EffectData
            {
                Shaders = new List<EffectData.Shader>(),
                Description = new EffectData.Effect()
                {
                    Name = name,
                    Techniques = new List<EffectData.Technique>(),
                }
            };

            effect = effectData.Description;
        }

        private void HandleTechnique(Ast.Technique techniqueAst)
        {
            SetupTechnique(techniqueAst.Name, techniqueAst.Span);

            // Process passes for this technique
            foreach (var passAst in techniqueAst.Passes)
            {
                HandlePass(passAst);
            }
        }

        private void SetupTechnique(string techniqueName, SourceSpan sourceSpan)
        {
            technique = new EffectData.Technique()
            {
                Name = techniqueName,
                Passes = new List<EffectData.Pass>()
            };

            // Check that the name of this technique is not already used.
            if (technique.Name != null)
            {
                foreach (var registeredTechnique in effect.Techniques)
                {
                    if (registeredTechnique.Name == technique.Name)
                    {
                        logger.Error("A technique with the same name [{0}] already exist", sourceSpan, technique.Name);
                        break;
                    }
                }
            }

            // Adds the technique to the list of technique for the current effect
            effect.Techniques.Add(technique);
        }

        private void HandlePass(Ast.Pass passAst)
        {
            SetupPass(passAst.Name, passAst.Span);

            if (nextSubPassCount > 0)
            {
                pass.IsSubPass = true;
                nextSubPassCount--;
            }

            // Process all statements inside this pass.
            foreach (var statement in passAst.Statements)
            {
                var expressionStatement = statement as Ast.ExpressionStatement;
                if (expressionStatement == null)
                    continue;
                HandleExpression(expressionStatement.Expression);
            }

            CheckPassConsistency(passAst.Span);
        }

        private void SetupPass(string name, SourceSpan span)
        {
            pass = new EffectData.Pass()
            {
                Name = name,
                Pipeline = new EffectData.Pipeline(),
                Properties = new CommonData.PropertyCollection()
            };

            // Clear current exports
            currentExports.Clear();

            // Check that the name of this pass is not already used.
            if (pass.Name != null)
            {
                foreach (var registeredPass in technique.Passes)
                {
                    if (registeredPass.Name == pass.Name)
                    {
                        logger.Error("A pass with the same name [{0}] already exist", span, pass.Name);
                        break;
                    }
                }
            }

            // Adds the pass to the list of pass for the current technique
            technique.Passes.Add(pass);
        }

        private void CheckPassConsistency(SourceSpan span)
        {
            // Check pass consistency (mainly for the GS)
            var gsLink = pass.Pipeline[EffectShaderType.Geometry];
            if (gsLink != null && gsLink.IsNullShader && (gsLink.StreamOutputRasterizedStream >= 0 /*|| gsLink.StreamOutputElements != null*/))
            {
                if (pass.Pipeline[EffectShaderType.Vertex] == null || pass.Pipeline[EffectShaderType.Vertex].IsNullShader)
                    logger.Error("Cannot specify StreamOutput for null geometry shader / vertex shader", span);
                else
                {
                    // For null geometry shaders with StreamOutput, directly use the VertexShader
                    gsLink.Index = pass.Pipeline[EffectShaderType.Vertex].Index;
                    gsLink.ImportName = pass.Pipeline[EffectShaderType.Vertex].ImportName;
                }
            }
        }

        private void HandleExpression(Ast.Expression expression)
        {
            if (expression is Ast.AssignExpression)
            {
                HandleAssignExpression((Ast.AssignExpression)expression);
            }
            else if (expression is Ast.MethodExpression)
            {
                HandleMethodExpression((Ast.MethodExpression)expression);
            }
            else
            {
                logger.Error("Unsupported expression type [{0}]", expression.Span, expression.GetType().Name);
            }
        }

        private void HandleAssignExpression(Ast.AssignExpression expression)
        {
            switch (expression.Name.Text)
            {
                case "Export":
                    HandleExport(expression.Value);
                    break;
                case EffectData.PropertyKeys.Blending:
                case EffectData.PropertyKeys.DepthStencil:
                case EffectData.PropertyKeys.Rasterizer:
                    HandleAttribute<string>(expression);
                    break;
                case EffectData.PropertyKeys.BlendingColor:
                    HandleAttribute<Vector4F>(expression);
                    break;
                case EffectData.PropertyKeys.BlendingSampleMask:
                    HandleAttribute<uint>(expression);
                    break;
                case EffectData.PropertyKeys.DepthStencilReference:
                    HandleAttribute<int>(expression);
                    break;
                case "ShareConstantBuffers":
                    HandleShareConstantBuffers(expression.Value);
                    break;
                case "EffectName":
                    HandleEffectName(expression.Value);
                    break;
                case "SubPassCount":
                    HandleSubPassCount(expression.Value);
                    break;
                case "Preprocessor":
                    HandlePreprocessor(expression.Value);
                    break;
                case "Language":
                    HandleLanguage(expression.Value);
                    break;
                case "Profile":
                    HandleProfile(expression.Value);
                    break;
                case "VertexShader":
                    CompileShader(EffectShaderType.Vertex, expression.Value);
                    break;
                case "FragmentShader":
                    CompileShader(EffectShaderType.Fragment, expression.Value);
                    break;
                case "GeometryShader":
                    CompileShader(EffectShaderType.Geometry, expression.Value);
                    break;
                case "StreamOutput":
                    HandleStreamOutput(expression.Value);
                    break;
                case "StreamOutputRasterizedStream":
                    HandleStreamOutputRasterizedStream(expression.Value);
                    break;
                case "DomainShader":
                    CompileShader(EffectShaderType.Domain, expression.Value);
                    break;
                case "HullShader":
                    CompileShader(EffectShaderType.Hull, expression.Value);
                    break;
                case "ComputeShader":
                    CompileShader(EffectShaderType.Compute, expression.Value);
                    break;
                default:
                    HandleAttribute(expression);
                    break;
            }
        }

        private void HandleStreamOutputRasterizedStream(Ast.Expression expression)
        {
            object value;
            if (!ExtractValue(expression, out value))
                return;

            if (!(value is int))
            {
                logger.Error("StreamOutputRasterizedStream value [{0}] must be an integer", expression.Span, value);
                return;
            }

            if (pass.Pipeline[EffectShaderType.Geometry] == null)
            {
                pass.Pipeline[EffectShaderType.Geometry] = new EffectData.ShaderLink();
            }

            pass.Pipeline[EffectShaderType.Geometry].StreamOutputRasterizedStream = (int)value;
        }

        private void HandleStreamOutput(Ast.Expression expression)
        {
//            var values = ExtractStringOrArrayOfStrings(expression);
//            if (values == null) return;
//
//            if (values.Count == 0 || values.Count > 4)
//            {
//                logger.Error("Invalid number [{0}] of stream output declarations. Maximum allowed is 4", expression.Span, values.Count);
//                return;
//            }
//
//            var elements = new List<StreamOutputElement>();
//
//            int streamIndex = 0;
//            foreach (var soDeclarationTexts in values)
//            {
//                if (string.IsNullOrEmpty(soDeclarationTexts))
//                {
//                    logger.Error("StreamOutput declaration cannot be null or empty", expression.Span);
//                    return;
//                }
//
//                // Parse a single string "[<slot> :] <semantic>[<index>][.<mask>]; [[<slot> :] <semantic>[<index>][.<mask>][;]]"
//                var text = soDeclarationTexts.Trim(' ', '\t', ';');
//                var declarationTextItems = splitSODeclartionRegex.Split(text);
//                foreach (var soDeclarationText in declarationTextItems)
//                {
//                    StreamOutputElement element;
//                    if (!ParseStreamOutputElement(soDeclarationText, expression.Span, out element))
//                    {
//                        return;
//                    }
//
//                    element.Stream = streamIndex;
//                    elements.Add(element);
//                }
//
//                streamIndex++;
//            }
//
//            if (elements.Count == 0)
//            {
//                logger.Error("Invalid number [0] of stream output declarations. Expected > 0", expression.Span);
//                return;
//            }
//
//            if (pass.Pipeline[EffectShaderType.Geometry] == null)
//            {
//                pass.Pipeline[EffectShaderType.Geometry] = new EffectData.ShaderLink();
//            }
//
//            pass.Pipeline[EffectShaderType.Geometry].StreamOutputElements = elements.ToArray();
        }

//        private bool ParseStreamOutputElement(string text, SourceSpan span, out StreamOutputElement streamOutputElement)
//        {
//            streamOutputElement = new StreamOutputElement();
//
//            var match = soDeclarationItemRegex.Match(text);
//
//            if (!match.Success)
//            {
//                logger.Error("Invalid StreamOutput declaration [{0}]. Must be of the form [<slot> :] <semantic>[<index>][.<mask>]", span, text);
//                return false;
//            }
//
//            // Parse slot if any
//            var slot = match.Groups[1].Value;
//            int slotIndex = 0;
//            if (!string.IsNullOrEmpty(slot))
//            {
//                int.TryParse(slot, out slotIndex);
//                streamOutputElement.OutputSlot = (byte)slotIndex;
//            }
//
//            // Parse semantic index if any
//            var semanticAndIndex = match.Groups[2].Value;
//            var matchSemanticAndIndex = soSemanticIndex.Match(semanticAndIndex);
//            streamOutputElement.SemanticName = matchSemanticAndIndex.Groups[1].Value;
//            var semanticIndexText = matchSemanticAndIndex.Groups[2].Value;
//            int semanticIndex = 0;
//            if (!string.IsNullOrEmpty(semanticIndexText))
//            {
//                int.TryParse(semanticIndexText, out semanticIndex);
//                streamOutputElement.SemanticIndex = (byte)semanticIndex;
//            }
//
//            // Parse the mask
//            var mask = match.Groups[3].Value;
//            int startComponent = -1;
//            int currentIndex = 0;
//            int countComponent = 1;
//            if (!string.IsNullOrEmpty(mask))
//            {
//                mask = mask.Substring(1);
//                foreach (var maskItem in mask.ToCharArray())
//                {
//                    var nextIndex = xyzwrgbaComponents.IndexOf(maskItem);
//                    if (startComponent < 0)
//                    {
//                        startComponent = nextIndex;
//                    }
//                    else if (nextIndex != (currentIndex + 1))
//                    {
//                        logger.Error("Invalid mask [{0}]. Must be of the form [xyzw] or [rgba] with increasing consecutive component and no duplicate", span, mask);
//                        return false;
//                    }
//                    else
//                    {
//                        countComponent++;
//                    }
//
//                    currentIndex = nextIndex;
//                }
//
//                // If rgba components?
//                if (startComponent > 3)
//                {
//                    startComponent -= 4;
//                }
//            }
//            else
//            {
//                startComponent = 0;
//                countComponent = 4;
//            }
//
//            streamOutputElement.StartComponent = (byte)startComponent;
//            streamOutputElement.ComponentCount = (byte)countComponent;
//
//            return true;
//        }

        private void HandleExport(Ast.Expression expression)
        {
            var values = ExtractStringOrArrayOfStrings(expression);
            if (values != null)
            {
                currentExports.AddRange(values);
            }
        }

        private List<string> ExtractStringOrArrayOfStrings(Ast.Expression expression)
        {
            // TODO implement this method using generics
            object value;
            if (!ExtractValue(expression, out value))
                return null;

            var values = new List<string>();

            if (value is string)
            {
                values.Add((string)value);
            }
            else if (value is object[])
            {
                var arrayValue = (object[])value;
                foreach (var exportItem in arrayValue)
                {
                    if (!(exportItem is string))
                    {
                        logger.Error("Unexpected value [{0}]. Expecting a string.", expression.Span, exportItem);
                        return null;
                    }
                    values.Add((string)exportItem);
                }
            }
            else
            {
                logger.Error("Unexpected value. Expecting a identifier/string or an array of identifier/string.", expression.Span);
            }

            return values;
        }

        private void HandleShareConstantBuffers(Ast.Expression expression)
        {
            object value;
            if (!ExtractValue(expression, out value))
                return;

            if (!(value is bool))
            {
                logger.Error("ShareConstantBuffers must be a bool", expression.Span);
            }
            else
            {
                effect.ShareConstantBuffers = (bool)value;
            }
        }

        private void HandleEffectName(Ast.Expression expression)
        {
            object value;
            if (!ExtractValue(expression, out value))
                return;

            if (!(value is string))
            {
                logger.Error("Effect name must be a string/identifier", expression.Span);
            }
            else
            {
                effect.Name = (string)value;
            }
        }

        private void HandleSubPassCount(Ast.Expression expression)
        {
            object value;
            if (!ExtractValue(expression, out value))
                return;

            if (!(value is int))
            {
                logger.Error("SubPassCount must be an integer", expression.Span);
            }
            else
            {
                nextSubPassCount = (int)value;
            }
        }

        private void HandlePreprocessor(Ast.Expression expression)
        {
            object value;
            if (!ExtractValue(expression, out value))
                return;

            // If null, then preprocessor is reset
            if (value == null)
                preprocessorText = null;

            // Else parse preprocessor directive
            var builder = new StringBuilder();
            if (value is string)
            {
                builder.AppendLine((string)value);
            }
            else if (value is object[])
            {
                var arrayValue = (object[])value;
                foreach (var stringItem in arrayValue)
                {
                    if (!(stringItem is string))
                    {
                        logger.Error("Unexpected type. Preprocessor only support strings in array declaration", expression.Span);
                    }
                    builder.AppendLine((string)stringItem);
                }
            }
            preprocessorText = builder.ToString();
        }

        private void HandleAttribute(Ast.AssignExpression expression)
        {
            // Extract the value and store it in the attribute
            object value;
            if (ExtractValue(expression.Value, out value))
            {
                pass.Properties[expression.Name.Text] = value;
            }
        }

        private void HandleAttribute<T>(Ast.AssignExpression expression)
        {
            // Extract the value and store it in the attribute
            object value;
            if (ExtractValue(expression.Value, out value))
            {
                if (typeof(T) == typeof(uint) && value is int)
                {
                    value = unchecked((uint)(int)value);
                }
                else
                {
                    try
                    {
                        value = Convert.ChangeType(value, typeof(T));
                    }
                    catch (Exception)
                    {
                        logger.Error("Invalid type for attribute [{0}]. Expecting [{1}]", expression.Value.Span, expression.Name.Text, typeof(T).Name);
                    }
                }

                pass.Properties[expression.Name.Text] = value;
            }
        }

        private bool ExtractValue(Ast.Expression expression, out object value)
        {
            value = null;
            if (expression is Ast.LiteralExpression)
            {
                value = ((Ast.LiteralExpression)expression).Value.Value;
            }
            else if (expression is Ast.IdentifierExpression)
            {
                value = ((Ast.IdentifierExpression)expression).Name.Text;
            }
            else if (expression is Ast.ArrayInitializerExpression)
            {
                var arrayExpression = (Ast.ArrayInitializerExpression)expression;
                var arrayValue = new object[arrayExpression.Values.Count];
                for (int i = 0; i < arrayValue.Length; i++)
                {
                    if (!ExtractValue(arrayExpression.Values[i], out arrayValue[i]))
                        return false;
                }
                value = arrayValue;
            }
            else if (expression is Ast.MethodExpression)
            {
                var methodExpression = (Ast.MethodExpression)expression;
                value = ExtractValueFromMethodExpression(methodExpression);
                if (value == null)
                {
                    logger.Error("Unable to convert method expression to value.", expression.Span);
                    return false;
                }
            }
            else
            {
                logger.Error("Unsupported value type. Only [literal, identifier, array]", expression.Span);
                return false;
            }

            return true;
        }

        private object ExtractValueFromMethodExpression(Ast.MethodExpression methodExpression)
        {
            ValueConverter converter;
            if (!ValueConverters.TryGetValue(methodExpression.Name.Text, out converter))
            {
                logger.Error("Unexpected method [{0}]", methodExpression.Span, methodExpression.Name.Text);
                return null;
            }

            // Check for arguments
            if (converter.ArgumentCount != methodExpression.Arguments.Count)
            {
                logger.Error("Unexpected number of arguments (expecting {0} arguments)", methodExpression.Span, converter.ArgumentCount);
                return null;
            }

            var values = new object[methodExpression.Arguments.Count];
            for (int i = 0; i < methodExpression.Arguments.Count; i++)
            {
                object localValue;
                if (!ExtractValue(methodExpression.Arguments[i], out localValue))
                    return null;
                values[i] = converter.ConvertItem(this, localValue);
                if (localValue == null)
                    return null;
            }

            return converter.ConvertFullItem(this, values);
        }
        
        private void HandleLanguage(Ast.Expression expression)
        {
            if (expression is Ast.LiteralExpression)
            {
                var literalValue = (string)((Ast.LiteralExpression)expression).Value.Value;
                try
                {
                    var rawLevel = (int)(Convert.ToSingle(literalValue, CultureInfo.InvariantCulture) * 10);
                }
                catch (Exception)
                {
                    // ignored
                }

                if (string.IsNullOrEmpty(literalValue))
                    logger.Error("Unexpected assignement for [Profile] attribute: expecting only [identifier (fx_4_0, fx_4_1... etc.), or number (9.3, 10.0, 11.0... etc.)]", expression.Span);
            }
        }

        private void HandleProfile(Ast.Expression expression)
        {
            if (expression is Ast.IdentifierExpression identifierExpression)
            {
                profile = (int)(Convert.ToSingle(identifierExpression.Name.Text, CultureInfo.InvariantCulture) * 10);
            }
            else if (expression is Ast.LiteralExpression)
            {
                var literalValue = (string)((Ast.LiteralExpression)expression).Value.Value;
                try
                {
                    profile = (int)(Convert.ToSingle(literalValue, CultureInfo.InvariantCulture) * 10);
                }
                catch (Exception)
                {
                    // ignored
                }

                if (string.IsNullOrEmpty(literalValue))
                    logger.Error("Unexpected assignement for [Profile] attribute: expecting only [identifier (fx_4_0, fx_4_1... etc.), or number (9.3, 10.0, 11.0... etc.)]", expression.Span);
            }
        }

        private string ExtractShaderName(EffectShaderType effectShaderType, Ast.Expression expression)
        {
            string shaderName = null;

            if (expression is Ast.IdentifierExpression)
            {
                shaderName = ((Ast.IdentifierExpression)expression).Name.Text;
            }
            else if (expression is Ast.LiteralExpression)
            {
                var value = ((Ast.LiteralExpression)expression).Value.Value;
                if (Equals(value, 0) || Equals(value, null))
                {
                    return null;
                }
            }
            else if (expression is Ast.CompileExpression)
            {
                var compileExpression = (Ast.CompileExpression)expression;

                var profileName = compileExpression.Profile.Text;

                if (compileExpression.Method is Ast.MethodExpression)
                {
                    var shaderMethod = (Ast.MethodExpression)compileExpression.Method;

                    if (shaderMethod.Arguments.Count > 0)
                    {
                        logger.Error("Default arguments for shader methods are not supported", shaderMethod.Span);
                        return null;
                    }

                    shaderName = shaderMethod.Name.Text;
                }
                else
                {
                    logger.Error("Unsupported expression for compile. Excepting method", expression.Span);
                    return null;
                }
            }
            else if (expression is Ast.MethodExpression)
            {
                // CompileShader( vs_4_0, VS() )
                var compileExpression = (Ast.MethodExpression)expression;

                if (compileExpression.Name.Text == "CompileShader")
                {
                    if (compileExpression.Arguments.Count != 2)
                    {
                        logger.Error("Unexpected number of arguments [{0}] instead of 2", expression.Span, compileExpression.Arguments.Count);
                        return null;
                    }

                    // Extract level (11.0 10.0) from profile name (
                    if (!ExtractValue(compileExpression.Arguments[0], out var profileName))
                        return null;

                    if (!(profileName is string))
                    {
                        logger.Error("Invalid profile [{0}]. Expecting identifier", compileExpression.Arguments[0].Span, profileName);
                        return null;
                    }

                    var shaderMethod = compileExpression.Arguments[1] as Ast.MethodExpression;
                    if (shaderMethod == null)
                    {
                        logger.Error("Unexpected expression. Only method expression are supported", compileExpression.Arguments[1].Span);
                        return null;
                    }

                    if (shaderMethod.Arguments.Count > 0)
                    {
                        logger.Error("Default arguments for shader methods are not supported", shaderMethod.Span);
                        return null;
                    }
                    
                    

                    shaderName = shaderMethod.Name.Text;
                }
            }

            if (shaderName == null)
            {
                logger.Error("Unexpected compile expression", expression.Span);
            }

            return shaderName;
        }

        private void CompileShader(EffectShaderType type, Ast.Expression assignValue)
        {
            CompileShader(type, ExtractShaderName(type, assignValue), assignValue.Span);
        }

        private void CompileShader(EffectShaderType type, string entryPoint, SourceSpan span)
        {
            if (entryPoint == null)
            {
                pass.Pipeline[type] = EffectData.ShaderLink.NullShader;
                return;
            }

            // If the level is not setup, return an error
            if (this.profile == 0)
            {
                logger.Error("Expecting setup of [Profile = fx_4_0/fx_5_0...etc.] before compiling a shader.", span);
                return;
            }

            //var profile = GetShaderProfile(type);

            try
            {
                var result = CompileParsedShader(type, entryPoint, profile);

                var compilerMessages = result.Messages;
                if (result.HasErrors || result.Bytecode == null)
                {
                    logger.LogMessage(new LogMessageRaw(LogMessageType.Error, compilerMessages));
                }
                else
                {
                    if (!string.IsNullOrEmpty(compilerMessages))
                        logger.LogMessage(new LogMessageRaw(LogMessageType.Warning, compilerMessages));

                    // Check if this shader is exported
                    if (currentExports.Contains(entryPoint))
                    {
                        // the exported name is EffectName::ShaderName
                        entryPoint = effect.Name + "::" + entryPoint;
                    }
                    else
                    {
                        // If the shader is not exported, set the name to null
                        entryPoint = null;
                    }

                    var shader = CreateEffectShader(type, entryPoint, result);

                    if (logger.HasErrors)
                        return;

                    ProcessShaderData(type, result, shader);
                }
            }
            finally
            {
            }
        }

        private CompilationResult CompileParsedShader(EffectShaderType shaderKind, string entryPoint, int profile)
        {
            var sourcecodeBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(preprocessorText))
                sourcecodeBuilder.Append(preprocessorText);
            sourcecodeBuilder.Append(parserResult.PreprocessedSource);

            var sourcecode = sourcecodeBuilder.ToString();

            var filePath = replaceBackSlash.Replace(parserResult.SourceFileName, @"\");

            var opts = CompileOptions.New();
            opts.EnableHlslFunctionality = true;
            opts.UseHlslIoMapping = true;
            opts.UseHlslOffsets = true;
            opts.SetAutoBindUniforms = true;
            opts.SourceLanguage = ShadercSourceLanguage.Hlsl;
            opts.SetForcedVersionProfile(profile, ShadercProfile.Core);
            //opts.SetIncludeCallbacks(,);

            var result = SpirvReflection.CompileToSpirvBinary(
                sourcecode,
                GetShadercType(shaderKind),
                Path.GetFileName(filePath),
                entryPoint,
                opts);
            return result;
        }

        private ShadercShaderKind GetShadercType(EffectShaderType type)
        {
            switch(type)
            {
                case EffectShaderType.Vertex: return ShadercShaderKind.VertexShader;
                case EffectShaderType.Hull: return ShadercShaderKind.TessControlShader;
                case EffectShaderType.Domain: return ShadercShaderKind.TessEvaluationShader;
                case EffectShaderType.Geometry: return ShadercShaderKind.GeometryShader;
                case EffectShaderType.Fragment: return ShadercShaderKind.FragmentShader;
                case EffectShaderType.Compute: return ShadercShaderKind.ComputeShader;
                default:
                    throw new ArgumentOutOfRangeException($"Shader type {type} could not be converted to any ShadercShaderKind type");
            }
        }

        private EffectData.Shader CreateEffectShader(EffectShaderType type, string shaderName, CompilationResult bytecode)
        {
            var shader = new EffectData.Shader()
            {
                Name = shaderName,
                //Level = level,
                Bytecode = bytecode,
                Type = type,
                ConstantBuffers = new List<EffectData.ConstantBuffer>(),
                ResourceParameters = new List<EffectData.ResourceParameter>(),
                InputSignature = new EffectData.Signature(),
                OutputSignature = new EffectData.Signature()
            };

            using (var reflect = new SpirvReflection(shader.Bytecode))
            {
                var result = reflect.Disassemble();
                //BuiltSemanticInputAndOutput(shader, reflect);
                BuildParameters(shader, result);
            }

            return shader;
        }

        private void ProcessShaderData(EffectShaderType type, CompilationResult bytecode, EffectData.Shader shader)
        {
            // Strip reflection data, as we are storing it in the toolkit format.
            //var byteCodeNoDebugReflection = bytecode.Strip(StripFlags.CompilerStripReflectionData | StripFlags.CompilerStripDebugInformation);

            // Compute Hashcode
            //shader.Hashcode = Utilities.ComputeHashFNVModified(byteCodeNoDebugReflection);

            // if No debug is required, take the bytecode without any debug/reflection info.
            if ((compilerFlags & EffectCompilerFlags.Debug) == 0)
                shader.Bytecode = bytecode;

            // Check that this shader was not already generated
            int shaderIndex;
            for (shaderIndex = 0; shaderIndex < effectData.Shaders.Count; shaderIndex++)
            {
                var shaderItem = effectData.Shaders[shaderIndex];
                if (shaderItem.IsSimilar(shader))
                    break;
            }

            // Check if there is a new shader to store in the archive
            if (shaderIndex >= effectData.Shaders.Count)
            {
                shaderIndex = effectData.Shaders.Count;

                // If this is a Vertex shader, compute the binary signature for the input layout
                if (shader.Type == EffectShaderType.Vertex)
                {
                    // Gets the signature from the stripped bytecode and compute the hashcode.
//                    shader.InputSignature.Bytecode = ShaderSignature.GetInputSignature(byteCodeNoDebugReflection);
//                    shader.InputSignature.Hashcode = Utilities.ComputeHashFNVModified(shader.InputSignature.Bytecode);
                }

                effectData.Shaders.Add(shader);
            }

            if (pass.Pipeline[type] == null)
                pass.Pipeline[type] = new EffectData.ShaderLink();

            pass.Pipeline[type].Index = shaderIndex;
        }

        private void HandleMethodExpression(Ast.MethodExpression expression)
        {
            if (expression.Arguments.Count == 1)
            {
                var argument = expression.Arguments[0];

                switch (expression.Name.Text)
                {
                    case "SetVertexShader":
                        CompileShader(EffectShaderType.Vertex, argument);
                        break;
                    case "SetPixelShader":
                        CompileShader(EffectShaderType.Fragment, argument);
                        break;
                    case "SetGeometryShader":
                        CompileShader(EffectShaderType.Geometry, argument);
                        break;
                    case "SetDomainShader":
                        CompileShader(EffectShaderType.Domain, argument);
                        break;
                    case "SetHullShader":
                        CompileShader(EffectShaderType.Hull, argument);
                        break;
                    case "SetComputeShader":
                        CompileShader(EffectShaderType.Compute, argument);
                        break;
                    default:
                        logger.Warning("Unhandled method [{0}]", expression.Span, expression.Name);
                        break;
                }
            }
            else
            {
                logger.Warning("Unhandled method [{0}]", expression.Span, expression.Name);
            }
        }

        private static string StageTypeToString(EffectShaderType type)
        {
            string profile = null;
            switch (type)
            {
                case EffectShaderType.Vertex:
                    profile = "vs";
                    break;
                case EffectShaderType.Domain:
                    profile = "ds";
                    break;
                case EffectShaderType.Hull:
                    profile = "hs";
                    break;
                case EffectShaderType.Geometry:
                    profile = "gs";
                    break;
                case EffectShaderType.Fragment:
                    profile = "ps";
                    break;
                case EffectShaderType.Compute:
                    profile = "cs";
                    break;
            }
            return profile;
        }

        private static EffectShaderType VersionToShaderType(ShadercShaderKind stageText)
        {
            switch (stageText)
            {
                case ShadercShaderKind.VertexShader: return EffectShaderType.Vertex;
                case ShadercShaderKind.TessEvaluationShader: return EffectShaderType.Domain;
                case ShadercShaderKind.TessControlShader: return EffectShaderType.Hull;
                case ShadercShaderKind.GeometryShader: return EffectShaderType.Geometry;
                case ShadercShaderKind.FragmentShader: return EffectShaderType.Fragment;
                case ShadercShaderKind.ComputeShader: return EffectShaderType.Compute;
            }

            throw new ArgumentException("Unknown shader stage type: " + stageText);
        }

//        private void BuiltSemanticInputAndOutput(EffectData.Shader shader, SpirvReflectionResult reflect)
//        {
//            var description = reflect.Description;
//            shader.InputSignature.Semantics = new EffectData.Semantic[description.InputParameters];
//            for (int i = 0; i < description.InputParameters; i++)
//                shader.InputSignature.Semantics[i] = ConvertToSemantic(reflect.GetInputParameterDescription(i));
//
//            shader.OutputSignature.Semantics = new EffectData.Semantic[description.OutputParameters];
//            for (int i = 0; i < description.OutputParameters; i++)
//                shader.OutputSignature.Semantics[i] = ConvertToSemantic(reflect.GetOutputParameterDescription(i));
//        }

//        private EffectData.Semantic ConvertToSemantic(ShaderReflectionVariable shaderParameterDescription)
//        {
//            return new EffectData.Semantic(
//                shaderParameterDescription.SemanticName,
//                (byte)shaderParameterDescription.SemanticIndex,
//                (byte)shaderParameterDescription.Register,
//                (byte)shaderParameterDescription.SystemValueType,
//                (byte)shaderParameterDescription.ComponentType,
//                (byte)shaderParameterDescription.UsageMask,
//                (byte)shaderParameterDescription.ReadWriteMask,
//                (byte)shaderParameterDescription.Stream
//                );
//        }

        /// <summary>
        ///   Builds the parameters for a particular shader.
        /// </summary>
        /// <param name="shader"> The shader to build parameters. </param>
        /// <param name="reflectionResult"> A shader-reflection interface accesses shader information.</param>
        private void BuildParameters(EffectData.Shader shader, SpirvReflectionResult reflectionResult)
        {
            //var description = reflect.Description;


            // Iterate on all Constant buffers used by this shader
            // Build all ParameterBuffers
            for (int i = 0; i < reflectionResult.UniformBuffers.Count; i++)
            {
                var reflectConstantBuffer = reflectionResult.UniformBuffers[i];
                var description = reflectConstantBuffer.Description;

                // Create the buffer
                var parameterBuffer = new EffectData.ConstantBuffer()
                {
                    Name = description.Name,
                    Size = (int)description.Size,
                    Parameters = new List<EffectData.ValueTypeParameter>(),
                };
                shader.ConstantBuffers.Add(parameterBuffer);

                // Iterate on each variable declared inside this buffer
                for (uint j = 0; j < reflectConstantBuffer.VariablesCount; j++)
                {
                    var variableBuffer = reflectConstantBuffer.GetVariable(j);

                    // Build constant buffer parameter
                    var parameter = BuildConstantBufferParameter(variableBuffer);

                    // Add this parameter to the ConstantBuffer
                    parameterBuffer.Parameters.Add(parameter);
                }
            }

            var resourceParameters = new Dictionary<string, EffectData.ResourceParameter>();
            var indicesUsedByName = new Dictionary<string, List<IndexedInputBindingDescription>>();

            // Iterate on all resources bound in order to resolve resource dependencies for this shader.
            // If the shader is dependent from an object variable, then create this variable as well.
            foreach (var resource in reflectionResult.AllResources)
            {
                var bindingDescription = resource.Description;
                string name = bindingDescription.Name;

                // Handle special case for indexable variable in SM5.0 that is different from SM4.0
                // In SM4.0, reflection on a texture array declared as "Texture2D textureArray[4];" will
                // result into a single "textureArray" InputBindingDescription
                // While in SM5.0, we will have several textureArray[1], textureArray[2]...etc, and 
                // indices depending on the usage. Fortunately, it seems that in SM5.0, there is no hole
                // so we can rebuilt a SM4.0 like description for SM5.0.
                // This is the purpose of this code.
                var matchResult = MatchVariableArray.Match(name);
                if (matchResult.Success)
                {
                    name = matchResult.Groups[1].Value;
                    int arrayIndex = int.Parse(matchResult.Groups[2].Value);

//                    if (bindingDescription.BindCount != 1)
//                    {
//                        logger.Error("Unexpected BindCount ({0}) instead of 1 for indexable variable '{1}'", new SourceSpan(), bindingDescription.BindCount, name);
//                        return;
//                    }

                    List<IndexedInputBindingDescription> indices;
                    if (!indicesUsedByName.TryGetValue(name, out indices))
                    {
                        indices = new List<IndexedInputBindingDescription>();
                        indicesUsedByName.Add(name, indices);
                    }

                    indices.Add(new IndexedInputBindingDescription(arrayIndex, bindingDescription));
                }

                // In the case of SM5.0 and texture array, there can be several input binding descriptions, so we ignore them
                // here, as we are going to recover them outside this loop.
                if (!resourceParameters.ContainsKey(name))
                {
                    // Build resource parameter
                    var parameter = BuildResourceParameter(name, bindingDescription);
                    shader.ResourceParameters.Add(parameter);
                    resourceParameters.Add(name, parameter);
                }
            }

            // Do we have any SM5.0 Indexable array to fix?
            if (indicesUsedByName.Count > 0)
            {
                foreach (var resourceParameter in resourceParameters)
                {
                    var name = resourceParameter.Key;
                    List<IndexedInputBindingDescription> indexedBindings;
                    if (indicesUsedByName.TryGetValue(name, out indexedBindings))
                    {
                        // Just make sure to sort the list in index ascending order
                        indexedBindings.Sort((left, right) => left.Index.CompareTo(right.Index));
                        int minIndex = -1;
                        int maxIndex = -1;
                        int previousIndex = -1;

                        int minBindingIndex = -1;
                        int previousBindingIndex = -1;


                        // Check that indices have only a delta of 1
                        foreach (var indexedBinding in indexedBindings)
                        {
                            if (minIndex < 0)
                            {
                                minIndex = indexedBinding.Index;
                            }

                            if (minBindingIndex < 0)
                            {
                                minBindingIndex = (int)indexedBinding.Description.SlotIndex;
                            }

                            if (indexedBinding.Index > maxIndex)
                            {
                                maxIndex = indexedBinding.Index;
                            }

                            if (previousIndex >= 0)
                            {
                                if ((indexedBinding.Index - previousIndex) != 1)
                                {
                                    logger.Error("Unexpected sparse index for indexable variable '{0}'", new SourceSpan(), name);
                                    return;
                                }

                                if ((indexedBinding.Description.SlotIndex - previousBindingIndex) != 1)
                                {
                                    logger.Error("Unexpected sparse index for indexable variable '{0}'", new SourceSpan(), name);
                                    return;
                                }
                            }

                            previousIndex = indexedBinding.Index;
                            previousBindingIndex = (int)indexedBinding.Description.SlotIndex;
                        }

                        // Fix the slot and count
                        resourceParameter.Value.Slot = (byte)(minBindingIndex - minIndex);
                        resourceParameter.Value.Count = (byte)(maxIndex + 1);
                    }
                }
            }
        }

        /// <summary>
        ///   Builds an effect parameter from a reflection variable.
        /// </summary>
        /// <returns> an EffectParameter, null if not handled </returns>
        private EffectData.ValueTypeParameter BuildConstantBufferParameter(ShaderReflectionVariable variable)
        {
            var parameter = new EffectData.ValueTypeParameter()
            {
                Name = variable.Name,
                Offset = variable.Offset,
                Size = (int)variable.Size,
                Count = variable.ElementCount,
                Class = (EffectParameterClass)variable.Class,
                Type = (EffectParameterType)variable.Type,
                RowCount = (byte)variable.RowCount,
            };

            return parameter;
        }

        /// <summary>
        ///   Builds an effect parameter from a reflection variable.
        /// </summary>
        /// <returns> an EffectParameter, null if not handled </returns>
        private static EffectData.ResourceParameter BuildResourceParameter(string name, ShaderReflectionVariable variableBinding)
        {
            var parameter = new EffectData.ResourceParameter()
            {
                Name = name,
                Class = EffectParameterClass.Object,
                Slot = (byte)variableBinding.SlotIndex,
                Count = (byte)variableBinding.ElementCount,
            };

            switch (variableBinding.Class)
            {
                case SpvcResourceType.UniformBuffer:
                    parameter.Type = EffectParameterType.ConstantBuffer;
                    break;
                case SpvcResourceType.SeparateImage:
                    switch (variableBinding.Dimension)
                    {
                        case ShaderResourceDimension.Buffer:
                            parameter.Type = EffectParameterType.Buffer;
                            break;
                        case ShaderResourceDimension.Texture1D:
                            parameter.Type = EffectParameterType.Texture1D;
                            break;
                        case ShaderResourceDimension.Texture2D:
                            parameter.Type = EffectParameterType.Texture2D;
                            break;
                        case ShaderResourceDimension.Texture3D:
                            parameter.Type = EffectParameterType.Texture3D;
                            break;
                        case ShaderResourceDimension.TextureCube:
                            parameter.Type = EffectParameterType.TextureCube;
                            break;
                    }
                    break;
                case SpvcResourceType.StorageBuffer:
                    parameter.Type = EffectParameterType.StorageBuffer;
                    break;
                case SpvcResourceType.StorageImage:
                    switch (variableBinding.Dimension)
                    {
                        case ShaderResourceDimension.Buffer:
                            parameter.Type = EffectParameterType.StorageImage;
                            break;
                        case ShaderResourceDimension.Texture1D:
                            parameter.Type = EffectParameterType.RWTexture1D;
                            break;
                        case ShaderResourceDimension.Texture2D:
                            parameter.Type = EffectParameterType.RWTexture2D;
                            break;
                        case ShaderResourceDimension.Texture3D:
                            parameter.Type = EffectParameterType.RWTexture3D;
                            break;
                    }
                    break;
                case SpvcResourceType.SeparateSamplers:
                    parameter.Type = EffectParameterType.Sampler;
                    break;
            }
            return parameter;
        }

        private static object ToFloat(EffectCompilerInternal compiler, object value)
        {
            try
            {
                return Convert.ToSingle(value);
            }
            catch (Exception)
            {
                compiler.logger.Error("Unable to convert [{0}] to float", new SourceSpan(), value);
            }
            return null;
        }

        #region Nested type: IndexedInputBindingDescription

        private class IndexedInputBindingDescription
        {
            public IndexedInputBindingDescription(int index, ShaderReflectionVariable description)
            {
                Index = index;
                Description = description;
            }

            public int Index;

            public ShaderReflectionVariable Description;
        }

        #endregion

        #region Nested type: ConvertFullItem

        private delegate object ConvertFullItem(EffectCompilerInternal compiler, object[] parameters);

        #endregion

        #region Nested type: ConvertItem

        private delegate object ConvertItem(EffectCompilerInternal compiler, object value);

        #endregion

        #region Nested type: ValueConverter

        private struct ValueConverter
        {
            public int ArgumentCount;
            public ConvertFullItem ConvertFullItem;
            public ConvertItem ConvertItem;

            public ValueConverter(string name, int argumentCount, ConvertItem convertItem, ConvertFullItem convertFullItem)
            {
                ArgumentCount = argumentCount;
                ConvertItem = convertItem;
                ConvertFullItem = convertFullItem;
            }
        }

        #endregion
    }
}
