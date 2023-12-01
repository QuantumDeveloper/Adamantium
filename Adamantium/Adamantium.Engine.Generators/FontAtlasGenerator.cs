using System.Collections.Immutable;
using Adamantium.Core;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Microsoft.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using MessagePack;
using System.Diagnostics;
using System.Text;

namespace Adamantium.Engine.Generators
{
    [Generator]
    public class FontAtlasGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var adamantiumFontData = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".aff"));
            
            var fontNamesAndContents = adamantiumFontData.Select((text, cancellationToken) => (
            name: Path.GetFileName(text.Path),
            path: text.Path,
            content: text.GetText(cancellationToken)!.ToString(),
            fontDataName: Path.GetFileNameWithoutExtension(text.Path)));

            //var availableFonts = context.AdditionalTextsProvider.Where(x =>
            //    x.Path.EndsWith(".ttf") || 
            //    x.Path.EndsWith(".otf") || 
            //    x.Path.EndsWith(".woff") ||
            //    x.Path.EndsWith(".woff2")).Select((text, cancellationToken) => new FontFileInfo()
            //{
            //    FileName = Path.GetFileName(text.Path),
            //    Path = text.Path,
            //    Content = text.GetText(cancellationToken)!.ToString()
            //});

            //var fontsProvider = availableFonts.Collect();

            var sourceProvider = fontNamesAndContents.Combine(context.AnalyzerConfigOptionsProvider);

            context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
            {
                var (file, configOptions) = provider;
                configOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var @namespace);
                if (string.IsNullOrEmpty(@namespace))
                {
                    CreateDiagnostic(ref spc,
                        file.name,
                        "No RootNamespace Compiler option provided in project file. Please, add <CompilerVisibleProperty Include=\"RootNamespace\" /> to your csproj file",
                        DiagnosticSeverity.Error);
                }
                configOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
                var fontData = FontData.Parse(file.content);
                if (fontData.PathToFont.Contains('/'))
                {
                    var path = fontData.PathToFont.Split('/');
                    fontData.PathToFont = Path.Combine(path);
                }
                // Get relative file path for further calculations
                fontData.PathToFont = Path.Combine(projectDir, fontData.PathToFont);
                var result = GenerateAtlasData(fontData, @namespace, spc);
                spc.AddSource($"{result.ClassName}.g.cs", result.SourceText);
            });
        }

        private FontGeneratorResult GenerateAtlasData(FontData fontData, string rootNamespace, SourceProductionContext spc)
        {
            var result = new FontGeneratorResult();
            result.FinalNamespace = $"{rootNamespace}.GeneratedFonts";

            var typeface = TypeFace.LoadFont(fontData.PathToFont, 3);
            
            var font = typeface.GetFont(0);
            if (fontData.GlyphCount == 0)
            {
                fontData.GlyphCount = typeface.GlyphCount;
            }
            var atlasGen = new TextureAtlasGenerator();
            var atlasData = atlasGen.GenerateTextureAtlas(
                typeface, 
                font, 
                fontData.GlyphTextureSize, 
                fontData.SampleRate, 
                fontData.PixelRangle, 
                fontData.StartGlyphIndex,
                fontData.GlyphCount);

            atlasData.FontData = typeface.GetFontAsBytesArray();
            atlasData.Name = fontData.FontName;

            var source = GenerateSourceCode(atlasData);

            result.SourceText = source;

            return result;
        }

        private string GenerateSourceCode(FontAtlasData atlasData)
        {
            var resolver = CompositeResolver.Create(
                new IMessagePackFormatter[] { TypelessFormatter.Instance },
                new IFormatterResolver[] { StandardResolverAllowPrivate.Instance });

            var options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(resolver);
            
            var compressedData = MessagePackSerializer.Serialize(atlasData, options);

            var bytes = GetBytecodeAsText(compressedData);

            var textGenerator = new TextGenerator();
            textGenerator.WriteLine("using Adamantium.Fonts.TextureGeneration");

            textGenerator.WriteLine($"public class {atlasData.Name} : FontAtlas");

            return textGenerator.ToString();

        }

        private void CreateDiagnostic(ref SourceProductionContext spc, string filePath, string diagnosticText, DiagnosticSeverity severity)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "FontAtlasGenerator",
                title: "font atlas generator error",
                messageFormat: "{0}: {1}",
                category: "Font Atlas Generator",
                severity,
                isEnabledByDefault: true);
            var diagnostic = Diagnostic.Create(descriptor, Location.None, $"{filePath}.fx", diagnosticText);
            spc.ReportDiagnostic(diagnostic);
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
}
