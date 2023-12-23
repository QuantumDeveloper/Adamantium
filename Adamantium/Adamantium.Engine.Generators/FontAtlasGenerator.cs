using System;
using System.Collections.Immutable;
using Adamantium.Core;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Microsoft.CodeAnalysis;
using System.IO;
using System.Linq;
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
                var result = GenerateAtlasData(file.path, fontData, @namespace, spc);
                if (!result.HasErrors)
                {
                    spc.AddSource($"{result.ClassName}.g.cs", result.SourceText);
                }
            });
        }

        private FontGeneratorResult GenerateAtlasData(string filePath, FontData fontData, string rootNamespace, SourceProductionContext spc)
        {
            var result = new FontGeneratorResult();
            result.FinalNamespace = $"{rootNamespace}.GeneratedFonts";

            try
            {
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
                if (!string.IsNullOrEmpty(fontData.Name))
                {
                    atlasData.Name = fontData.Name;
                }
                else
                {
                    atlasData.Name = Path.GetFileNameWithoutExtension(filePath);
                }

                atlasData.Name = atlasData.Name.Replace("-", "");
                result.ClassName = $"{atlasData.Name}FontAtlas";

                var source = GenerateSourceCode(atlasData, rootNamespace);

                result.SourceText = source;

            }
            catch (Exception ex)
            {
                CreateDiagnostic(ref spc, filePath, ex.Message, DiagnosticSeverity.Error);
                result.HasErrors = true;
            }

            return result;
        }

        private string GenerateSourceCode(FontAtlasData atlasData, string @namespace)
        {
            var compressedData = atlasData.Save();

            var bytes = GetBytecodeAsText(compressedData);
            var className = $"{atlasData.Name}FontAtlas";

            var textGenerator = new TextGenerator();
            textGenerator.WriteLine($"namespace {@namespace}.Fonts.Generated;");

            textGenerator.NewLine();

            textGenerator.WriteLine("using Adamantium.Engine.Graphics;");
            textGenerator.WriteLine("using Adamantium.Fonts.TextureGeneration;");
            textGenerator.WriteLine("using Adamantium.Engine.Graphics.Fonts;");

            textGenerator.NewLine();

            textGenerator.WriteLine($"public class {className} : FontAtlas");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.WriteLine(@$"private static readonly FontAtlasData atlasData = FontAtlasData.Load(new byte[] {{
            {bytes}
            }});");

            textGenerator.WriteLine($"public {className}(GraphicsDevice device) : base(device, atlasData)");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.UnindentAndWriteCloseBrace();

            textGenerator.UnindentAndWriteCloseBrace();

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
