using System;
using System.IO;
using System.Threading.Tasks;
using Adamantium.Engine.Compiler.Models;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine;

/// <summary>
/// Loads a model into engine-native <see cref="SceneData"/>. A baked artifact (.aemf) is deserialized via
/// <see cref="SceneDataSerializer"/>; a raw model file (Collada/OBJ/3DS) is parsed by the
/// <see cref="ModelConverter"/>. Which one is used is decided by the resolver (see
/// <see cref="CookedContentResolver"/>, which prefers cooked content), so this reader just branches on the
/// resolved file's extension. Assembling an <c>Entity</c> from the result stays the caller's job
/// (e.g. <see cref="Templates.EntityImportTemplate"/>), so the reader remains pure.
/// </summary>
public class SceneDataContentReader : IContentReader
{
    public Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters)
    {
        SceneData scene;
        if (parameters.AssetPath != null &&
            parameters.AssetPath.EndsWith(".aemf", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(parameters.AssetPath);
            scene = SceneDataSerializer.Deserialize(stream);
        }
        else
        {
            scene = new ModelConverter().ImportFile(parameters.AssetPath);
        }

        if (scene != null)
        {
            scene.Name = parameters.AssetName;
            PointImagesAtTheModel(scene, parameters.AssetPath);
        }

        return Task.FromResult((object)scene);
    }

    // A baked scene stores only the RELATIVE reference the model file wrote (Image.FilePath is not persisted - an
    // absolute path of the baking machine points nowhere after the first move). Resolving it is this reader's job,
    // and the anchor is the file we ACTUALLY opened, not the logical asset name: a cooked artifact lives in the
    // output folder with its textures copied beside it, which the logical name knows nothing about.
    private static void PointImagesAtTheModel(SceneData scene, string assetPath)
    {
        if (scene.Images == null || string.IsNullOrEmpty(assetPath)) return;

        var beside = Path.GetDirectoryName(Path.GetFullPath(assetPath)) ?? string.Empty;

        foreach (var image in scene.Images.Values)
        {
            if (string.IsNullOrEmpty(image?.ImageName)) continue;

            image.FilePath = Path.GetFullPath(Path.Combine(beside, image.ImageName));
        }
    }
}
