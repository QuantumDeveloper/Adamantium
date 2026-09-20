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
            scene = new ModelConverter().ImportFileAsync(parameters.AssetPath);
        }

        if (scene != null)
        {
            scene.Name = parameters.AssetName;
            PointImagesAtTheModel(scene, parameters.AssetName);
        }

        return Task.FromResult((object)scene);
    }

    // A baked scene carries the ABSOLUTE texture paths of the machine that baked it, so another checkout - or a
    // renamed folder - leaves a model silently untextured. Where the model itself was asked for is the authority.
    private static void PointImagesAtTheModel(SceneData scene, string assetName)
    {
        if (scene.Images == null || string.IsNullOrEmpty(assetName)) return;

        var beside = Path.GetDirectoryName(assetName.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;

        foreach (var image in scene.Images.Values)
        {
            if (string.IsNullOrEmpty(image?.ImageName)) continue;

            var near = Path.GetFullPath(Path.Combine(beside, image.ImageName));
            if (File.Exists(near)) image.FilePath = near;
        }
    }
}
