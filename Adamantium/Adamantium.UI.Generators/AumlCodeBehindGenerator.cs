using Adamantium.UI.Markup;
using Microsoft.CodeAnalysis;
using System.IO;
using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Roslyn;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Generators
{
    [Generator]
    public class AumlCodeBehindGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var aumlFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".xml"));

            // read their contents and save their name
            var namesAndContents = aumlFiles.Select((text, cancellationToken) => (
                path: text.Path,
                name: Path.GetFileNameWithoutExtension(text.Path),
                content: text.GetText(cancellationToken)!.ToString()));

            var sourceProvider = namesAndContents.Combine(context.CompilationProvider).Combine(context.AnalyzerConfigOptionsProvider);
            
            context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
            {
                var ((file, compilation), configOptions) = provider;
                configOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var @namespace);
                var diagnostics = new RoslynDiagnosticSink(spc);
                if (string.IsNullOrEmpty(@namespace))
                {
                    diagnostics.ReportError(file.name,
                        "No RootNamespace Compiler option provided in project file. Please, add <CompilerVisibleProperty Include=\"RootNamespace\" /> to your csproj file");

                }
                configOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
                var text = file.content;
                var aumlDoc = AumlParser.Parse(text);
                if (aumlDoc.HasErrors)
                {
                    foreach (var message in aumlDoc.Logger.Messages)
                    {
                        diagnostics.ReportLogMessage(file.name, message);
                    }
                    
                    return;
                }
                // Get relative file path for further calculations
                aumlDoc.RelativeFilePath = file.path.Replace(projectDir, string.Empty);
                aumlDoc.RootNamespace = @namespace;
                var transformer = new DefaultAumlTransformer();
                var typeResolver = new RoslynTypeResolver(compilation);
                var aumlMetadataContainer = transformer.Transform(aumlDoc, typeResolver, diagnostics);
                if (!diagnostics.HasErrors)
                {
                    var codeGenerator = new AumlSourceGenerator();
                    codeGenerator.GenerateSourceCode(aumlMetadataContainer, new RoslynOutputSink(spc), diagnostics);
                }
            });
        }
    }
}