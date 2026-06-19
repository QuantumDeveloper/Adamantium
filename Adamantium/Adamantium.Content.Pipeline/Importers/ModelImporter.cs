using Adamantium.Engine.Compiler.Models;

namespace Adamantium.Content.Pipeline.Importers;

/// <summary>
/// Imports a 3D model file (Collada/OBJ/3DS) into engine-native <c>SceneData</c> via the existing
/// <see cref="ModelConverter"/>. Mesh optimization/normal+tangent generation happen inside the converter,
/// so no separate processor is needed yet.
/// </summary>
[ContentImporter("Model", ".dae", ".obj", ".3ds")]
public sealed class ModelImporter : IContentImporter
{
    public object Import(string sourcePath, ContentBuildContext context)
    {
        return new ModelConverter().ImportFileAsync(sourcePath);
    }
}
