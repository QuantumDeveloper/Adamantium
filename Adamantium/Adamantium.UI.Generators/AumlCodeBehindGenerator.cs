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
                .Where(file => file.Path.EndsWith(".xml") || file.Path.EndsWith(".auml"))
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

                    foreach (var finding in QuickAccessMenuCheck.Run(aumlDoc))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create("AUI010", "Ribbon",
                            $"{relativePath}({finding.Line},{finding.Position}): {finding.Command} states its menu as MenuItem CONTROLS. " +
                            "Taken into the quick-access bar it will drop an EMPTY menu - a ContextMenu is a logical child and " +
                            "cannot be in two places, so only the row DATA travels. State the rows as ItemsSource + ItemTemplate, " +
                            "or say Ribbon.CanAddToQuickAccess=\"False\" if this command is not for the bar.",
                            DiagnosticSeverity.Warning, DiagnosticSeverity.Warning, true, 1));
                    }

                    metadata.Add(aumlDoc);
                }

                // Phase 1.5 - pre-register every control document (Window/View/Page) as a generated type BEFORE any body
                // is transformed, so cross-document references (e.g. <local:ControlsView/>) resolve regardless of file
                // processing order, including a view embedded inside another view.
                foreach (var aumlDoc in metadata)
                {
                    try
                    {
                        transformer.PreRegisterDocument(aumlDoc, typeResolver);
                    }
                    catch
                    {
                        // A malformed document surfaces its real error during the Phase 2 transform below; pre-registration
                        // is best-effort and must never abort the whole generation.
                    }
                }

                // Phase 2 - metadata transform and code generation based on sorted metadata
                var sortedMetadata = metadata.OrderBy(meta => GetGenerationPriority(meta.Root));

                foreach (var aumlDoc in sortedMetadata)
                {
                    var diagnostics = new RoslynDiagnosticSink(spc);

                    try
                    {
                        var aumlMetadataContainer = transformer.Transform(aumlDoc, typeResolver, diagnostics);
                        if (diagnostics.HasErrors)
                        {
                            continue;
                        }

                        // A fragment root (no Window/View/Page/Theme/StyleSet/ResourceDictionary) has no class to emit.
                        // The transformer still walks and judges it - the runtime loader previews exactly such markup -
                        // so the decision NOT to generate is stated here, where generation is decided.
                        if (aumlMetadataContainer.RootEntityType == EntityType.Unknown)
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
                    catch (System.Exception ex)
                    {
                        // Don't let one bad document silently abort generation (CS8785); surface where it broke.
                        var flat = ex.ToString().Replace("\r", "").Replace("\n", " | ");
                        spc.ReportDiagnostic(Diagnostic.Create("AUI900", "Build", $"AUML generation failed for {aumlDoc.RelativeFilePath}: {flat}", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0));
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