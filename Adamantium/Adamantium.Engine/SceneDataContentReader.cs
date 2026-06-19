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
        }

        return Task.FromResult((object)scene);
    }
}
