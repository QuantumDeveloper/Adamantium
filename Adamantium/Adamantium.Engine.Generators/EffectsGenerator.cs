using System.Collections.Generic;
using System.IO;
using System.Text;
using Adamantium.Core;
using Adamantium.Engine.Effects;
using Microsoft.CodeAnalysis;

namespace Adamantium.Engine.Generators;

[Generator]
public class EffectsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var effectFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".fx"));
        
        var namesAndContents = effectFiles.Select((text, cancellationToken) => (
            name: Path.GetFileNameWithoutExtension(text.Path),
            content: text.GetText(cancellationToken)!.ToString()));

        var sourceProvider = namesAndContents.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
        {
            var (file, compilation) = provider;
            var text = file.content;
            var compilerResult = EffectCompiler.Compile(text, file.name);
            var result = GenerateEffect(compilerResult, compilation, file.name);
            if (compilerResult.Logger.Messages.Count > 0)
            {
                CreateDiagnostic(ref spc, compilerResult, file.name);
            }
            if (!compilerResult.HasErrors)
            {
                spc.AddSource($"{file.name}.g.cs", result);
            }

        });
    }

    private void CreateDiagnostic(ref SourceProductionContext spc, EffectCompilerResult result, string filePath)
    {
        foreach(var message in result.Logger.Messages)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "EffectGenerator", 
                title: "Effects parsing error",
                messageFormat: "Error while parsing {0}: {1}", 
                category: "EffectParser",
                (DiagnosticSeverity)message.Type, 
                isEnabledByDefault: true);
            var diagnostic = Diagnostic.Create(descriptor, Location.None, $"{filePath}.fx", message.Text);
            spc.ReportDiagnostic(diagnostic);
        }
    }

    private string GenerateEffect(EffectCompilerResult result, Compilation compilation, string fileName)
    {
        var textGenerator = new TextGenerator();
        textGenerator.WriteLine("using Adamantium.Engine.Effects;");
        textGenerator.WriteLine("using Adamantium.Engine.Graphics;");
        textGenerator.WriteLine("using Adamantium.Engine.Graphics.Effects;");

        textGenerator.NewLine();

        textGenerator.WriteLine($"namespace Adamantium.Generated.Effects;");
        textGenerator.WriteLine($"public partial class {Path.GetFileName(fileName)} : Effect");
        textGenerator.WriteOpenBraceAndIndent();

        var bytecodeStream = new MemoryStream();
        result.EffectData.Save(bytecodeStream);
        string bytes = GetBytecodeAsText(bytecodeStream.GetBuffer());
        bytecodeStream.Position = 0;
        textGenerator.WriteLine(@$"private static readonly EffectData bytecode = EffectData.Load(new byte[] {{
        {bytes}
        }});");
        
        textGenerator.NewLine();

        textGenerator.WriteLine($"public {fileName}(GraphicsDevice device, EffectPool effectPool = null) " +
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
                if (parametersIdentity.Contains(resource.Name) || resource.Name == "$Global") continue;

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
            textGenerator.WriteLine($"public EffectPass {pass} {{get;}}");
        }

        textGenerator.NewLine();

        foreach (var parameter in effectParameters)
        {
            textGenerator.WriteLine($"public EffectParameter {parameter} {{get;}}");
        }

        textGenerator.UnindentAndWriteCloseBrace();

        return textGenerator.ToString();
    }
    

    private string GetBytecodeAsText(byte[] bytecode)
    {
        var bufferAsText = new StringBuilder();
        for (int i = 0; i < bytecode.Length; i++)
        {
            bufferAsText.Append(bytecode[i]).Append(", ");
            if (i > 0 && (i % 28) == 0)
            {
                bufferAsText.AppendLine();
            }
        }

        return bufferAsText.ToString();
    }
}