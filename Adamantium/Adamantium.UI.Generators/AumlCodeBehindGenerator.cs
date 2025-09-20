using System.Collections.Generic;
using Adamantium.UI.Markup;
using Microsoft.CodeAnalysis;
using System.Linq;
using Adamantium.UI.Markup.AST;
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
           var filesProvider = context.AdditionalTextsProvider
                .Where(file => file.Path.EndsWith(".xml"))
                .Select((text, cancellationToken) => (
                    Path: text.Path,
                    Content: text.GetText(cancellationToken)!.ToString()))
                .Collect();

            var compilationProvider = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider);
            var sourceProvider = filesProvider.Combine(compilationProvider);

            context.RegisterSourceOutput(sourceProvider, (spc, source) =>
            {
                var (collectedFiles, (compilation, configOptions)) = source;

                var resourceDictionaries = new List<ResourceDictionaryInfo>();

                configOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                configOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);

                if (string.IsNullOrEmpty(rootNamespace))
                {
                    spc.ReportDiagnostic(Diagnostic.Create("AUI001", "Build", "No RootNamespace Compiler option provided. Please add <CompilerVisibleProperty Include=\"RootNamespace\" /> to your csproj file.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0));
                    return;
                }

                var typeResolver = new RoslynTypeResolver(compilation);
                var transformer = new DefaultAumlTransformer();
                var codeGenerator = new AumlSourceGenerator();
                
                var metadata = new List<AumlDocument>();

                // Phase 1 - parsing and metadata collection
                foreach (var file in collectedFiles)
                {
                    var diagnostics = new RoslynDiagnosticSink(spc);

                    if (file.Path.EndsWith("FluentDark.xml"))
                    {
                        int x = 0;
                    }

                    var aumlDoc = AumlParser.Parse(file.Content);
                    if (aumlDoc.HasErrors)
                    {
                        foreach (var message in aumlDoc.Logger.Messages)
                        {
                            diagnostics.ReportLogMessage(file.Path, message);
                        }
                        continue;
                    }

                    // Get a relative file path for further calculations
                    var relativePath = file.Path.Replace(projectDir, string.Empty).Replace("\\", "/");
                    if (relativePath.StartsWith("/"))
                    {
                        relativePath = relativePath.Substring(1);
                    }
                    
                    aumlDoc.RelativeFilePath = relativePath;
                    aumlDoc.RootNamespace = rootNamespace;
                    
                    metadata.Add(aumlDoc);
                }

                // Phase 2 - metadata transform and code generation based on sorted metadata
                var sortedMetadata = metadata.OrderBy(meta => GetGenerationPriority(meta.Root));

                foreach (var aumlDoc in sortedMetadata)
                {
                    if (aumlDoc.Root.TypeReference.Name == "Window")
                    {
                        int x = 0;
                    }
                    
                    var diagnostics = new RoslynDiagnosticSink(spc);
                    
                    var aumlMetadataContainer = transformer.Transform(aumlDoc, typeResolver, diagnostics);
                    if (diagnostics.HasErrors)
                    {
                        continue;
                    }
                    
                    codeGenerator.GenerateSourceCode(aumlMetadataContainer, new RoslynOutputSink(spc), diagnostics);

                    if (aumlMetadataContainer.RootEntityType == EntityType.ResourceDictionary)
                    {
                        var info = new ResourceDictionaryInfo(
                            $"/{aumlDoc.RelativeFilePath}",
                            $"{aumlMetadataContainer.FullClassName}");
                        resourceDictionaries.Add(info);
                    }
                }

                if (resourceDictionaries.Any())
                {
                    codeGenerator.GenerateResourceMap(new RoslynOutputSink(spc), resourceDictionaries);
                    codeGenerator.GenerateAssemblyAttribute(new RoslynOutputSink(spc));
                }
            });
        }
        
        private int GetGenerationPriority(AumlAstObjectNode rootNode)
        {
            return rootNode.TypeReference.Name switch
            {
                "ResourceDictionary" => 0,
                "StyleSet" => 1,
                "Theme" => 2,
                _ => 99
            };
        }
    }
}