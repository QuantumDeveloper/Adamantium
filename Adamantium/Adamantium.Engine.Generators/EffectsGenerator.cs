using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Core;
using Adamantium.EffectsCompiler;
using Microsoft.CodeAnalysis;

namespace Adamantium.Engine.Generators;

[Generator]
public class EffectsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        NativeLibraryLoader.LoadNativeLibraries();
        
        var effectFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".fx"));
        var includeFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".fxh"));

        var fxNamesAndContents = effectFiles.Select((text, cancellationToken) => (
            name: Path.GetFileName(text.Path),
            path: text.Path,
            content: text.GetText(cancellationToken)!.ToString(),
            fxName: Path.GetFileNameWithoutExtension(text.Path)));

        var includesAndContents = includeFiles.Select((text, cancellationToken) => new ShaderFileInfo()
        {
            FileName = Path.GetFileName(text.Path),
            Path = text.Path,
            Content = text.GetText(cancellationToken)!.ToString()
        });

        var includesProvider = includesAndContents.Collect();

        var sourceProvider = fxNamesAndContents
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(includesProvider);

        context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
        {
            var (((file, compilation), configOptions), includes) = provider;

            configOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var @namespace);
            if (string.IsNullOrEmpty(@namespace))
            {
                CreateDiagnostic(ref spc,
                    file.name,
                    "No RootNamespace Compiler option provided in project file. Please, add <CompilerVisibleProperty Include=\"RootNamespace\" /> to your csproj file",
                    DiagnosticSeverity.Error);
            }
            
            try
            {
                var text = file.content;
                var compilerResult = EffectCompiler.Compile(text, file.name, includes);
                if (compilerResult.HasErrors)
                {
                    CreateDiagnostic(ref spc, compilerResult, file.name);
                }
                else
                {
                    var result = GenerateEffect(compilerResult, compilation, file.fxName, @namespace);
                    spc.AddSource($"{file.fxName}.g.cs", result);
                }
            }
            catch(System.Exception ex)
            {
                CreateDiagnostic(ref spc, file.name, ex.Message, DiagnosticSeverity.Error);
            }
        });
    }

    private string GenerateEffect(EffectCompilerResult result, Compilation compilation, string fxName, string @namespace)
    {
        var textGenerator = new TextGenerator();
        textGenerator.WriteLine("using Adamantium.EffectsCompiler;");
        textGenerator.WriteLine("using Adamantium.Graphics.Core;");
        textGenerator.WriteLine("using Adamantium.Graphics.Core.EffectsFramework;");

        textGenerator.NewLine();

        textGenerator.WriteLine($"namespace {@namespace}.Effects.Generated");
        textGenerator.WriteOpenBraceAndIndent();
        textGenerator.WriteLine($"public partial class {fxName} : Effect");
        textGenerator.WriteOpenBraceAndIndent();

        var bytecodeStream = new MemoryStream();
        result.EffectData.Save(bytecodeStream);
        // The EffectData is already MessagePack+LZ4-compressed. Emit it as a Base64 string literal, NOT a decimal byte[]
        // literal: Base64 is ~1.33 source chars/byte vs ~4-5 for each "123, ", so the generated file shrinks ~3-4x (and
        // ToArray() gives exactly the written Length - GetBuffer() returned the whole zero-padded capacity).
        string base64 = System.Convert.ToBase64String(bytecodeStream.ToArray());
        textGenerator.WriteLine(@$"private static readonly EffectData bytecode = EffectData.Load(System.Convert.FromBase64String(""{base64}""));");

        textGenerator.NewLine();

        textGenerator.WriteLine($"public {fxName}(IGraphicsDevice device, EffectPool effectPool = null) " +
                                $": base(device, bytecode, effectPool)");
        textGenerator.WriteOpenBraceAndIndent();

        var effectParameters = new List<string>();
        var effectPasses = new List<string>();
        foreach (var technique in result.EffectData.Description.Techniques)
        {
            foreach (var pass in technique.Passes)
            {
                var passName = $"{technique.Name}{pass.Name}Pass";
                passName = char.ToUpper(passName[0]) + passName.Substring(1);
                effectPasses.Add(passName);
                textGenerator.WriteLine($"{passName} = Techniques[\"{technique.Name}\"].Passes[\"{pass.Name}\"];");
            }
        }

        var parametersIdentity = new HashSet<string>();
        foreach (var shader in result.EffectData.Shaders)
        {
            foreach (var constantBuffer in shader.ConstantBuffers)
            {
                foreach (var parameter in constantBuffer.Parameters)
                {
                    if (parametersIdentity.Contains(parameter.Name)) continue;

                    parametersIdentity.Add(parameter.Name);
                    var name = char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
                    effectParameters.Add(name);
                    textGenerator.WriteLine($"{name} = Parameters[\"{parameter.Name}\"];");
                }
            }

            foreach (var resource in shader.ResourceParameters)
            {
                if (parametersIdentity.Contains(resource.Name) || resource.Name == "type.$Globals") continue;

                parametersIdentity.Add(resource.Name);
                var name = char.ToUpper(resource.Name[0]) + resource.Name.Substring(1);
                effectParameters.Add(name);
                textGenerator.WriteLine($"{name} = Parameters[\"{resource.Name}\"];");
            }
        }

        textGenerator.UnindentAndWriteCloseBrace();


        textGenerator.NewLine();

        foreach (var pass in effectPasses)
        {
            textGenerator.WriteLine($"public IEffectPass {pass} {{get;}}");
        }

        textGenerator.NewLine();

        foreach (var parameter in effectParameters)
        {
            textGenerator.WriteLine($"public EffectParameter {parameter} {{get;}}");
        }

        textGenerator.UnindentAndWriteCloseBrace(); // close class
        textGenerator.UnindentAndWriteCloseBrace(); // close namespace

        return textGenerator.ToString();
    }

    private void CreateDiagnostic(ref SourceProductionContext spc, EffectCompilerResult result, string filePath)
    {
        foreach (var message in result.Logger.Messages)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "EffectGenerator",
                title: "Effects parsing error",
                messageFormat: "Error while parsing {0}: {1}",
                category: "EffectParser",
                (DiagnosticSeverity)message.Type,
                isEnabledByDefault: true);
            var diagnostic = Diagnostic.Create(descriptor, Location.None, filePath, message.ToString());
            spc.ReportDiagnostic(diagnostic);
        }
    }

    private void CreateDiagnostic(ref SourceProductionContext spc, string filePath, string diagnosticText, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(
            id: "EffectGenerator",
            title: "Effects parsing error",
            messageFormat: "{0}: {1}",
            category: "EffectParser",
            severity,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, Location.None, filePath, diagnosticText);
        spc.ReportDiagnostic(diagnostic);

    }
}