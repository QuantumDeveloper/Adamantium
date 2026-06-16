using System.Threading.Tasks;
using Adamantium.Engine.Compiler.Models;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine;

/// <summary>
/// Reads a 3D model file (Collada/OBJ/3DS) into engine-native <see cref="SceneData"/> by running the model
/// converter. Assembling an <c>Entity</c> from this <see cref="SceneData"/> is the caller's job (e.g.
/// <see cref="Templates.EntityImportTemplate"/>), so this reader stays pure.
/// Phase 2 (build-time baking) will swap this runtime parse for deserializing a precompiled binary — the
/// <see cref="SceneData"/> result and everything downstream of it stay the same.
/// </summary>
public class SceneDataContentReader : IContentReader
{
    public Task<object> ReadContentAsync(IContentManager contentManager, ContentReaderParameters parameters)
    {
        var scene = new ModelConverter().ImportFileAsync(parameters.AssetPath);
        if (scene != null)
        {
            scene.Name = parameters.AssetName;
        }
        return Task.FromResult((object)scene);
    }
}
