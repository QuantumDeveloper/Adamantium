using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Adamantium.Content.Pipeline.Importers;
using Adamantium.Content.Pipeline.Writers;

namespace Adamantium.Content.Pipeline;

/// <summary>
/// Orchestrates the content build: resolves an importer (and writer) per asset, runs
/// importer → (optional) processor → writer, and cooks incrementally against a <see cref="BuildCache"/>.
/// Shared core for the CLI, an MSBuild step, and the editor/file-watcher.
/// </summary>
public sealed class ContentBuilder
{
    private readonly Dictionary<string, IContentImporter> importersByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IContentImporter> importersByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IContentWriter> writersByImporter = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds a content builder with the built-in importers/writers registered.</summary>
    public ContentBuilder()
    {
        Register(new ModelImporter(), new SceneDataWriter());
    }

    /// <summary>
    /// Registers an importer (its <see cref="ContentImporterAttribute"/> supplies the name + extensions) and
    /// the writer that serializes its output.
    /// </summary>
    public void Register(IContentImporter importer, IContentWriter writer)
    {
        var attribute = importer.GetType().GetCustomAttribute<ContentImporterAttribute>()
                        ?? throw new InvalidOperationException(
                            $"Importer {importer.GetType().Name} is missing [ContentImporter].");

        importersByName[attribute.Name] = importer;
        writersByImporter[attribute.Name] = writer;
        foreach (var extension in attribute.Extensions)
        {
            importersByExtension[extension] = importer;
        }
    }

    /// <summary>True if the builder can handle the given source extension.</summary>
    public bool CanImport(string extension) => importersByExtension.ContainsKey(extension);

    /// <summary>
    /// Adds entries for every supported source file found under <paramref name="projectDirectory"/> that is
    /// not already present in the manifest. Bootstraps/refreshes a manifest from convention.
    /// </summary>
    public void ScanInto(ContentManifest manifest, string projectDirectory)
    {
        var existing = new HashSet<string>(
            manifest.Assets.Select(a => Normalize(a.Source)), StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (!importersByExtension.TryGetValue(extension, out var importer))
            {
                continue;
            }

            var relative = Normalize(Path.GetRelativePath(projectDirectory, file));
            if (relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                !existing.Add(relative))
            {
                continue;
            }

            manifest.Assets.Add(new ContentAsset
            {
                Source = relative,
                Importer = importer.GetType().GetCustomAttribute<ContentImporterAttribute>()!.Name
            });
        }
    }

    /// <summary>Cooks every asset in the manifest that is stale, writing artifacts under the output directory.</summary>
    public BuildResult Build(ContentManifest manifest, string projectDirectory, Action<string> log = null)
    {
        log ??= _ => { };

        var outputDirectory = Path.GetFullPath(Path.Combine(projectDirectory, manifest.OutputDirectory));
        var intermediateDirectory = Path.GetFullPath(Path.Combine(projectDirectory, manifest.IntermediateDirectory));
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(intermediateDirectory);

        var cachePath = Path.Combine(intermediateDirectory, ".acache");
        var cache = BuildCache.Load(cachePath);
        var result = new BuildResult();

        foreach (var asset in manifest.Assets)
        {
            var sourceFull = Path.GetFullPath(Path.Combine(projectDirectory, asset.Source));
            if (!File.Exists(sourceFull))
            {
                log($"  [skip] missing source: {asset.Source}");
                result.Failed++;
                continue;
            }

            var importer = ResolveImporter(asset, sourceFull);
            if (importer == null)
            {
                log($"  [skip] no importer for: {asset.Source}");
                result.Failed++;
                continue;
            }

            var importerName = importer.GetType().GetCustomAttribute<ContentImporterAttribute>()!.Name;
            var writer = writersByImporter[importerName];

            // The source extension is kept in the logical name so two models that share a base name in
            // different formats (e.g. foo.dae and foo.3ds — which may be entirely different models) don't
            // collide. Cooked artifact is "<source>.aemf", load name is the source-relative path.
            var logicalName = string.IsNullOrEmpty(asset.LogicalName)
                ? Normalize(asset.Source)
                : Normalize(asset.LogicalName);
            var cookedRelative = logicalName + writer.OutputExtension;
            var cookedFull = Path.Combine(outputDirectory, cookedRelative);

            if (!cache.IsStale(asset.Source, sourceFull, asset.Parameters, cookedFull))
            {
                log($"  [up-to-date] {asset.Source}");
                result.Skipped++;
                continue;
            }

            log($"  [cook] {asset.Source} -> {cookedRelative}");
            var context = new ContentBuildContext
            {
                ProjectDirectory = projectDirectory,
                OutputDirectory = outputDirectory,
                IntermediateDirectory = intermediateDirectory,
                Parameters = asset.Parameters,
                Log = log
            };

            try
            {
                var content = importer.Import(sourceFull, context);

                Directory.CreateDirectory(Path.GetDirectoryName(cookedFull)!);
                using (var stream = File.Create(cookedFull))
                {
                    writer.Write(stream, content);
                }

                cache.Update(asset.Source, sourceFull, asset.Parameters, cookedRelative);
                result.Cooked++;
            }
            catch (Exception ex)
            {
                log($"  [error] {asset.Source}: {ex.Message}");
                result.Failed++;
            }
        }

        cache.Save(cachePath);
        return result;
    }

    private IContentImporter ResolveImporter(ContentAsset asset, string sourceFull)
    {
        if (!string.IsNullOrEmpty(asset.Importer) && importersByName.TryGetValue(asset.Importer, out var byName))
        {
            return byName;
        }

        return importersByExtension.GetValueOrDefault(Path.GetExtension(sourceFull));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
